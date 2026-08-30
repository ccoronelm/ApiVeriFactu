using gesFactu.Application.Common.Abstractions;
using MediatR;

namespace gesFactu.Application.Operaciones.Queries.Metrics;

public sealed record GetOperationalMetricsQuery
    : IRequest<OperationalMetricsSnapshot>;

public sealed class GetOperationalMetricsQueryHandler
    : IRequestHandler<GetOperationalMetricsQuery, OperationalMetricsSnapshot>
{
    private readonly IOperationalMetricsStore _store;

    public GetOperationalMetricsQueryHandler(IOperationalMetricsStore store)
    {
        _store = store;
    }

    public Task<OperationalMetricsSnapshot> Handle(
        GetOperationalMetricsQuery request,
        CancellationToken cancellationToken)
        => _store.GetSnapshotAsync(cancellationToken);
}
