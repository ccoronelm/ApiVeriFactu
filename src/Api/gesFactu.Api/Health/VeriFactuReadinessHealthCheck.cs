using gesFactu.Infrastructure.Integrations.VeriFactu;
using gesFactu.Infrastructure.Integrations.VeriFactu.Certificate;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace gesFactu.Api.Health;

public sealed class VeriFactuReadinessHealthCheck : IHealthCheck
{
    private readonly VeriFactuOptions _options;
    private readonly CertificateLoader _certificateLoader;

    public VeriFactuReadinessHealthCheck(
        IOptions<VeriFactuOptions> options,
        CertificateLoader certificateLoader)
    {
        _options = options.Value;
        _certificateLoader = certificateLoader;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (_options.Environment == VeriFactuEntorno.Production &&
                !_options.AllowProduction)
            {
                return Task.FromResult(
                    HealthCheckResult.Unhealthy(
                        "Producción VERI*FACTU está bloqueada."));
            }

            if (_options.Environment == VeriFactuEntorno.Production &&
                _options.ClientMode != VeriFactuClientMode.Soap)
            {
                return Task.FromResult(
                    HealthCheckResult.Unhealthy(
                        "Producción requiere VeriFactu:ClientMode=Soap."));
            }

            var taxpayers = _options.GetConfiguredTaxpayers();
            if (taxpayers.Count == 0)
            {
                return Task.FromResult(
                    HealthCheckResult.Unhealthy(
                        "No hay obligados tributarios configurados."));
            }

            if (_options.ClientMode == VeriFactuClientMode.Soap)
            {
                foreach (var taxpayer in taxpayers)
                {
                    using var certificate =
                        _certificateLoader.Load(taxpayer.Certificate);

                    if (certificate is null)
                    {
                        return Task.FromResult(
                            HealthCheckResult.Unhealthy(
                                $"No se pudo cargar el certificado de {taxpayer.Nif}."));
                    }
                }
            }

            return Task.FromResult(
                HealthCheckResult.Healthy(
                    $"VERI*FACTU {_options.Environment}; {taxpayers.Count} obligado(s)."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(
                HealthCheckResult.Unhealthy(
                    "Configuración/certificado VERI*FACTU no preparado.",
                    ex));
        }
    }
}
