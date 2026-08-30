using gesFactu.Domain.Entities;
using gesFactu.Domain.ValueObjects;
using gesFactu.Infrastructure.Persistence;
using gesFactu.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace gesFactu.Infrastructure.Tests.Persistence;

/// <summary>
/// Tests rápidos de comportamiento del repositorio.
/// Las garantías reales de concurrencia se prueban por separado contra SQL Server.
/// </summary>
public sealed class BillingRecordRepositoryTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly BillingRecordRepository _repository;

    public BillingRecordRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"gesFactuTestDb-{Guid.NewGuid()}")
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _repository = new BillingRecordRepository(_dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    private static InvoiceIdentifier CreateInvoice(
        string nif = "12345678A",
        string series = "A",
        string number = "001",
        DateOnly? date = null)
    {
        var nifVo = ((ValueObjectResult<TaxpayerNif>.SuccessWithValue)
            TaxpayerNif.Create(nif)).Value;
        var seriesVo = ((ValueObjectResult<InvoiceSeries>.SuccessWithValue)
            InvoiceSeries.Create(series)).Value;
        var numberVo = ((ValueObjectResult<InvoiceNumber>.SuccessWithValue)
            InvoiceNumber.Create(number)).Value;

        return ((ValueObjectResult<InvoiceIdentifier>.SuccessWithValue)
            InvoiceIdentifier.Create(
                nifVo,
                seriesVo,
                numberVo,
                date ?? new DateOnly(2026, 8, 30))).Value;
    }

    private static Money Money(decimal amount)
        => ((ValueObjectResult<Money>.SuccessWithValue)
            gesFactu.Domain.ValueObjects.Money.Create(amount)).Value;

    private static BillingRecord CreateRecord(
        string nif,
        string series,
        string number,
        DateOnly? date = null)
    {
        var record = BillingRecord.Create(
            CreateInvoice(nif, series, number, date),
            "Issuer",
            "87654321B",
            "Recipient",
            $"Invoice {series}{number}",
            Money(121m),
            Money(21m));

        record.SetComputedHash(Guid.NewGuid().ToString("N").PadRight(64, 'A')[..64]);
        return record;
    }

    [Fact]
    public async Task AddAsync_PersistsFiscalIdentityAndTimestamp()
    {
        var record = CreateRecord("12345678A", "A/", "001");

        await _repository.AddAsync(record);
        await _dbContext.SaveChangesAsync();

        var saved = await _repository.GetByIdAsync(record.Id);

        Assert.NotNull(saved);
        Assert.Equal("A/001", saved.FiscalInvoiceNumber);
        Assert.False(string.IsNullOrWhiteSpace(saved.RegisterTimestamp));
    }

    [Fact]
    public async Task GetLastGeneratedRecordAsync_ReturnsImmediatePreviousAcrossSeries()
    {
        var first = CreateRecord("12345678A", "A/", "001");
        await _repository.AddAsync(first);
        await _dbContext.SaveChangesAsync();

        var second = CreateRecord("12345678A", "B/", "900");
        await _repository.AddAsync(second);
        await _dbContext.SaveChangesAsync();

        var previous = await _repository.GetLastGeneratedRecordAsync("12345678A");

        Assert.NotNull(previous);
        Assert.Equal(second.Id, previous.Id);
        Assert.Equal("B/900", previous.FiscalInvoiceNumber);
    }

    [Fact]
    public async Task GetLastGeneratedRecordAsync_DoesNotRequirePreviousSubmission()
    {
        var record = CreateRecord("12345678A", "A/", "001");
        Assert.False(record.IsSubmitted);

        await _repository.AddAsync(record);
        await _dbContext.SaveChangesAsync();

        var previous = await _repository.GetLastGeneratedRecordAsync("12345678A");

        Assert.NotNull(previous);
        Assert.Equal(record.Id, previous.Id);
    }

    [Fact]
    public async Task GetLastGeneratedRecordAsync_IsolatedByIssuer()
    {
        var a = CreateRecord("12345678A", "A/", "001");
        var b = CreateRecord("87654321B", "B/", "001");

        await _repository.AddAsync(a);
        await _repository.AddAsync(b);
        await _dbContext.SaveChangesAsync();

        Assert.Equal(
            a.Id,
            (await _repository.GetLastGeneratedRecordAsync("12345678A"))!.Id);
        Assert.Equal(
            b.Id,
            (await _repository.GetLastGeneratedRecordAsync("87654321B"))!.Id);
    }

    [Fact]
    public async Task GetByFiscalIdentityAsync_UsesAeatInvoiceIdentity()
    {
        var date = new DateOnly(2026, 8, 30);
        var record = CreateRecord("12345678A", "FAC/", "0007", date);

        await _repository.AddAsync(record);
        await _dbContext.SaveChangesAsync();

        var found = await _repository.GetByFiscalIdentityAsync(
            "12345678A",
            "FAC/0007",
            date);

        Assert.NotNull(found);
        Assert.Equal(record.Id, found.Id);
    }

    [Fact]
    public async Task ListByIssuerAsync_FiltersIssuerBeforePaging()
    {
        await _repository.AddAsync(CreateRecord("12345678A", "A/", "001"));
        await _repository.AddAsync(CreateRecord("87654321B", "B/", "001"));
        await _repository.AddAsync(CreateRecord("12345678A", "A/", "002"));
        await _dbContext.SaveChangesAsync();

        var records = (await _repository.ListByIssuerAsync(
            "12345678A",
            pageSize: 10,
            pageNumber: 1)).ToList();

        Assert.Equal(2, records.Count);
        Assert.All(records, r => Assert.Equal("12345678A", r.IssuerNif));
    }

    [Fact]
    public async Task UpdateSubmissionStatusAsync_MarksRecordSubmitted()
    {
        var record = CreateRecord("12345678A", "A/", "001");
        await _repository.AddAsync(record);
        await _dbContext.SaveChangesAsync();

        await _repository.UpdateSubmissionStatusAsync(record.Id, "LOCAL-CORRELATION");
        await _dbContext.SaveChangesAsync();

        var updated = await _repository.GetByIdAsync(record.Id);

        Assert.NotNull(updated);
        Assert.True(updated.IsSubmitted);
        Assert.Equal("LOCAL-CORRELATION", updated.AeatSubmissionId);
    }
}
