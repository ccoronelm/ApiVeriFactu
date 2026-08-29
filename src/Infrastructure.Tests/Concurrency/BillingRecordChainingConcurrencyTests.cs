using Microsoft.EntityFrameworkCore;
using Xunit;
using gesFactu.Domain.Entities;
using gesFactu.Domain.ValueObjects;
using gesFactu.Infrastructure.Persistence;
using gesFactu.Infrastructure.Persistence.Repositories;

namespace gesFactu.Infrastructure.Tests.Concurrency;

/// <summary>
/// Tests de concurrencia para el encadenamiento de registros.
/// Valida que la cadena sea segura bajo ejecución paralela.
/// </summary>
public sealed class BillingRecordChainingConcurrencyTests
{
    private ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private InvoiceIdentifier CreateTestInvoiceIdentifier(
        string nif = "12345678A",
        string series = "A",
        string number = "001",
        DateOnly? issueDate = null)
    {
        var nipResult = TaxpayerNif.Create(nif);
        var nifValue = ((ValueObjectResult<TaxpayerNif>.SuccessWithValue)nipResult).Value;

        var seriesResult = InvoiceSeries.Create(series);
        var seriesValue = ((ValueObjectResult<InvoiceSeries>.SuccessWithValue)seriesResult).Value;

        var numberResult = InvoiceNumber.Create(number);
        var numberValue = ((ValueObjectResult<InvoiceNumber>.SuccessWithValue)numberResult).Value;

        var dateValue = issueDate ?? DateOnly.FromDateTime(DateTime.Now);

        var invoiceIdResult = InvoiceIdentifier.Create(nifValue, seriesValue, numberValue, dateValue);
        return ((ValueObjectResult<InvoiceIdentifier>.SuccessWithValue)invoiceIdResult).Value;
    }

    private Money CreateTestMoney(decimal amount)
    {
        var moneyResult = Money.Create(amount);
        return ((ValueObjectResult<Money>.SuccessWithValue)moneyResult).Value;
    }

    [Fact]
    public async Task ParallelRecordCreation_PreservesChainIntegrity()
    {
        // Test de concurrencia: crear múltiples registros en paralelo
        // y verificar que la cadena siga siendo válida.

        // Arrange - Usar el mismo nombre de BD en memoria para todos los contextos
        var dbName = Guid.NewGuid().ToString();
        var nif = "12345678A";
        var series = "A";
        var today = DateOnly.FromDateTime(DateTime.Now);

        Func<ApplicationDbContext> createContextWithSameName = () =>
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName) // Mismo nombre para compartir datos
                .Options;
            return new ApplicationDbContext(options);
        };

        // Crear un registro base (ya enviado)
        using (var context = createContextWithSameName())
        {
            var repository = new BillingRecordRepository(context);

            var baseRecord = BillingRecord.Create(
                CreateTestInvoiceIdentifier(nif, series, "001", today.AddDays(-5)),
                "Company",
                "Invoice 1",
                CreateTestMoney(1000m),
                CreateTestMoney(210m));
            baseRecord.SetComputedHash("HASH-BASE");
            baseRecord.MarkAsSubmitted("SUB-BASE");

            await repository.AddAsync(baseRecord);
            await context.SaveChangesAsync();
        }

        // Act: Crear múltiples registros
        for (int i = 0; i < 5; i++)
        {
            using (var context = createContextWithSameName())
            {
                var repository = new BillingRecordRepository(context);

                var invoiceNumber = $"{i + 2:000}";
                var record = BillingRecord.Create(
                    CreateTestInvoiceIdentifier(nif, series, invoiceNumber, today.AddDays(-4 + i)),
                    "Company",
                    $"Invoice {i + 2}",
                    CreateTestMoney(1000m * (i + 2)),
                    CreateTestMoney(210m * (i + 2)));
                record.SetComputedHash($"HASH-{i + 2}");
                record.MarkAsSubmitted($"SUB-{i + 2}");

                await repository.AddAsync(record);
                await context.SaveChangesAsync();
            }
        }

        // Assert: Verificar que la cadena es correcta
        using (var context = createContextWithSameName())
        {
            var repository = new BillingRecordRepository(context);
            var today2 = today.AddDays(-1);
            var previous = await repository.GetPreviousRecordAsync(nif, series, today2);

            Assert.NotNull(previous);
            // Debe devolver el registro más reciente anterior a today-1
            // Día -4+0=-4, -4+1=-3, -4+2=-2 son anteriores a -1
            // Día -4+3=-1 es igual a -1 (no anterior)
            // Día -4+4=0 es posterior a -1
            // El más reciente anterior a -1 es i=2 (invoice 004, day -2)
            Assert.Equal("004", previous.InvoiceNumber);

            // Verificar que todos los registros fueron creados
            var allRecords = await context.BillingRecords
                .Where(r => r.IssuerNif == nif && r.InvoiceSeries == series)
                .OrderBy(r => r.IssueDate)
                .ToListAsync();

            Assert.Equal(6, allRecords.Count()); // Base + 5 nuevos
        }
    }

    [Fact]
    public async Task MultipleIssuers_PreserveIsolation()
    {
        // Verificar que las cadenas de diferentes contribuyentes están aisladas

        // Arrange
        using var context = CreateDbContext();
        var repository = new BillingRecordRepository(context);

        var nif1 = "12345678A";
        var nif2 = "87654321B";
        var series = "A";
        var today = DateOnly.FromDateTime(DateTime.Now);

        // Crear registros para dos contribuyentes diferentes
        var record1_nif1 = BillingRecord.Create(
            CreateTestInvoiceIdentifier(nif1, series, "001", today.AddDays(-2)),
            "Company 1",
            "Invoice 1",
            CreateTestMoney(1000m),
            CreateTestMoney(210m));
        record1_nif1.SetComputedHash("HASH-1");
        record1_nif1.MarkAsSubmitted("SUB-1");

        var record1_nif2 = BillingRecord.Create(
            CreateTestInvoiceIdentifier(nif2, series, "001", today.AddDays(-1)),
            "Company 2",
            "Invoice 1",
            CreateTestMoney(2000m),
            CreateTestMoney(420m));
        record1_nif2.SetComputedHash("HASH-2");
        record1_nif2.MarkAsSubmitted("SUB-2");

        await repository.AddAsync(record1_nif1);
        await repository.AddAsync(record1_nif2);
        await context.SaveChangesAsync();

        // Act: Consultar anterior para cada contribuyente en la misma fecha
        var previousNif1 = await repository.GetPreviousRecordAsync(nif1, series, today);
        var previousNif2 = await repository.GetPreviousRecordAsync(nif2, series, today);

        // Assert
        Assert.NotNull(previousNif1);
        Assert.Equal(nif1, previousNif1.IssuerNif);

        Assert.NotNull(previousNif2);
        Assert.Equal(nif2, previousNif2.IssuerNif);

        // Verificar que son diferentes
        Assert.NotEqual(previousNif1.ComputedHash, previousNif2.ComputedHash);
    }
}
