using gesFactu.Application.Common;
using gesFactu.Application.RegistrosFacturacion.Commands.CrearRegistro;
using gesFactu.Infrastructure.Integrations.VeriFactu;
using gesFactu.Infrastructure.Persistence;
using gesFactu.Infrastructure.Persistence.Repositories;
using gesFactu.Infrastructure.VeriFactu;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace gesFactu.Infrastructure.Tests.Concurrency;

public sealed class MultiTaxpayerIntegrationTests
{
    [PostgreSqlFact]
    public async Task DosObligados_MismaFactura_ConservanIdempotenciaYCadenasSeparadas()
    {
        var connectionString = await CreateDatabaseAsync();

        try
        {
            await using var context = CreateContext(connectionString);
            var repository = new BillingRecordRepository(context);
            var options = Options.Create(CreateOptions());
            var registry = new ConfiguredVeriFactuTaxpayerRegistry(options);

            var handler = new CreateBillingRecordCommandHandler(
                context,
                repository,
                new Sha256HashCalculator(),
                NullLogger<CreateBillingRecordCommandHandler>.Instance,
                registry);

            var a1 = await CreateAsync(
                handler,
                "12345678A",
                "EMPRESA A SL",
                "A/",
                "0001");

            var b1 = await CreateAsync(
                handler,
                "87654321B",
                "EMPRESA B SL",
                "A/",
                "0001");

            var a2 = await CreateAsync(
                handler,
                "12345678A",
                "EMPRESA A SL",
                "A/",
                "0002");

            var recordA1 = await context.BillingRecords.SingleAsync(x => x.Id == a1);
            var recordB1 = await context.BillingRecords.SingleAsync(x => x.Id == b1);
            var recordA2 = await context.BillingRecords.SingleAsync(x => x.Id == a2);

            Assert.Null(recordA1.PreviousBillingRecordId);
            Assert.Null(recordB1.PreviousBillingRecordId);

            Assert.Equal(recordA1.Id, recordA2.PreviousBillingRecordId);
            Assert.Equal(recordA1.ComputedHash, recordA2.PreviousRecordHash);

            Assert.NotEqual(recordA1.ComputedHash, recordB1.ComputedHash);

            Assert.Equal(
                2,
                await context.BillingRecords.CountAsync(
                    x => x.FiscalInvoiceNumber == "A/0001"));
        }
        finally
        {
            await DeleteDatabaseAsync(connectionString);
        }
    }

    [PostgreSqlFact]
    public async Task Alta_DeObligadoNoConfigurado_SeRechaza()
    {
        var connectionString = await CreateDatabaseAsync();

        try
        {
            await using var context = CreateContext(connectionString);
            var repository = new BillingRecordRepository(context);
            var registry = new ConfiguredVeriFactuTaxpayerRegistry(
                Options.Create(CreateOptions()));

            var handler = new CreateBillingRecordCommandHandler(
                context,
                repository,
                new Sha256HashCalculator(),
                NullLogger<CreateBillingRecordCommandHandler>.Instance,
                registry);

            var result = await handler.Handle(
                Command(
                    "11111111H",
                    "NO CONFIGURADA SL",
                    "A/",
                    "0099"),
                CancellationToken.None);

            var error =
                Assert.IsType<Result<CreateBillingRecordResponse>.ValidationError>(
                    result);

            Assert.Equal("IssuerNif", error.PropertyName);
            Assert.Empty(context.BillingRecords);
        }
        finally
        {
            await DeleteDatabaseAsync(connectionString);
        }
    }

    private static async Task<int> CreateAsync(
        CreateBillingRecordCommandHandler handler,
        string nif,
        string name,
        string series,
        string number)
    {
        var result = await handler.Handle(
            Command(nif, name, series, number),
            CancellationToken.None);

        var success =
            Assert.IsType<Result<CreateBillingRecordResponse>.SuccessWithValue>(
                result);

        return success.Value.BillingRecordId;
    }

    private static CreateBillingRecordCommand Command(
        string nif,
        string name,
        string series,
        string number)
        => new(
            nif,
            series,
            number,
            "30-08-2026",
            name,
            "B98588544",
            "INICIATIVAS EN ANALISIS CLINICOS SL",
            "Prueba multiobligado",
            121m,
            21m,
            "F1");

    private static VeriFactuOptions CreateOptions()
        => new()
        {
            Environment = VeriFactuEntorno.Test,
            Taxpayers =
            [
                new VeriFactuTaxpayerProfileOptions
                {
                    Key = "empresa-a",
                    Nif = "12345678A",
                    Name = "EMPRESA A SL",
                    InstallationNumber = "INST-A"
                },
                new VeriFactuTaxpayerProfileOptions
                {
                    Key = "empresa-b",
                    Nif = "87654321B",
                    Name = "EMPRESA B SL",
                    InstallationNumber = "INST-B"
                }
            ],
            SistemaInformatico = new SistemaInformaticoOptions
            {
                NombreRazon = "PRODUCTOR SL",
                Nif = "89890001K",
                NombreSistemaInformatico = "gesFactu",
                IdSistemaInformatico = "GF",
                Version = "1.0",
                NumeroInstalacion = "DEFAULT",
                TipoUsoPosibleSoloVerifactu = "S",
                TipoUsoPosibleMultiOT = "S",
                IndicadorMultiplesOT = "S"
            }
        };

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

        var databaseName = "gesFactuMultiCi_" + Guid.NewGuid().ToString("N");
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
