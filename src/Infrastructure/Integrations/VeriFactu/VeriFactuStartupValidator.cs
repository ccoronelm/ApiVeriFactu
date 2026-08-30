using gesFactu.Infrastructure.Integrations.VeriFactu.Certificate;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace gesFactu.Infrastructure.Integrations.VeriFactu;

/// <summary>
/// Valida al arrancar que la configuración SOAP puede cargar realmente todos
/// los certificados de cliente. Evita descubrir un thumbprint/ruta inválidos
/// en la primera remisión fiscal.
/// </summary>
public sealed class VeriFactuStartupValidator : IHostedService
{
    private readonly VeriFactuOptions _options;
    private readonly CertificateLoader _certificateLoader;
    private readonly ILogger<VeriFactuStartupValidator> _logger;

    public VeriFactuStartupValidator(
        IOptions<VeriFactuOptions> options,
        CertificateLoader certificateLoader,
        ILogger<VeriFactuStartupValidator> logger)
    {
        _options = options.Value;
        _certificateLoader = certificateLoader;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_options.ClientMode != VeriFactuClientMode.Soap)
            return Task.CompletedTask;

        foreach (var taxpayer in _options.GetConfiguredTaxpayers())
        {
            using var certificate =
                _certificateLoader.Load(taxpayer.Certificate)
                ?? throw new InvalidOperationException(
                    $"No se pudo cargar el certificado del obligado {taxpayer.Nif}.");

            _logger.LogInformation(
                "Certificado mTLS validado al arrancar para obligado {TaxpayerNif}. Subject={Subject}; NotAfter={NotAfter:yyyy-MM-dd}",
                taxpayer.Nif,
                certificate.Subject,
                certificate.NotAfter);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}
