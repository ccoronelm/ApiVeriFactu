namespace gesFactu.Application.Common.Abstractions;

public interface IAuditContext
{
    string Actor { get; }
    string? CorrelationId { get; }
}
