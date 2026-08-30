using gesFactu.Application.Common.Abstractions;
using MediatR;

namespace gesFactu.Application.Configuracion.Queries.ObtenerObligados;

public sealed record GetConfiguredTaxpayersQuery
    : IRequest<IReadOnlyList<VeriFactuTaxpayerIdentity>>;

public sealed class GetConfiguredTaxpayersQueryHandler
    : IRequestHandler<
        GetConfiguredTaxpayersQuery,
        IReadOnlyList<VeriFactuTaxpayerIdentity>>
{
    private readonly IVeriFactuTaxpayerRegistry _registry;

    public GetConfiguredTaxpayersQueryHandler(
        IVeriFactuTaxpayerRegistry registry)
    {
        _registry = registry;
    }

    public Task<IReadOnlyList<VeriFactuTaxpayerIdentity>> Handle(
        GetConfiguredTaxpayersQuery request,
        CancellationToken cancellationToken)
        => Task.FromResult(_registry.GetAll());
}
