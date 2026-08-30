using gesFactu.Application.Common.Abstractions;
using Microsoft.Extensions.Options;

namespace gesFactu.Infrastructure.Integrations.VeriFactu;

public sealed class ConfiguredVeriFactuSystemContext : IVeriFactuSystemContext
{
    private readonly VeriFactuOptions _options;

    public ConfiguredVeriFactuSystemContext(IOptions<VeriFactuOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public string ProducerName => _options.SistemaInformatico.NombreRazon;
    public string ProducerNif => _options.SistemaInformatico.Nif;
    public string SystemName => _options.SistemaInformatico.NombreSistemaInformatico;
    public string SystemId => _options.SistemaInformatico.IdSistemaInformatico;
    public string Version => _options.SistemaInformatico.Version;
    public string InstallationNumber => _options.SistemaInformatico.NumeroInstalacion;

    public string GetInstallationNumber(string taxpayerNif)
        => _options.GetSistemaInformaticoForTaxpayer(taxpayerNif)
            .NumeroInstalacion;
}
