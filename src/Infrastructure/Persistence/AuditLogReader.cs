using gesFactu.Application.Common.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace gesFactu.Infrastructure.Persistence;

public sealed class AuditLogReader : IAuditLogReader
{
    private readonly ApplicationDbContext _dbContext;

    public AuditLogReader(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<AuditLogEntryDto>> GetAsync(
        string? entityName,
        string? entityId,
        string? correlationId,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(entityName))
            query = query.Where(x => x.EntityName == entityName.Trim());

        if (!string.IsNullOrWhiteSpace(entityId))
            query = query.Where(x => x.EntityId == entityId.Trim());

        if (!string.IsNullOrWhiteSpace(correlationId))
            query = query.Where(x => x.CorrelationId == correlationId.Trim());

        return await query
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(Math.Clamp(take, 1, 500))
            .Select(x => new AuditLogEntryDto(
                x.Id,
                x.EntityName,
                x.EntityId,
                x.Action,
                x.Actor,
                x.CorrelationId,
                x.OccurredAtUtc,
                x.OldValues,
                x.NewValues))
            .ToListAsync(cancellationToken);
    }
}
