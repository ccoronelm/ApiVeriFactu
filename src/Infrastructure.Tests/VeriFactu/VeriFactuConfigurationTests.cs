using gesFactu.Infrastructure.Integrations.VeriFactu;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace gesFactu.Infrastructure.Tests.VeriFactu;

public sealed class VeriFactuConfigurationTests
{
    [Fact]
    public void AddVeriFactuClient_BlocksProductionWithoutExplicitSwitch()
    {
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["VeriFactu:Environment"] = "Production",
            ["VeriFactu:AllowProduction"] = "false",
            ["VeriFactu:ClientMode"] = "Stub"
        });

        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(
            () => services.AddVeriFactuClient(config));

        Assert.Contains("AllowProduction=true", ex.Message);
    }

    [Fact]
    public void AddVeriFactuClient_ProductionRejectsStubEvenWithExplicitSwitch()
    {
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["VeriFactu:Environment"] = "Production",
            ["VeriFactu:AllowProduction"] = "true",
            ["VeriFactu:ClientMode"] = "Stub"
        });

        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(
            () => services.AddVeriFactuClient(config));

        Assert.Contains("ClientMode=SoapClient", ex.Message);
    }

    [Fact]
    public void AddVeriFactuClient_SoapModeFailsFastWhenCertificateIsMissing()
    {
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["VeriFactu:Environment"] = "Test",
            ["VeriFactu:ClientMode"] = "SoapClient",
            ["VeriFactu:Taxpayer:Nif"] = "89890001K",
            ["VeriFactu:Taxpayer:Name"] = "EMISOR PRUEBAS",
            ["VeriFactu:SistemaInformatico:NombreRazon"] = "PRODUCTOR PRUEBAS",
            ["VeriFactu:SistemaInformatico:Nif"] = "89890001K",
            ["VeriFactu:SistemaInformatico:NombreSistemaInformatico"] = "gesFactu",
            ["VeriFactu:SistemaInformatico:IdSistemaInformatico"] = "77",
            ["VeriFactu:SistemaInformatico:Version"] = "1.0.0",
            ["VeriFactu:SistemaInformatico:NumeroInstalacion"] = "CI"
        });

        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(
            () => services.AddVeriFactuClient(config));

        Assert.Contains("Certificate:Thumbprint", ex.Message);
    }

    [Fact]
    public void AddVeriFactuClient_RejectsUnknownClientMode()
    {
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["VeriFactu:Environment"] = "Test",
            ["VeriFactu:ClientMode"] = "Whatever"
        });

        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(
            () => services.AddVeriFactuClient(config));

        Assert.Contains("ClientMode inválido", ex.Message);
    }

    private static IConfiguration BuildConfiguration(
        IDictionary<string, string?> values)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
