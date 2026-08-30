using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Infrastructure.Integrations.VeriFactu.Certificate;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace gesFactu.Infrastructure.Integrations.VeriFactu;

/// <summary>
/// Extension methods para registrar cliente AEAT en contenedor DI.
/// </summary>
public static class VeriFactuServiceCollectionExtensions
{
    /// <summary>
    /// Registra la implementación configurada de IVeriFactuGateway y servicios relacionados.
    ///
    /// Selecciona entre stub (desarrollo) o cliente SOAP real basado en VeriFactu:ClientMode.
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

        // Registrar CertificateLoader siempre (usado en modo SOAP)
        services.AddSingleton<CertificateLoader>();

        var clientMode = options.ClientMode?.ToLowerInvariant() ?? "stub";

        if (clientMode == "soapclient")
        {
            AddSoapClient(services, options);
        }
        else
        {
            services.AddScoped<IVeriFactuGateway, VeriFactuGatewayStub>();
        }

        return services;
    }

    private static void AddSoapClient(IServiceCollection services, VeriFactuOptions options)
    {
        // Cargar el certificado una sola vez (singleton-safe: el certificado es inmutable)
        services.AddSingleton<X509Certificate2?>(sp =>
        {
            var loader = sp.GetRequiredService<CertificateLoader>();
            return loader.Load(options.Certificate);
        });

        // HttpClient con handler configurado con mTLS
        services.AddHttpClient<VeriFactuGatewaySoapClient>()
            .ConfigureHttpClient(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                client.DefaultRequestHeaders.Add("User-Agent", "gesFactu/1.0");
            })
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                var cert = sp.GetService<X509Certificate2>();
                return CreateHttpHandler(cert, options);
            });

        services.AddScoped<IVeriFactuGateway>(provider =>
            provider.GetRequiredService<VeriFactuGatewaySoapClient>());
    }

    private static HttpClientHandler CreateHttpHandler(X509Certificate2? clientCert, VeriFactuOptions options)
    {
        var handler = new HttpClientHandler();

        if (clientCert != null)
            handler.ClientCertificates.Add(clientCert);

        // En entorno Test, los certificados del servidor AEAT de pruebas
        // están firmados por la misma CA que producción — no omitir validación del servidor.
        // Si hubiera problemas de cadena en Test, se puede relajar aquí con documentación explícita.

        return handler;
    }
}
