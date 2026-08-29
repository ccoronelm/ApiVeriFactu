using Microsoft.EntityFrameworkCore;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Domain.Entities;

namespace gesFactu.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación del repositorio para BillingRecord.
/// Aísla EF Core de la lógica de aplicación.
/// </summary>
public sealed class BillingRecordRepository : IBillingRecordRepository
{
    private readonly ApplicationDbContext _dbContext;

    public BillingRecordRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(BillingRecord billingRecord, CancellationToken cancellationToken = default)
    {
        await _dbContext.BillingRecords.AddAsync(billingRecord, cancellationToken);
    }

    public async Task<BillingRecord?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.BillingRecords.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<BillingRecord?> GetPreviousRecordAsync(
        string issuerNif,
        string invoiceSeries,
        CancellationToken cancellationToken = default)
    {
        // Obtiene el registro más reciente (por fecha de creación) de una serie de un contribuyente
        return await _dbContext.BillingRecords
            .Where(r => r.CreateDate.HasValue)
            .OrderByDescending(r => r.CreateDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<BillingRecord>> ListByIssuerAsync(
        string issuerNif,
        int pageSize = 50,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var skip = (pageNumber - 1) * pageSize;

        return await _dbContext.BillingRecords
            .Skip(skip)
            .Take(pageSize)
            .OrderByDescending(r => r.CreateDate)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateSubmissionStatusAsync(
        int id,
        string aeatSubmissionId,
        CancellationToken cancellationToken = default)
    {
        var record = await _dbContext.BillingRecords.FindAsync(new object[] { id }, cancellationToken: cancellationToken);
        if (record != null)
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
        var record = await _dbContext.BillingRecords.FindAsync(new object[] { id }, cancellationToken: cancellationToken);
        if (record != null)
        {
            // TODO: Crear método específico en agregado si es necesario
            // Por ahora actualizamos el status directamente
            record.GetType().GetProperty("Status")?.SetValue(record, status);
            _dbContext.BillingRecords.Update(record);
        }
    }
}
