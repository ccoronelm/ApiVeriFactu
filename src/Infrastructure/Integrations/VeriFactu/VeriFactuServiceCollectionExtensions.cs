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
        services.AddScoped<IVeriFactuTaxpayerRegistry, ConfiguredVeriFactuTaxpayerRegistry>();
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

        if (options.Environment == VeriFactuEntorno.Production &&
            !string.Equals(
                options.ClientMode,
                "SoapClient",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "BLOQUEO DE SEGURIDAD: Producción requiere VeriFactu:ClientMode=SoapClient.");
        }

        if (options.TimeoutSeconds <= 0)
            throw new InvalidOperationException("VeriFactu:TimeoutSeconds debe ser mayor que cero.");

        var taxpayers = options.GetConfiguredTaxpayers();

        if (taxpayers.Count > 1 &&
            (options.SistemaInformatico.TipoUsoPosibleMultiOT != "S" ||
             options.SistemaInformatico.IndicadorMultiplesOT != "S"))
        {
            throw new InvalidOperationException(
                "Con varios obligados tributarios, TipoUsoPosibleMultiOT e IndicadorMultiplesOT deben ser S.");
        }

        if (taxpayers
            .Where(x => !string.IsNullOrWhiteSpace(x.Nif))
            .GroupBy(x => x.Nif.Trim(), StringComparer.OrdinalIgnoreCase)
            .Any(x => x.Count() > 1))
        {
            throw new InvalidOperationException(
                "No puede haber NIF duplicados en VeriFactu:Taxpayers.");
        }

        if (taxpayers
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .GroupBy(x => x.Key.Trim(), StringComparer.OrdinalIgnoreCase)
            .Any(x => x.Count() > 1))
        {
            throw new InvalidOperationException(
                "No puede haber claves duplicadas en VeriFactu:Taxpayers.");
        }
    }

    private static void ValidateSoapConfiguration(VeriFactuOptions options)
    {
        var missing = new List<string>();

        var taxpayers = options.GetConfiguredTaxpayers();

        if (taxpayers.Count == 0)
            missing.Add("Taxpayer o Taxpayers");

        foreach (var taxpayer in taxpayers)
        {
            var label = string.IsNullOrWhiteSpace(taxpayer.Key)
                ? taxpayer.Nif
                : taxpayer.Key;

            if (string.IsNullOrWhiteSpace(taxpayer.Key))
                missing.Add($"Taxpayers:{label}:Key");
            if (string.IsNullOrWhiteSpace(taxpayer.Nif) || taxpayer.Nif.Trim().Length != 9)
                missing.Add($"Taxpayers:{label}:Nif");
            if (string.IsNullOrWhiteSpace(taxpayer.Name))
                missing.Add($"Taxpayers:{label}:Name");

            var hasThumbprint = !string.IsNullOrWhiteSpace(taxpayer.Certificate.Thumbprint);
            var hasPfx = !string.IsNullOrWhiteSpace(taxpayer.Certificate.PfxPath);
            if (!hasThumbprint && !hasPfx)
            {
                missing.Add(
                    options.Taxpayers.Count == 0
                        ? "Certificate:Thumbprint o Certificate:PfxPath"
                        : $"Taxpayers:{label}:Certificate");
            }
        }

        if (taxpayers
            .Where(x => !string.IsNullOrWhiteSpace(x.Nif))
            .GroupBy(x => x.Nif.Trim(), StringComparer.OrdinalIgnoreCase)
            .Any(g => g.Count() > 1))
        {
            missing.Add("Taxpayers:Nif duplicado");
        }

        if (taxpayers
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .GroupBy(x => x.Key.Trim(), StringComparer.OrdinalIgnoreCase)
            .Any(g => g.Count() > 1))
        {
            missing.Add("Taxpayers:Key duplicada");
        }

        if (taxpayers.Count > 1 &&
            (options.SistemaInformatico.TipoUsoPosibleMultiOT != "S" ||
             options.SistemaInformatico.IndicadorMultiplesOT != "S"))
        {
            missing.Add(
                "SistemaInformatico:TipoUsoPosibleMultiOT/IndicadorMultiplesOT deben ser S con varios obligados");
        }

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
        services.AddSingleton<IVeriFactuHttpClientProvider, VeriFactuHttpClientProvider>();
        services.AddHostedService<VeriFactuStartupValidator>();
        services.AddScoped<VeriFactuGatewaySoapClient>();
        services.AddScoped<IVeriFactuGateway>(sp =>
            sp.GetRequiredService<VeriFactuGatewaySoapClient>());
    }
}
