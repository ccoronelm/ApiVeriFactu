using gesFactu.Application.Common.Abstractions;

namespace gesFactu.Infrastructure.Persistence;

internal sealed class SystemAuditContext : IAuditContext
{
    public string Actor => "system";
    public string? CorrelationId => null;
}
