using System.Security.Cryptography.X509Certificates;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Infrastructure.Integrations.VeriFactu.Certificate;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace gesFactu.Infrastructure.Integrations.VeriFactu;

/// <summary>
/// Registro y validación fail-fast del cliente AEAT.
/// </summary>
public static class VeriFactuServiceCollectionExtensions
{
    public static IServiceCollection AddVeriFactuClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(VeriFactuOptions.SectionName);
        var options = new VeriFactuOptions();
        section.Bind(options);

        ValidateSafety(options);

        services.Configure<VeriFactuOptions>(section);
        services.AddSingleton<CertificateLoader>();
        services.AddScoped<IVeriFactuTaxpayerContext, ConfiguredVeriFactuTaxpayerContext>();
        services.AddScoped<IVeriFactuSystemContext, ConfiguredVeriFactuSystemContext>();

        var mode = options.ClientMode?.Trim().ToLowerInvariant();

        switch (mode)
        {
            case "stub":
                services.AddScoped<IVeriFactuGateway, VeriFactuGatewayStub>();
                break;

            case "soapclient":
                ValidateSoapConfiguration(options);
                AddSoapClient(services, options);
                break;

            default:
                throw new InvalidOperationException(
                    $"VeriFactu:ClientMode inválido: '{options.ClientMode}'. " +
                    "Valores permitidos: Stub, SoapClient.");
        }

        return services;
    }

    private static void ValidateSafety(VeriFactuOptions options)
    {
        if (options.Environment == VeriFactuEntorno.Production &&
            !options.AllowProduction)
        {
            throw new InvalidOperationException(
                "BLOQUEO DE SEGURIDAD: VeriFactu:Environment=Production requiere " +
                "VeriFactu:AllowProduction=true de forma explícita.");
        }

        if (options.TimeoutSeconds <= 0)
            throw new InvalidOperationException("VeriFactu:TimeoutSeconds debe ser mayor que cero.");
    }

    private static void ValidateSoapConfiguration(VeriFactuOptions options)
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Taxpayer.Nif))
            missing.Add("Taxpayer:Nif");
        if (string.IsNullOrWhiteSpace(options.Taxpayer.Name))
            missing.Add("Taxpayer:Name");

        if (string.IsNullOrWhiteSpace(options.SistemaInformatico.NombreRazon))
            missing.Add("SistemaInformatico:NombreRazon");
        if (string.IsNullOrWhiteSpace(options.SistemaInformatico.Nif))
            missing.Add("SistemaInformatico:Nif");
        if (string.IsNullOrWhiteSpace(options.SistemaInformatico.NombreSistemaInformatico))
            missing.Add("SistemaInformatico:NombreSistemaInformatico");
        if (string.IsNullOrWhiteSpace(options.SistemaInformatico.IdSistemaInformatico))
            missing.Add("SistemaInformatico:IdSistemaInformatico");
        if (string.IsNullOrWhiteSpace(options.SistemaInformatico.Version))
            missing.Add("SistemaInformatico:Version");
        if (string.IsNullOrWhiteSpace(options.SistemaInformatico.NumeroInstalacion))
            missing.Add("SistemaInformatico:NumeroInstalacion");

        var hasThumbprint = !string.IsNullOrWhiteSpace(options.Certificate.Thumbprint);
        var hasPfx = !string.IsNullOrWhiteSpace(options.Certificate.PfxPath);

        if (!hasThumbprint && !hasPfx)
            missing.Add("Certificate:Thumbprint o Certificate:PfxPath");

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                "Configuración VERI*FACTU SOAP incompleta: " +
                string.Join(", ", missing));
        }
    }

    private static void AddSoapClient(
        IServiceCollection services,
        VeriFactuOptions options)
    {
        services.AddSingleton<X509Certificate2>(sp =>
        {
            var loader = sp.GetRequiredService<CertificateLoader>();
            return loader.Load(options.Certificate)
                ?? throw new InvalidOperationException(
                    "SoapClient requiere un certificado cliente X.509 válido.");
        });

        services.AddHttpClient<VeriFactuGatewaySoapClient>()
            .ConfigureHttpClient(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("gesFactu/1.0");
            })
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                var cert = sp.GetRequiredService<X509Certificate2>();
                return CreateHttpHandler(cert);
            });

        services.AddScoped<IVeriFactuGateway>(sp =>
            sp.GetRequiredService<VeriFactuGatewaySoapClient>());
    }

    private static HttpClientHandler CreateHttpHandler(X509Certificate2 clientCert)
    {
        var handler = new HttpClientHandler();
        handler.ClientCertificates.Add(clientCert);

        // Nunca se desactiva la validación TLS del servidor AEAT.
        return handler;
    }
}
