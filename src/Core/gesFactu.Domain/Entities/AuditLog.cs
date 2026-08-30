namespace gesFactu.Domain.Entities;

/// <summary>
/// Registro append-only de cambios persistentes en entidades de negocio.
/// </summary>
public sealed class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
}
