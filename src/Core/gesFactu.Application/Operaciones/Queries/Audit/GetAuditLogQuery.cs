using gesFactu.Application.Common.Abstractions;
using MediatR;

namespace gesFactu.Application.Operaciones.Queries.Audit;

public sealed record GetAuditLogQuery(
    string? EntityName = null,
    string? EntityId = null,
    string? CorrelationId = null,
    int Take = 100)
    : IRequest<IReadOnlyList<AuditLogEntryDto>>;

public sealed class GetAuditLogQueryHandler
    : IRequestHandler<GetAuditLogQuery, IReadOnlyList<AuditLogEntryDto>>
{
    private readonly IAuditLogReader _reader;

    public GetAuditLogQueryHandler(IAuditLogReader reader)
    {
        _reader = reader;
    }

    public Task<IReadOnlyList<AuditLogEntryDto>> Handle(
        GetAuditLogQuery request,
        CancellationToken cancellationToken)
        => _reader.GetAsync(
            request.EntityName,
            request.EntityId,
            request.CorrelationId,
            Math.Clamp(request.Take, 1, 500),
            cancellationToken);
}
