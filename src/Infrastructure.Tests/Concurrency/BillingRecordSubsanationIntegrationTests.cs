using gesFactu.Application.Common;
using gesFactu.Application.RegistrosFacturacion.Commands.CrearRegistro;
using gesFactu.Application.RegistrosFacturacion.Commands.CrearSubsanacion;
using gesFactu.Infrastructure.Persistence;
using gesFactu.Infrastructure.Persistence.Repositories;
using gesFactu.Infrastructure.VeriFactu;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace gesFactu.Infrastructure.Tests.Concurrency;

public sealed class BillingRecordSubsanationIntegrationTests
{
    [PostgreSqlFact]
    public async Task Subsanacion_ReutilizaIdentidadFiscal_YEncadenaConUltimoRegistro()
    {
        var connectionString = await CreateDatabaseAsync();

        try
        {
            var firstResult = await CreateRecordAsync(
                connectionString,
                "VF/",
                "000001",
                "Primera factura");

            var firstId = ((Result<CreateBillingRecordResponse>.SuccessWithValue)firstResult)
                .Value.BillingRecordId;

            await using (var context = CreateContext(connectionString))
            {
                var first = await context.BillingRecords.SingleAsync(x => x.Id == firstId);
                first.Status = "AceptadoConErrores";
                await context.SaveChangesAsync();
            }

            var secondResult = await CreateRecordAsync(
                connectionString,
                "VF/",
                "000002",
                "Segunda factura");

            var secondId = ((Result<CreateBillingRecordResponse>.SuccessWithValue)secondResult)
                .Value.BillingRecordId;

            await using (var context = CreateContext(connectionString))
            {
                var repository = new BillingRecordRepository(context);
                var handler = new CreateBillingRecordSubsanationCommandHandler(
                    context,
                    repository,
                    new Sha256HashCalculator(),
                    NullLogger<CreateBillingRecordSubsanationCommandHandler>.Instance);

                var result = await handler.Handle(
                    new CreateBillingRecordSubsanationCommand(
                        firstId,
                        null,
                        null,
                        null,
                        null,
                        null),
                    CancellationToken.None);

                var success =
                    Assert.IsType<Result<CreateBillingRecordSubsanationResponse>.SuccessWithValue>(result);

                var subsanation = await context.BillingRecords
                    .SingleAsync(x => x.Id == success.Value.BillingRecordId);

                var first = await context.BillingRecords.SingleAsync(x => x.Id == firstId);
                var second = await context.BillingRecords.SingleAsync(x => x.Id == secondId);

                Assert.Equal(first.IssuerNif, subsanation.IssuerNif);
                Assert.Equal(first.FiscalInvoiceNumber, subsanation.FiscalInvoiceNumber);
                Assert.Equal(first.IssueDate, subsanation.IssueDate);
                Assert.Equal(first.Id, subsanation.SubsanatesBillingRecordId);

                Assert.Equal(second.Id, subsanation.PreviousBillingRecordId);
                Assert.Equal(second.ComputedHash, subsanation.PreviousRecordHash);
                Assert.NotEqual(first.ComputedHash, subsanation.ComputedHash);

                Assert.Equal(3, await context.BillingRecords.CountAsync());
            }
        }
        finally
        {
            await DeleteDatabaseAsync(connectionString);
        }
    }

    private static async Task<Result<CreateBillingRecordResponse>> CreateRecordAsync(
        string connectionString,
        string series,
        string number,
        string description)
    {
        await using var context = CreateContext(connectionString);
        var repository = new BillingRecordRepository(context);
        var handler = new CreateBillingRecordCommandHandler(
            context,
            repository,
            new Sha256HashCalculator(),
            NullLogger<CreateBillingRecordCommandHandler>.Instance);

        return await handler.Handle(
            new CreateBillingRecordCommand(
                "12345678A",
                series,
                number,
                "30-08-2026",
                "Emisor pruebas",
                "87654321B",
                "Destinatario pruebas",
                description,
                121.23m,
                21.04m),
            CancellationToken.None);
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
        var serverConnection = Environment.GetEnvironmentVariable(
            "GESFACTU_TEST_POSTGRESQL");

        if (string.IsNullOrWhiteSpace(serverConnection))
            throw new InvalidOperationException("GESFACTU_TEST_POSTGRESQL no está configurada.");

        var databaseName = "gesFactuCi_" + Guid.NewGuid().ToString("N");
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
