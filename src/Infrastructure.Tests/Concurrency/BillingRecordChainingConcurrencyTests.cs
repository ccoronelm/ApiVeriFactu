using gesFactu.Application.Common;
using gesFactu.Application.RegistrosFacturacion.Commands.CrearRegistro;
using gesFactu.Infrastructure.Persistence;
using gesFactu.Infrastructure.Persistence.Repositories;
using gesFactu.Infrastructure.VeriFactu;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace gesFactu.Infrastructure.Tests.Concurrency;

/// <summary>
/// Pruebas relacionales contra SQL Server de las dos garantías críticas:
/// - un único avance de cadena por obligado tributario;
/// - idempotencia de la identidad fiscal bajo peticiones concurrentes.
/// </summary>
public sealed class BillingRecordChainingConcurrencyTests
{
    [SqlServerFact]
    public async Task ConcurrentCreation_ProducesSingleOrderedChainAcrossSeries()
    {
        var connectionString = await CreateDatabaseAsync();

        try
        {
            var taskA = CreateRecordAsync(
                connectionString,
                new CreateBillingRecordCommand(
                    "12345678A",
                    "A/",
                    "0001",
                    "30-08-2026",
                    "Emisor pruebas",
                    "Factura A",
                    121m,
                    21m));

            var taskB = CreateRecordAsync(
                connectionString,
                new CreateBillingRecordCommand(
                    "12345678A",
                    "B/",
                    "0900",
                    "30-08-2026",
                    "Emisor pruebas",
                    "Factura B",
                    242m,
                    42m));

            var results = await Task.WhenAll(taskA, taskB);

            Assert.All(
                results,
                result => Assert.IsType<
                    Result<CreateBillingRecordResponse>.SuccessWithValue>(result));

            await using var verify = CreateContext(connectionString);
            var records = await verify.BillingRecords
                .OrderBy(r => r.Id)
                .ToListAsync();

            Assert.Equal(2, records.Count);

            var first = records[0];
            var second = records[1];

            Assert.Null(first.PreviousBillingRecordId);
            Assert.Null(first.PreviousRecordHash);

            Assert.Equal(first.Id, second.PreviousBillingRecordId);
            Assert.Equal(first.ComputedHash, second.PreviousRecordHash);

            // La segunda factura puede pertenecer a otra serie y sigue la misma cadena del SIF.
            Assert.NotEqual(first.InvoiceSeries, second.InvoiceSeries);
        }
        finally
        {
            await DeleteDatabaseAsync(connectionString);
        }
    }

    [SqlServerFact]
    public async Task ConcurrentDuplicateFiscalIdentity_CreatesOnlyOneRecord()
    {
        var connectionString = await CreateDatabaseAsync();

        try
        {
            var command = new CreateBillingRecordCommand(
                "12345678A",
                "F/",
                "0042",
                "30-08-2026",
                "Emisor pruebas",
                "Factura duplicada",
                121m,
                21m);

            var results = await Task.WhenAll(
                CreateRecordAsync(connectionString, command),
                CreateRecordAsync(connectionString, command));

            Assert.Equal(
                1,
                results.Count(r =>
                    r is Result<CreateBillingRecordResponse>.SuccessWithValue));

            Assert.Equal(
                1,
                results.Count(r =>
                    r is Result<CreateBillingRecordResponse>.IdempotencyConflictError));

            await using var verify = CreateContext(connectionString);
            Assert.Equal(1, await verify.BillingRecords.CountAsync());
        }
        finally
        {
            await DeleteDatabaseAsync(connectionString);
        }
    }

    private static async Task<Result<CreateBillingRecordResponse>> CreateRecordAsync(
        string connectionString,
        CreateBillingRecordCommand command)
    {
        await using var context = CreateContext(connectionString);
        var repository = new BillingRecordRepository(context);
        var handler = new CreateBillingRecordCommandHandler(
            context,
            repository,
            new Sha256HashCalculator(),
            NullLogger<CreateBillingRecordCommandHandler>.Instance);

        return await handler.Handle(command, CancellationToken.None);
    }

    private static ApplicationDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task<string> CreateDatabaseAsync()
    {
        var serverConnection = Environment.GetEnvironmentVariable(
            "GESFACTU_TEST_SQLSERVER");

        if (string.IsNullOrWhiteSpace(serverConnection))
            throw new InvalidOperationException(
                "GESFACTU_TEST_SQLSERVER no está configurada.");

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
            "SQL Server de pruebas no estuvo disponible a tiempo.",
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
            // La limpieza no debe ocultar el resultado principal del test.
        }
    }
}

/// <summary>
/// En local, estos tests se omiten si no se ha configurado una instancia SQL Server.
/// En CI la variable se configura y las pruebas son obligatorias.
/// </summary>
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("GESFACTU_TEST_SQLSERVER")))
        {
            Skip = "Requiere GESFACTU_TEST_SQLSERVER.";
        }
    }
}
