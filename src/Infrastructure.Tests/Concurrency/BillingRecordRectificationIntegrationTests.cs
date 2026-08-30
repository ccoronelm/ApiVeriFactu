using gesFactu.Application.Common;
using gesFactu.Application.RegistrosFacturacion.Commands.CrearRectificativa;
using gesFactu.Application.RegistrosFacturacion.Commands.CrearRegistro;
using gesFactu.Infrastructure.Persistence;
using gesFactu.Infrastructure.Persistence.Repositories;
using gesFactu.Infrastructure.VeriFactu;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace gesFactu.Infrastructure.Tests.Concurrency;

public sealed class BillingRecordRectificationIntegrationTests
{
    [PostgreSqlFact]
    public async Task R4I_ConImportesNegativos_SePersisteYEncadena()
    {
        var connectionString = await CreateDatabaseAsync();

        try
        {
            await using var context = CreateContext(connectionString);
            var repository = new BillingRecordRepository(context);
            var calculator = new Sha256HashCalculator();

            var createHandler = new CreateBillingRecordCommandHandler(
                context,
                repository,
                calculator,
                NullLogger<CreateBillingRecordCommandHandler>.Instance);

            var sourceResult = await createHandler.Handle(
                new CreateBillingRecordCommand(
                    "12345678A",
                    "A/",
                    "000001",
                    "30-08-2026",
                    "Emisor pruebas",
                    "B98588544",
                    "INICIATIVAS EN ANALISIS CLINICOS SL",
                    "Factura origen",
                    121.00m,
                    21.00m,
                    "F1"),
                CancellationToken.None);

            var sourceSuccess =
                Assert.IsType<Result<CreateBillingRecordResponse>.SuccessWithValue>(
                    sourceResult);

            var source = await context.BillingRecords.SingleAsync(
                x => x.Id == sourceSuccess.Value.BillingRecordId);
            source.MarkAsAccepted();
            await context.SaveChangesAsync();

            var handler = new CreateRectificativeBillingRecordCommandHandler(
                context,
                repository,
                calculator,
                NullLogger<CreateRectificativeBillingRecordCommandHandler>.Instance);

            var result = await handler.Handle(
                new CreateRectificativeBillingRecordCommand(
                    source.Id,
                    "R/",
                    "000001",
                    "30-08-2026",
                    "R4",
                    "I",
                    "Rectificación por diferencias",
                    -24.20m,
                    -4.20m),
                CancellationToken.None);

            var success =
                Assert.IsType<Result<CreateRectificativeBillingRecordResponse>.SuccessWithValue>(
                    result);

            var stored = await context.BillingRecords.SingleAsync(
                x => x.Id == success.Value.BillingRecordId);

            Assert.Equal("R4", stored.InvoiceType);
            Assert.Equal("I", stored.RectificationType);
            Assert.Equal(source.Id, stored.RectifiesBillingRecordId);
            Assert.Equal(-24.20m, stored.TotalAmount);
            Assert.Equal(-4.20m, stored.TotalTaxAmount);
            Assert.Null(stored.RectifiedBaseAmount);
            Assert.Equal(source.Id, stored.PreviousBillingRecordId);
            Assert.Equal(source.ComputedHash, stored.PreviousRecordHash);

            var expectedHash = calculator.CalculateChainHash(
                new gesFactu.Application.Common.Abstractions.BillingRecordHashInput
                {
                    PreviousHash = source.ComputedHash!,
                    IssuerNif = stored.IssuerNif,
                    InvoiceSeries = stored.InvoiceSeries,
                    InvoiceNumber = stored.InvoiceNumber,
                    IssueDate = "30-08-2026",
                    InvoiceType = "R4",
                    TotalAmount = -24.20m,
                    TotalTaxAmount = -4.20m,
                    RegisterTimestamp = stored.RegisterTimestamp
                });

            Assert.Equal(expectedHash, stored.ComputedHash);
        }
        finally
        {
            await DeleteDatabaseAsync(connectionString);
        }
    }

    [PostgreSqlFact]
    public async Task R1S_PersisteImportesSustituidosDelOrigen()
    {
        var connectionString = await CreateDatabaseAsync();

        try
        {
            await using var context = CreateContext(connectionString);
            var repository = new BillingRecordRepository(context);
            var calculator = new Sha256HashCalculator();
            var createHandler = new CreateBillingRecordCommandHandler(
                context,
                repository,
                calculator,
                NullLogger<CreateBillingRecordCommandHandler>.Instance);

            var sourceResult = await createHandler.Handle(
                new CreateBillingRecordCommand(
                    "12345678A",
                    "A/",
                    "000002",
                    "30-08-2026",
                    "Emisor pruebas",
                    "B98588544",
                    "INICIATIVAS EN ANALISIS CLINICOS SL",
                    "Factura origen",
                    121.00m,
                    21.00m,
                    "F1"),
                CancellationToken.None);

            var sourceSuccess =
                Assert.IsType<Result<CreateBillingRecordResponse>.SuccessWithValue>(
                    sourceResult);
            var source = await context.BillingRecords.SingleAsync(
                x => x.Id == sourceSuccess.Value.BillingRecordId);
            source.MarkAsAccepted();
            await context.SaveChangesAsync();

            var handler = new CreateRectificativeBillingRecordCommandHandler(
                context,
                repository,
                calculator,
                NullLogger<CreateRectificativeBillingRecordCommandHandler>.Instance);

            var result = await handler.Handle(
                new CreateRectificativeBillingRecordCommand(
                    source.Id,
                    "R/",
                    "000002",
                    "30-08-2026",
                    "R1",
                    "S",
                    "Rectificación sustitutiva",
                    96.80m,
                    16.80m),
                CancellationToken.None);

            var success =
                Assert.IsType<Result<CreateRectificativeBillingRecordResponse>.SuccessWithValue>(
                    result);
            var stored = await context.BillingRecords.SingleAsync(
                x => x.Id == success.Value.BillingRecordId);

            Assert.Equal(100.00m, stored.RectifiedBaseAmount);
            Assert.Equal(21.00m, stored.RectifiedTaxAmount);
            Assert.Null(stored.RectifiedSurchargeAmount);
        }
        finally
        {
            await DeleteDatabaseAsync(connectionString);
        }
    }

    private static ApplicationDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task<string> CreateDatabaseAsync()
    {
        var serverConnection =
            Environment.GetEnvironmentVariable("GESFACTU_TEST_POSTGRESQL");

        if (string.IsNullOrWhiteSpace(serverConnection))
            throw new InvalidOperationException(
                "GESFACTU_TEST_POSTGRESQL no está configurada.");

        var databaseName = "gesFactuRectCi_" + Guid.NewGuid().ToString("N");
        var connectionString =
            serverConnection.TrimEnd(';') + $";Database={databaseName};";

        Exception? lastError = null;
        for (var attempt = 0; attempt < 30; attempt++)
        {
            try
            {
                await using var context = CreateContext(connectionString);
                await context.Database.EnsureCreatedAsync();
                return connectionString;
            }
            catch (Exception ex)
            {
                lastError = ex;
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }

        throw new InvalidOperationException(
            "PostgreSQL de pruebas no estuvo disponible a tiempo.",
            lastError);
    }

    private static async Task DeleteDatabaseAsync(string connectionString)
    {
        try
        {
            await using var context = CreateContext(connectionString);
            await context.Database.EnsureDeletedAsync();
        }
        catch
        {
        }
    }
}
