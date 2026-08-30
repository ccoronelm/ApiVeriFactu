using gesFactu.Application.Common;
using gesFactu.Application.RegistrosFacturacion.Commands.CrearRegistro;
using gesFactu.Infrastructure.Persistence;
using gesFactu.Infrastructure.Persistence.Repositories;
using gesFactu.Infrastructure.VeriFactu;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace gesFactu.Infrastructure.Tests.Concurrency;

public sealed class BillingRecordF2IntegrationTests
{
    [PostgreSqlFact]
    public async Task F2_SinDestinatario_SePersisteYCalculaHuellaConTipoF2()
    {
        var connectionString = await CreateDatabaseAsync();

        try
        {
            await using var context = CreateContext(connectionString);
            var repository = new BillingRecordRepository(context);
            var calculator = new Sha256HashCalculator();
            var handler = new CreateBillingRecordCommandHandler(
                context,
                repository,
                calculator,
                NullLogger<CreateBillingRecordCommandHandler>.Instance);

            var result = await handler.Handle(
                new CreateBillingRecordCommand(
                    "12345678A",
                    "F2/",
                    "000001",
                    "30-08-2026",
                    "Emisor pruebas",
                    null,
                    null,
                    "Factura simplificada",
                    12.10m,
                    2.10m,
                    "F2"),
                CancellationToken.None);

            var success =
                Assert.IsType<Result<CreateBillingRecordResponse>.SuccessWithValue>(
                    result);

            var stored = await context.BillingRecords.SingleAsync(
                x => x.Id == success.Value.BillingRecordId);

            Assert.Equal("F2", stored.InvoiceType);
            Assert.Equal(string.Empty, stored.RecipientNif);
            Assert.Equal(string.Empty, stored.RecipientName);

            var expected = calculator.CalculateChainHash(
                new gesFactu.Application.Common.Abstractions.BillingRecordHashInput
                {
                    PreviousHash = string.Empty,
                    IssuerNif = stored.IssuerNif,
                    InvoiceSeries = stored.InvoiceSeries,
                    InvoiceNumber = stored.InvoiceNumber,
                    IssueDate = "30-08-2026",
                    InvoiceType = "F2",
                    TotalAmount = stored.TotalAmount,
                    TotalTaxAmount = stored.TotalTaxAmount,
                    RegisterTimestamp = stored.RegisterTimestamp
                });

            Assert.Equal(expected, stored.ComputedHash);
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
        var serverConnection = Environment.GetEnvironmentVariable(
            "GESFACTU_TEST_POSTGRESQL");

        if (string.IsNullOrWhiteSpace(serverConnection))
            throw new InvalidOperationException(
                "GESFACTU_TEST_POSTGRESQL no está configurada.");

        var databaseName = "gesFactuF2Ci_" + Guid.NewGuid().ToString("N");
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
