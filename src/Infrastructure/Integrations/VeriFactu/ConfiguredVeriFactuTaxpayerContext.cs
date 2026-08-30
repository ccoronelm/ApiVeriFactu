using gesFactu.Application.Common.Abstractions;
using Microsoft.Extensions.Options;

namespace gesFactu.Infrastructure.Integrations.VeriFactu;

public sealed class ConfiguredVeriFactuTaxpayerContext : IVeriFactuTaxpayerContext
{
    private readonly VeriFactuOptions _options;

    public ConfiguredVeriFactuTaxpayerContext(IOptions<VeriFactuOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public string Nif => _options.Taxpayer.Nif;
    public string Name => _options.Taxpayer.Name;
}
