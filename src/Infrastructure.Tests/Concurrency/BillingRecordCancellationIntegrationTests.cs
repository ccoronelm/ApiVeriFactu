using gesFactu.Application.Common;
using gesFactu.Application.RegistrosFacturacion.Commands.CrearAnulacion;
using gesFactu.Application.RegistrosFacturacion.Commands.CrearRegistro;
using gesFactu.Domain.Entities;
using gesFactu.Infrastructure.Persistence;
using gesFactu.Infrastructure.Persistence.Repositories;
using gesFactu.Infrastructure.VeriFactu;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace gesFactu.Infrastructure.Tests.Concurrency;

public sealed class BillingRecordCancellationIntegrationTests
{
    [PostgreSqlFact]
    public async Task Anulacion_ReutilizaIdentidadFiscal_YEncadenaConUltimoRegistro()
    {
        var connectionString = await CreateDatabaseAsync();

        try
        {
            var firstResult = await CreateRecordAsync(
                connectionString,
                "VF/",
                "C001",
                "Factura a anular");

            var firstId =
                ((Result<CreateBillingRecordResponse>.SuccessWithValue)firstResult)
                .Value.BillingRecordId;

            await using (var context = CreateContext(connectionString))
            {
                var firstInSetup = await context.BillingRecords.SingleAsync(x => x.Id == firstId);
                firstInSetup.Status = "Aceptado";
                await context.SaveChangesAsync();
            }

            var secondResult = await CreateRecordAsync(
                connectionString,
                "VF/",
                "C002",
                "Registro posterior");

            var secondId =
                ((Result<CreateBillingRecordResponse>.SuccessWithValue)secondResult)
                .Value.BillingRecordId;

            await using var cancellationContext = CreateContext(connectionString);
            var repository = new BillingRecordRepository(cancellationContext);
            var handler = new CreateBillingRecordCancellationCommandHandler(
                cancellationContext,
                repository,
                new Sha256HashCalculator(),
                NullLogger<CreateBillingRecordCancellationCommandHandler>.Instance);

            var result = await handler.Handle(
                new CreateBillingRecordCancellationCommand(firstId),
                CancellationToken.None);

            var success =
                Assert.IsType<Result<CreateBillingRecordCancellationResponse>.SuccessWithValue>(
                    result);

            var cancellation = await cancellationContext.BillingRecords
                .SingleAsync(x => x.Id == success.Value.BillingRecordId);

            var first = await cancellationContext.BillingRecords
                .SingleAsync(x => x.Id == firstId);

            var second = await cancellationContext.BillingRecords
                .SingleAsync(x => x.Id == secondId);

            Assert.Equal(BillingRecord.CancellationRecordType, cancellation.RecordType);
            Assert.Equal(first.Id, cancellation.CancelsBillingRecordId);
            Assert.Equal(first.FiscalInvoiceNumber, cancellation.FiscalInvoiceNumber);
            Assert.Equal(first.IssueDate, cancellation.IssueDate);
            Assert.Equal(second.Id, cancellation.PreviousBillingRecordId);
            Assert.Equal(second.ComputedHash, cancellation.PreviousRecordHash);
            Assert.NotEqual(second.ComputedHash, cancellation.ComputedHash);
            Assert.Equal(3, await cancellationContext.BillingRecords.CountAsync());

            var secondCancellation = await handler.Handle(
                new CreateBillingRecordCancellationCommand(firstId),
                CancellationToken.None);

            Assert.IsType<Result<CreateBillingRecordCancellationResponse>.ConflictError>(
                secondCancellation);
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
            throw new InvalidOperationException(
                "GESFACTU_TEST_POSTGRESQL no está configurada.");

        var databaseName = "gesFactuCancelCi_" + Guid.NewGuid().ToString("N");
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
