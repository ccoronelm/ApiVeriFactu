using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.InMemory;
using Xunit;
using gesFactu.Domain.Entities;
using gesFactu.Domain.ValueObjects;
using gesFactu.Infrastructure.Persistence;
using gesFactu.Infrastructure.Persistence.Repositories;

namespace gesFactu.Infrastructure.Tests.Persistence;

/// <summary>
/// Tests para la persistencia del repositorio BillingRecord.
/// Usa EF Core InMemory para aislamiento rápido.
/// </summary>
public sealed class BillingRecordRepositoryTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly BillingRecordRepository _repository;

    public BillingRecordRepositoryTests()
    {
        // Configurar DbContext en memoria
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"gesFactuTestDb-{Guid.NewGuid()}")
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _repository = new BillingRecordRepository(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private static InvoiceIdentifier CreateTestInvoiceIdentifier(
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

    private static Money CreateTestMoney(decimal amount)
    {
        var moneyResult = Money.Create(amount);
        return ((ValueObjectResult<Money>.SuccessWithValue)moneyResult).Value;
    }

    [Fact]
    public async Task AddAsync_ShouldPersistBillingRecord()
    {
        // Arrange
        var invoiceId = CreateTestInvoiceIdentifier();
        var totalAmountResult = Money.Create(1000m);
        var totalAmount = ((ValueObjectResult<Money>.SuccessWithValue)totalAmountResult).Value;

        var taxAmountResult = Money.Create(210m);
        var taxAmount = ((ValueObjectResult<Money>.SuccessWithValue)taxAmountResult).Value;

        var record = BillingRecord.Create(
            invoiceId,
            issuerName: "Test Company",
            description: "Test Invoice",
            totalAmount,
            taxAmount);

        record.SetComputedHash("HASH123456789");

        // Act
        await _repository.AddAsync(record);
        await _dbContext.SaveChangesAsync();

        // Assert
        var saved = await _dbContext.BillingRecords.FirstOrDefaultAsync(r => r.IssuerNif == "12345678A");
        Assert.NotNull(saved);
        Assert.Equal("Test Company", saved.IssuerName);
        Assert.Equal("HASH123456789", saved.ComputedHash);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnBillingRecord()
    {
        // Arrange
        var invoiceId = CreateTestInvoiceIdentifier();
        var totalAmount = CreateTestMoney(500m);
        var taxAmount = CreateTestMoney(105m);

        var record = BillingRecord.Create(invoiceId, "Issuer Name", "Description", totalAmount, taxAmount);
        record.SetComputedHash("HASH999");

        await _repository.AddAsync(record);
        await _dbContext.SaveChangesAsync();

        var recordId = record.Id;

        // Act
        var retrieved = await _repository.GetByIdAsync(recordId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("Issuer Name", retrieved.IssuerName);
        Assert.Equal("HASH999", retrieved.ComputedHash);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNullForNonExistentId()
    {
        // Act
        var result = await _repository.GetByIdAsync(9999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateSubmissionStatusAsync_ShouldMarkAsSubmitted()
    {
        // Arrange
        var invoiceId = CreateTestInvoiceIdentifier();
        var record = BillingRecord.Create(
            invoiceId,
            "Company",
            "Invoice",
            CreateTestMoney(1000m),
            CreateTestMoney(210m));

        record.SetComputedHash("ABC123");
        await _repository.AddAsync(record);
        await _dbContext.SaveChangesAsync();

        var recordId = record.Id;

        // Act
        await _repository.UpdateSubmissionStatusAsync(recordId, "AEAT-SUBMISSION-ID-12345");
        await _dbContext.SaveChangesAsync();

        // Assert
        var updated = await _repository.GetByIdAsync(recordId);
        Assert.NotNull(updated);
        Assert.True(updated.IsSubmitted);
        Assert.Equal("AEAT-SUBMISSION-ID-12345", updated.AeatSubmissionId);
        Assert.Equal("Enviado", updated.Status);
    }

    [Fact]
    public async Task UpdateAeatStatusAsync_ShouldUpdateStatus()
    {
        // Arrange
        var invoiceId = CreateTestInvoiceIdentifier();
        var record = BillingRecord.Create(
            invoiceId,
            "Company",
            "Invoice",
            CreateTestMoney(2000m),
            CreateTestMoney(420m));

        record.SetComputedHash("XYZ789");
        await _repository.AddAsync(record);
        await _dbContext.SaveChangesAsync();

        var recordId = record.Id;

        // Act
        await _repository.UpdateAeatStatusAsync(recordId, "Aceptado");
        await _dbContext.SaveChangesAsync();

        // Assert
        var updated = await _repository.GetByIdAsync(recordId);
        Assert.NotNull(updated);
        Assert.Equal("Aceptado", updated.Status);
    }

    [Fact]
    public async Task ListByIssuerAsync_ShouldReturnRecordsForIssuer()
    {
        // Arrange
        var nif = "12345678A";

        // Crear varios registros para el mismo NIF
        for (int i = 0; i < 3; i++)
        {
            var invoiceId = CreateTestInvoiceIdentifier(
                nif,
                series: "A",
                number: $"{i + 1:000}");

            var record = BillingRecord.Create(
                invoiceId,
                $"Company {i}",
                $"Invoice {i}",
                CreateTestMoney(1000m),
                CreateTestMoney(210m));

            record.SetComputedHash($"HASH{i}");
            await _repository.AddAsync(record);
        }

        await _dbContext.SaveChangesAsync();

        // Act
        var records = await _repository.ListByIssuerAsync(nif, pageSize: 10, pageNumber: 1);

        // Assert
        Assert.NotEmpty(records);
        Assert.Equal(3, records.Count());
    }

    [Fact]
    public async Task ListByIssuerAsync_ShouldRespectPaging()
    {
        // Arrange
        var nif = "12345678A";

        // Crear 5 registros
        for (int i = 0; i < 5; i++)
        {
            var invoiceId = CreateTestInvoiceIdentifier(
                nif,
                series: "B",
                number: $"{i + 1:000}");

            var record = BillingRecord.Create(
                invoiceId,
                $"Company {i}",
                $"Invoice {i}",
                CreateTestMoney(1000m),
                CreateTestMoney(210m));

            record.SetComputedHash($"HASH_B{i}");
            await _repository.AddAsync(record);
        }

        await _dbContext.SaveChangesAsync();

        // Act
        var page1 = await _repository.ListByIssuerAsync(nif, pageSize: 2, pageNumber: 1);
        var page2 = await _repository.ListByIssuerAsync(nif, pageSize: 2, pageNumber: 2);

        // Assert
        Assert.Equal(2, page1.Count());
        Assert.Equal(2, page2.Count());
    }

    [Fact]
    public async Task GetPreviousRecordAsync_ShouldReturnLastRecordByDate()
    {
        // Test para la cadena de registros: obtiene el registro más reciente
        // de la misma serie del mismo contribuyente cuya fecha sea anterior.

        // Arrange
        var nif = "12345678A";
        var series = "A";
        var today = DateOnly.FromDateTime(DateTime.Now);

        // Crear un primer registro con fecha anterior
        var dateRecord1 = today.AddDays(-2);
        var record1 = BillingRecord.Create(
            CreateTestInvoiceIdentifier(nif, series, "001", dateRecord1),
            "Company",
            "Invoice 1",
            CreateTestMoney(1000m),
            CreateTestMoney(210m));
        record1.SetComputedHash("HASH1");
        record1.MarkAsSubmitted("SUB-001"); // Debe estar enviado para la cadena

        await _repository.AddAsync(record1);
        await _dbContext.SaveChangesAsync();

        // Crear un segundo registro con fecha intermedia
        var dateRecord2 = today.AddDays(-1);
        var record2 = BillingRecord.Create(
            CreateTestInvoiceIdentifier(nif, series, "002", dateRecord2),
            "Company",
            "Invoice 2",
            CreateTestMoney(2000m),
            CreateTestMoney(420m));
        record2.SetComputedHash("HASH2");
        record2.MarkAsSubmitted("SUB-002");

        await _repository.AddAsync(record2);
        await _dbContext.SaveChangesAsync();

        // Crear un tercer registro con fecha actual
        var record3 = BillingRecord.Create(
            CreateTestInvoiceIdentifier(nif, series, "003", today),
            "Company",
            "Invoice 3",
            CreateTestMoney(3000m),
            CreateTestMoney(630m));
        record3.SetComputedHash("HASH3");

        await _repository.AddAsync(record3);
        await _dbContext.SaveChangesAsync();

        // Act: Buscar el registro anterior a record3
        var previous = await _repository.GetPreviousRecordAsync(nif, series, today);

        // Assert
        // Debe retornar record2 (el más reciente antes de today)
        Assert.NotNull(previous);
        Assert.Equal("002", previous.InvoiceNumber);
        Assert.Equal("HASH2", previous.ComputedHash);
    }

    [Fact]
    public async Task GetPreviousRecordAsync_ShouldReturnNullIfNoSubmittedPrevious()
    {
        // Si no hay registros anteriores enviados, retorna null

        // Arrange
        var nif = "12345678A";
        var series = "A";
        var today = DateOnly.FromDateTime(DateTime.Now);

        // Crear un registro sin marcar como enviado
        var record1 = BillingRecord.Create(
            CreateTestInvoiceIdentifier(nif, series, "001", today.AddDays(-1)),
            "Company",
            "Invoice 1",
            CreateTestMoney(1000m),
            CreateTestMoney(210m));
        record1.SetComputedHash("HASH1");
        // No lo marcamos como enviado

        await _repository.AddAsync(record1);
        await _dbContext.SaveChangesAsync();

        // Act
        var previous = await _repository.GetPreviousRecordAsync(nif, series, today);

        // Assert
        Assert.Null(previous);
    }

    [Fact]
    public async Task GetPreviousRecordAsync_ShouldIsolateBySeries()
    {
        // Solo devuelve registros de la misma serie

        // Arrange
        var nif = "12345678A";
        var seriesA = "A";
        var seriesB = "B";
        var today = DateOnly.FromDateTime(DateTime.Now);

        // Crear registro en serie A (en el pasado)
        var recordSeriesA1 = BillingRecord.Create(
            CreateTestInvoiceIdentifier(nif, seriesA, "001", today.AddDays(-2)),
            "Company",
            "Invoice A1",
            CreateTestMoney(1000m),
            CreateTestMoney(210m));
        recordSeriesA1.SetComputedHash("HASH-A1");
        recordSeriesA1.MarkAsSubmitted("SUB-A1");

        // Crear otro registro en serie A (más reciente pero aún anterior a today)
        var recordSeriesA2 = BillingRecord.Create(
            CreateTestInvoiceIdentifier(nif, seriesA, "002", today.AddDays(-1)),
            "Company",
            "Invoice A2",
            CreateTestMoney(1000m),
            CreateTestMoney(210m));
        recordSeriesA2.SetComputedHash("HASH-A2");
        recordSeriesA2.MarkAsSubmitted("SUB-A2");

        // Crear registro en serie B (más reciente en fecha)
        var recordSeriesB = BillingRecord.Create(
            CreateTestInvoiceIdentifier(nif, seriesB, "001", today),
            "Company",
            "Invoice B",
            CreateTestMoney(2000m),
            CreateTestMoney(420m));
        recordSeriesB.SetComputedHash("HASH-B");
        recordSeriesB.MarkAsSubmitted("SUB-B");

        await _repository.AddAsync(recordSeriesA1);
        await _repository.AddAsync(recordSeriesA2);
        await _repository.AddAsync(recordSeriesB);
        await _dbContext.SaveChangesAsync();

        // Act: Buscar anterior en serie A para hoy (debe retornar A2)
        var previousA = await _repository.GetPreviousRecordAsync(nif, seriesA, today);

        // Assert
        Assert.NotNull(previousA);
        Assert.Equal("002", previousA.InvoiceNumber);
        Assert.Equal("HASH-A2", previousA.ComputedHash);

        // Act: Buscar anterior en serie B para hoy
        var previousB = await _repository.GetPreviousRecordAsync(nif, seriesB, today);

        // Assert: Debe retornar null porque no hay nada anterior en serie B
        Assert.Null(previousB);
    }

    [Fact]
    public async Task PersistencePreservesMoneyValues()
    {
        // Arrange
        var invoiceId = CreateTestInvoiceIdentifier();
        var totalAmount = CreateTestMoney(1234.56m);
        var taxAmount = CreateTestMoney(259.27m);

        var record = BillingRecord.Create(
            invoiceId,
            "Test Issuer",
            "Test Description",
            totalAmount,
            taxAmount);

        record.SetComputedHash("PRECISEHASH");

        // Act
        await _repository.AddAsync(record);
        await _dbContext.SaveChangesAsync();

        var retrieved = await _repository.GetByIdAsync(record.Id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(1234.56m, retrieved.TotalAmount);
        Assert.Equal(259.27m, retrieved.TotalTaxAmount);
    }

    [Fact]
    public async Task PersistencePreservesInvoiceIdentifier()
    {
        // Arrange
        var nif = "87654321Z";
        var series = "FAC";
        var number = "00123";
        var issueDate = new DateOnly(2025, 8, 29);

        var invoiceId = CreateTestInvoiceIdentifier(nif, series, number, issueDate);
        var record = BillingRecord.Create(
            invoiceId,
            "Test Issuer",
            "Test Description",
            CreateTestMoney(1000m),
            CreateTestMoney(210m));

        record.SetComputedHash("IDENTITYHASH");

        // Act
        await _repository.AddAsync(record);
        await _dbContext.SaveChangesAsync();

        var retrieved = await _repository.GetByIdAsync(record.Id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(nif, retrieved.IssuerNif);
        Assert.Equal(series, retrieved.InvoiceSeries);
        Assert.Equal(number, retrieved.InvoiceNumber);
        Assert.Equal(issueDate, retrieved.IssueDate);
    }
}
