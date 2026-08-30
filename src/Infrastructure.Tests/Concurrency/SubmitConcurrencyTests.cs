using gesFactu.Application.Common;
using gesFactu.Application.RegistrosFacturacion.Commands.EnviarAEAT;
using gesFactu.Domain.Entities;
using gesFactu.Domain.ValueObjects;
using gesFactu.Infrastructure.Integrations.VeriFactu;
using gesFactu.Infrastructure.Integrations.VeriFactu.Validation;
using gesFactu.Infrastructure.Integrations.VeriFactu.XmlGeneration;
using gesFactu.Infrastructure.Persistence;
using gesFactu.Infrastructure.Persistence.Repositories;
using gesFactu.Infrastructure.VeriFactu;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace gesFactu.Infrastructure.Tests.Concurrency;

public sealed class SubmitConcurrencyTests
{
    [PostgreSqlFact]
    public async Task ConcurrentSubmit_CreatesSingleOutboxMessage()
    {
        var connectionString = await CreateDatabaseAsync();

        try
        {
            var recordId = await SeedBillingRecordAsync(connectionString);

            var results = await Task.WhenAll(
                SubmitAsync(connectionString, recordId),
                SubmitAsync(connectionString, recordId));

            Assert.Equal(
                1,
                results.Count(x =>
                    x is Result<EnviarRegistroAEATResponse>.SuccessWithValue));

            Assert.Equal(
                1,
                results.Count(x =>
                    x is Result<EnviarRegistroAEATResponse>.ConflictError));

            await using var verify = CreateContext(connectionString);

            Assert.Equal(
                1,
                await verify.OutboxMessages.CountAsync(
                    x => x.AggregateId == recordId));

            var stored = await verify.BillingRecords.SingleAsync(
                x => x.Id == recordId);

            Assert.True(stored.IsSubmitted);
            Assert.NotNull(stored.SubmissionCorrelationId);
            Assert.Null(stored.AeatSubmissionId);
            Assert.Equal("PendienteEnvio", stored.Status);
        }
        finally
        {
            await DeleteDatabaseAsync(connectionString);
        }
    }

    private static async Task<Result<EnviarRegistroAEATResponse>> SubmitAsync(
        string connectionString,
        int recordId)
    {
        await using var context = CreateContext(connectionString);

        var repository = new BillingRecordRepository(context);
        var outboxStore = new OutboxStore(context);

        var options = Options.Create(CreateOptions());
        var builder = new RegistroAltaXmlBuilderAdapter(options);
        var cancellationBuilder = new RegistroAnulacionXmlBuilderAdapter(options);

        var validator = new XmlSchemaValidator(
            NullLogger<XmlSchemaValidator>.Instance,
            Path.Combine(AppContext.BaseDirectory, "VERIFACTU"));

        var handler = new EnviarRegistroAEATCommandHandler(
            repository,
            context,
            outboxStore,
            builder,
            cancellationBuilder,
            validator,
            new Sha256HashCalculator(),
            NullLogger<EnviarRegistroAEATCommandHandler>.Instance);

        return await handler.Handle(
            new EnviarRegistroAEATCommand(recordId),
            CancellationToken.None);
    }

    private static async Task<int> SeedBillingRecordAsync(string connectionString)
    {
        await using var context = CreateContext(connectionString);

        var nif = ((ValueObjectResult<TaxpayerNif>.SuccessWithValue)
            TaxpayerNif.Create("89890001K")).Value;

        var series = ((ValueObjectResult<InvoiceSeries>.SuccessWithValue)
            InvoiceSeries.Create("A/")).Value;

        var number = ((ValueObjectResult<InvoiceNumber>.SuccessWithValue)
            InvoiceNumber.Create("0001")).Value;

        var identifier = ((ValueObjectResult<InvoiceIdentifier>.SuccessWithValue)
            InvoiceIdentifier.Create(
                nif,
                series,
                number,
                new DateOnly(2026, 8, 30))).Value;

        var total = ((ValueObjectResult<Money>.SuccessWithValue)
            Money.Create(121m)).Value;

        var tax = ((ValueObjectResult<Money>.SuccessWithValue)
            Money.Create(21m)).Value;

        var record = BillingRecord.Create(
            identifier,
            "EMISOR PRUEBAS",
            "87654321B",
            "DESTINATARIO PRUEBAS",
            "Servicio de pruebas",
            total,
            tax,
            previousBillingRecordId: null,
            previousRecordHash: null,
            registerTimestamp: "2026-08-30T08:00:00+02:00");

        // Una huella de 64 hex es suficiente aquí: este test cubre solo la carrera de submit.
        record.SetComputedHash(
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");

        context.BillingRecords.Add(record);
        await context.SaveChangesAsync();

        return record.Id;
    }

    private static VeriFactuOptions CreateOptions()
        => new()
        {
            Taxpayer = new ObligadoTributarioOptions
            {
                Nif = "89890001K",
                Name = "EMISOR PRUEBAS"
            },
            SistemaInformatico = new SistemaInformaticoOptions
            {
                NombreRazon = "PRODUCTOR PRUEBAS",
                Nif = "89890001K",
                NombreSistemaInformatico = "gesFactu",
                IdSistemaInformatico = "77",
                Version = "1.0.0",
                NumeroInstalacion = "CI",
                TipoUsoPosibleSoloVerifactu = "S",
                TipoUsoPosibleMultiOT = "N",
                IndicadorMultiplesOT = "N"
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
        var serverConnection = Environment.GetEnvironmentVariable(
            "GESFACTU_TEST_POSTGRESQL");

        if (string.IsNullOrWhiteSpace(serverConnection))
            throw new InvalidOperationException(
                "GESFACTU_TEST_POSTGRESQL no está configurada.");

        var databaseName = "gesFactuSubmitCi_" + Guid.NewGuid().ToString("N");
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
