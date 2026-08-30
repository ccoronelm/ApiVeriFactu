using gesFactu.Application.Common.Abstractions;
using gesFactu.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace gesFactu.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repositorio EF Core de BillingRecord.
/// </summary>
public sealed class BillingRecordRepository : IBillingRecordRepository
{
    private readonly ApplicationDbContext _dbContext;

    public BillingRecordRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(
        BillingRecord billingRecord,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.BillingRecords.AddAsync(
            billingRecord,
            cancellationToken);
    }

    public Task<BillingRecord?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
        => _dbContext.BillingRecords
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<BillingRecord?> GetLastGeneratedRecordAsync(
        string issuerNif,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuerNif);

        // La cadena VERI*FACTU sigue el orden de generación de RF del SIF,
        // no la serie, la fecha de expedición ni el estado de remisión.
        // Dentro de la transacción SERIALIZABLE, el índice (IssuerNif, Id)
        // permite que SQL Server proteja el rango consultado.
        return _dbContext.BillingRecords
            .Where(r => r.IssuerNif == issuerNif)
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<BillingRecord?> GetByFiscalIdentityAsync(
        string issuerNif,
        string fiscalInvoiceNumber,
        DateOnly issueDate,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuerNif);
        ArgumentException.ThrowIfNullOrWhiteSpace(fiscalInvoiceNumber);

        return _dbContext.BillingRecords
            .FirstOrDefaultAsync(
                r => r.IssuerNif == issuerNif
                     && r.FiscalInvoiceNumber == fiscalInvoiceNumber
                     && r.IssueDate == issueDate
                     && r.SubsanatesBillingRecordId == null,
                cancellationToken);
    }

    public Task<BillingRecord?> GetPendingSubsanationForSourceAsync(
        int sourceBillingRecordId,
        CancellationToken cancellationToken = default)
        => _dbContext.BillingRecords
            .Where(r =>
                r.SubsanatesBillingRecordId == sourceBillingRecordId &&
                (r.Status == "Pendiente" ||
                 r.Status == "PendienteEnvio" ||
                 r.Status == "Enviado"))
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IEnumerable<BillingRecord>> ListByIssuerAsync(
        string issuerNif,
        int pageSize = 50,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var skip = (pageNumber - 1) * pageSize;

        return await _dbContext.BillingRecords
            .Where(r => r.IssuerNif == issuerNif)
            .OrderByDescending(r => r.Id)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateSubmissionStatusAsync(
        int id,
        string aeatSubmissionId,
        CancellationToken cancellationToken = default)
    {
        var record = await _dbContext.BillingRecords.FindAsync(
            new object[] { id },
            cancellationToken: cancellationToken);

        if (record is not null)
        {
            record.MarkAsSubmitted(aeatSubmissionId);
            _dbContext.BillingRecords.Update(record);
        }
    }

    public async Task UpdateAeatStatusAsync(
        int id,
        string status,
        CancellationToken cancellationToken = default)
    {
        var record = await _dbContext.BillingRecords.FindAsync(
            new object[] { id },
            cancellationToken: cancellationToken);

        if (record is not null)
        {
            record.Status = status;
            _dbContext.BillingRecords.Update(record);
        }
    }
}
