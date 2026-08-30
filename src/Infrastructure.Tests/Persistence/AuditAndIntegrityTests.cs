using gesFactu.Application.Common.Abstractions;
using gesFactu.Domain.Entities;
using gesFactu.Domain.ValueObjects;
using gesFactu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace gesFactu.Infrastructure.Tests.Persistence;

public sealed class AuditAndIntegrityTests
{
    [Fact]
    public async Task CreateBillingRecord_GeneratesAuditWithActorAndCorrelation()
    {
        await using var db = CreateDbContext(
            new TestAuditContext("python:user-42", "corr-001"));

        var record = CreateRecord();
        db.BillingRecords.Add(record);

        await db.SaveChangesAsync();

        var audit = await db.AuditLogs.SingleAsync();

        Assert.Equal("BillingRecord", audit.EntityName);
        Assert.Equal(record.Id.ToString(), audit.EntityId);
        Assert.Equal("Created", audit.Action);
        Assert.Equal("python:user-42", audit.Actor);
        Assert.Equal("corr-001", audit.CorrelationId);
        Assert.Contains("FiscalInvoiceNumber", audit.NewValues);
        Assert.Equal("python:user-42", record.CreatedBy);
    }

    [Fact]
    public async Task OperationalStatusChange_IsAllowedAndAudited()
    {
        await using var db = CreateDbContext(
            new TestAuditContext("outbox-worker", "corr-status"));

        var record = CreateRecord();
        db.BillingRecords.Add(record);
        await db.SaveChangesAsync();

        db.AuditLogs.RemoveRange(db.AuditLogs);
        await db.SaveChangesAsync();

        record.Status = "Aceptado";
        await db.SaveChangesAsync();

        var audit = await db.AuditLogs
            .OrderByDescending(x => x.OccurredAtUtc)
            .FirstAsync();

        Assert.Equal("Updated", audit.Action);
        Assert.Contains("Status", audit.OldValues);
        Assert.Contains("Status", audit.NewValues);
        Assert.Equal("Aceptado", record.Status);
        Assert.Equal("outbox-worker", record.LastModifiedBy);
    }

    [Fact]
    public async Task ChangingFiscalDataAfterPersistence_IsRejected()
    {
        await using var db = CreateDbContext();

        var record = CreateRecord();
        db.BillingRecords.Add(record);
        await db.SaveChangesAsync();

        record.Description = "Descripción manipulada";

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => db.SaveChangesAsync());

        Assert.Contains("fiscalmente inmutable", ex.Message);
    }

    [Fact]
    public async Task DeletingBillingRecord_IsRejected()
    {
        await using var db = CreateDbContext();

        var record = CreateRecord();
        db.BillingRecords.Add(record);
        await db.SaveChangesAsync();

        db.BillingRecords.Remove(record);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => db.SaveChangesAsync());

        Assert.Contains("no pueden borrarse", ex.Message);
    }

    [Fact]
    public void FiscalChildren_UseRestrictDeleteBehavior()
    {
        using var db = CreateDbContext();

        var taxDetailFk = db.Model
            .FindEntityType(typeof(BillingTaxDetail))!
            .GetForeignKeys()
            .Single(x => x.PrincipalEntityType.ClrType == typeof(BillingRecord));

        var attemptFk = db.Model
            .FindEntityType(typeof(SubmissionAttempt))!
            .GetForeignKeys()
            .Single(x => x.PrincipalEntityType.ClrType == typeof(BillingRecord));

        Assert.Equal(DeleteBehavior.Restrict, taxDetailFk.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, attemptFk.DeleteBehavior);
    }

    private static ApplicationDbContext CreateDbContext(
        IAuditContext? auditContext = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"audit-integrity-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options, auditContext);
    }

    private static BillingRecord CreateRecord()
    {
        var nif = ((ValueObjectResult<TaxpayerNif>.SuccessWithValue)
            TaxpayerNif.Create("12345678A")).Value;
        var series = ((ValueObjectResult<InvoiceSeries>.SuccessWithValue)
            InvoiceSeries.Create("AUD/")).Value;
        var number = ((ValueObjectResult<InvoiceNumber>.SuccessWithValue)
            InvoiceNumber.Create(Guid.NewGuid().ToString("N")[..10])).Value;
        var id = ((ValueObjectResult<InvoiceIdentifier>.SuccessWithValue)
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
            id,
            "EMISOR TEST",
            "87654321B",
            "DESTINATARIO TEST",
            "Factura de auditoría",
            total,
            tax);

        record.SetComputedHash(
            new string('A', 64));

        record.SetTaxDetails(
        [
            BillingTaxDetail.Create(
                "01",
                "01",
                "S1",
                null,
                21m,
                100m,
                21m)
        ]);

        return record;
    }

    private sealed class TestAuditContext : IAuditContext
    {
        public TestAuditContext(
            string actor = "test",
            string? correlationId = "corr-test")
        {
            Actor = actor;
            CorrelationId = correlationId;
        }

        public string Actor { get; }
        public string? CorrelationId { get; }
    }
}
