using gesFactu.Application.Common;
using gesFactu.Application.RegistrosFacturacion.Commands.CrearRegistro;
using gesFactu.Infrastructure.Persistence;
using gesFactu.Infrastructure.Persistence.Repositories;
using gesFactu.Infrastructure.VeriFactu;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace gesFactu.Infrastructure.Tests.Concurrency;

public sealed class BillingTaxDetailsIntegrationTests
{
    [Fact]
    public void Factory_RechazaExentaConCuota()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            BillingTaxDetailFactory.Create(
                [
                    new BillingTaxDetailInput(
                        "01", "01", null, "E1",
                        21m, 100m, 21m)
                ],
                121m,
                21m));

        Assert.Contains("exentas", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Factory_AceptaRecargoYMultiplesTipos()
    {
        var details = BillingTaxDetailFactory.Create(
            [
                new BillingTaxDetailInput(
                    "01", "01", "S1", null,
                    21m, 100m, 21m, 5.2m, 5.2m),
                new BillingTaxDetailInput(
                    "01", "01", "S1", null,
                    10m, 50m, 5m)
            ],
            181.20m,
            31.20m);

        Assert.Equal(2, details.Count);
    }

    [PostgreSqlFact]
    public async Task AltaConDosTiposIva_PersisteDesgloses()
    {
        var connectionString = await CreateDatabaseAsync();

        try
        {
            await using var context = CreateContext(connectionString);
            var repository = new BillingRecordRepository(context);
            var handler = new CreateBillingRecordCommandHandler(
                context,
                repository,
                new Sha256HashCalculator(),
                NullLogger<CreateBillingRecordCommandHandler>.Instance);

            var result = await handler.Handle(
                new CreateBillingRecordCommand(
                    "12345678A",
                    "MIX/",
                    "000001",
                    "30-08-2026",
                    "Emisor pruebas",
                    "B98588544",
                    "INICIATIVAS EN ANALISIS CLINICOS SL",
                    "Factura con múltiples tipos de IVA",
                    176m,
                    26m,
                    "F1",
                    [
                        new BillingTaxDetailInput(
                            "01", "01", "S1", null,
                            21m, 100m, 21m),
                        new BillingTaxDetailInput(
                            "01", "01", "S1", null,
                            10m, 50m, 5m)
                    ]),
                CancellationToken.None);

            var success =
                Assert.IsType<Result<CreateBillingRecordResponse>.SuccessWithValue>(
                    result);

            var stored = await context.BillingRecords
                .Include(x => x.TaxDetails)
                .SingleAsync(x => x.Id == success.Value.BillingRecordId);

            Assert.Equal(2, stored.TaxDetails.Count);
            Assert.Contains(stored.TaxDetails, x => x.TaxRate == 21m);
            Assert.Contains(stored.TaxDetails, x => x.TaxRate == 10m);
            Assert.Equal(26m, stored.TaxDetails.Sum(x => x.TaxAmount ?? 0m));
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

        var databaseName = "gesFactuTaxCi_" + Guid.NewGuid().ToString("N");
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
