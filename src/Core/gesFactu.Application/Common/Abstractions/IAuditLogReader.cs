namespace gesFactu.Application.Common.Abstractions;

public sealed record AuditLogEntryDto(
    Guid Id,
    string EntityName,
    string EntityId,
    string Action,
    string Actor,
    string? CorrelationId,
    DateTime OccurredAtUtc,
    string? OldValues,
    string? NewValues);

public interface IAuditLogReader
{
    Task<IReadOnlyList<AuditLogEntryDto>> GetAsync(
        string? entityName,
        string? entityId,
        string? correlationId,
        int take,
        CancellationToken cancellationToken = default);
}
