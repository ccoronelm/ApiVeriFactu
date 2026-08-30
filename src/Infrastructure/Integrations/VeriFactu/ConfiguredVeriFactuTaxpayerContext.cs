using gesFactu.Application.Common.Abstractions;

namespace gesFactu.Infrastructure.Integrations.VeriFactu;

/// <summary>
/// Compatibilidad mono-obligado. En una instalación multiempresa no existe
/// un obligado implícito y esta abstracción falla de forma explícita.
/// </summary>
public sealed class ConfiguredVeriFactuTaxpayerContext
    : IVeriFactuTaxpayerContext
{
    private readonly IVeriFactuTaxpayerRegistry _registry;

    public ConfiguredVeriFactuTaxpayerContext(
        IVeriFactuTaxpayerRegistry registry)
    {
        _registry = registry;
    }

    public string Nif => _registry.ResolveDefault().Nif;
    public string Name => _registry.ResolveDefault().Name;
}
