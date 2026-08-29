using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using gesFactu.Application.Common.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace gesFactu.Infrastructure.Integrations.VeriFactu;

/// <summary>
/// Extension methods para registrar cliente AEAT en contenedor DI.
/// </summary>
public static class VeriFactuServiceCollectionExtensions
{
    /// <summary>
    /// Registra la implementación configurada de IVeriFactuGateway.
    /// 
    /// Selecciona entre stub (desarrollo) o cliente SOAP real basado en configuración.
    /// </summary>
    public static IServiceCollection AddVeriFactuClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var veriFactuSection = configuration.GetSection(VeriFactuOptions.SectionName);
        var options = new VeriFactuOptions();
        veriFactuSection.Bind(options);

        services.Configure<VeriFactuOptions>(veriFactuSection);

        // Seleccionar implementación
        var clientMode = options.ClientMode?.ToLowerInvariant() ?? "stub";

        if (clientMode == "soapclient")
        {
            AddSoapClient(services, configuration, options);
        }
        else
        {
            // Por defecto, usar stub (desarrollo/testing)
            services.AddScoped<IVeriFactuGateway, VeriFactuGatewayStub>();
        }

        return services;
    }

    /// <summary>
    /// Registra el cliente SOAP real con configuración de certificados y HttpClient.
    /// </summary>
    private static void AddSoapClient(
        IServiceCollection services,
        IConfiguration configuration,
        VeriFactuOptions options)
    {
        // Registrar HttpClientFactory para VeriFactuGatewaySoapClient
        services.AddHttpClient<VeriFactuGatewaySoapClient>()
            .ConfigureHttpClient(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                client.DefaultRequestHeaders.Add("User-Agent", "gesFactu/1.0");
            })
            .ConfigurePrimaryHttpMessageHandler(() => CreateHttpHandler(options));

        // Registrar VeriFactuGatewaySoapClient como implementación de IVeriFactuGateway
        services.AddScoped<IVeriFactuGateway>(provider =>
            provider.GetRequiredService<VeriFactuGatewaySoapClient>());
    }

    /// <summary>
    /// Crea el HttpMessageHandler con configuración de certificados y validación SSL.
    /// </summary>
    private static HttpClientHandler CreateHttpHandler(VeriFactuOptions options)
    {
        var handler = new HttpClientHandler();

        // Configurar certificado cliente si está disponible
        if (!string.IsNullOrEmpty(options.CertificatePath))
        {
            try
            {
                var clientCert = new X509Certificate2(
                    options.CertificatePath,
                    options.CertificatePassword);

                handler.ClientCertificates.Add(clientCert);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to load client certificate from {options.CertificatePath}", ex);
            }
        }

        // En desarrollo/staging, permitir certificados auto-firmados
        if (options.UseStaging)
        {
            handler.ServerCertificateCustomValidationCallback = AllowStagingCertificates;
        }

        return handler;
    }

    /// <summary>
    /// Validación de certificados permisiva para entorno de staging.
    /// 
    /// ADVERTENCIA: Solo usar en desarrollo/pruebas. En producción, validar correctamente.
    /// </summary>
    private static bool AllowStagingCertificates(
        HttpRequestMessage request,
        X509Certificate2? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors)
    {
        if (sslPolicyErrors == SslPolicyErrors.None)
            return true;

        // En staging, permitir algunos errores comunes
        if (sslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
            return true;

        if (sslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateChainErrors))
            return true;

        return false;
    }
}
