using gesFactu.Infrastructure.Integrations.VeriFactu;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace gesFactu.Infrastructure.Tests.VeriFactu;

public sealed class MultiTaxpayerConfigurationTests
{
    [Fact]
    public void Registry_ResuelvePorClaveYNif()
    {
        var registry = new ConfiguredVeriFactuTaxpayerRegistry(
            Options.Create(CreateOptions()));

        var byKey = registry.Resolve("empresa-a");
        var byNif = registry.ResolveByNif("87654321B");

        Assert.Equal("12345678A", byKey.Nif);
        Assert.Equal("EMPRESA B SL", byNif.Name);
    }

    [Fact]
    public void Registry_ConVariosObligados_NoPermiteDefaultImplicito()
    {
        var registry = new ConfiguredVeriFactuTaxpayerRegistry(
            Options.Create(CreateOptions()));

        var ex = Assert.Throws<InvalidOperationException>(
            registry.ResolveDefault);

        Assert.Contains("varios obligados", ex.Message);
    }

    [Fact]
    public void Options_ResuelveNumeroInstalacionPorObligado()
    {
        var options = CreateOptions();

        Assert.Equal(
            "INST-A",
            options.GetSistemaInformaticoForTaxpayer("12345678A")
                .NumeroInstalacion);

        Assert.Equal(
            "INST-B",
            options.GetSistemaInformaticoForTaxpayer("87654321B")
                .NumeroInstalacion);
    }

    [Fact]
    public void Startup_ConVariosObligadosYFlagsMultiN_FallaCerrado()
    {
        var values = new Dictionary<string, string?>
        {
            ["VeriFactu:ClientMode"] = "Stub",
            ["VeriFactu:Environment"] = "Test",
            ["VeriFactu:Taxpayers:0:Key"] = "a",
            ["VeriFactu:Taxpayers:0:Nif"] = "12345678A",
            ["VeriFactu:Taxpayers:0:Name"] = "A",
            ["VeriFactu:Taxpayers:1:Key"] = "b",
            ["VeriFactu:Taxpayers:1:Nif"] = "87654321B",
            ["VeriFactu:Taxpayers:1:Name"] = "B",
            ["VeriFactu:SistemaInformatico:TipoUsoPosibleMultiOT"] = "N",
            ["VeriFactu:SistemaInformatico:IndicadorMultiplesOT"] = "N"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(
            () => services.AddVeriFactuClient(configuration));

        Assert.Contains("TipoUsoPosibleMultiOT", ex.Message);
    }

    private static VeriFactuOptions CreateOptions()
        => new()
        {
            Taxpayers =
            [
                new VeriFactuTaxpayerProfileOptions
                {
                    Key = "empresa-a",
                    Nif = "12345678A",
                    Name = "EMPRESA A SL",
                    InstallationNumber = "INST-A",
                    Certificate = new CertificateOptions
                    {
                        Thumbprint = "AAAAAAAA"
                    }
                },
                new VeriFactuTaxpayerProfileOptions
                {
                    Key = "empresa-b",
                    Nif = "87654321B",
                    Name = "EMPRESA B SL",
                    InstallationNumber = "INST-B",
                    Certificate = new CertificateOptions
                    {
                        Thumbprint = "BBBBBBBB"
                    }
                }
            ],
            SistemaInformatico = new SistemaInformaticoOptions
            {
                NombreRazon = "PRODUCTOR SL",
                Nif = "89890001K",
                NombreSistemaInformatico = "gesFactu",
                IdSistemaInformatico = "GF",
                Version = "1.0",
                NumeroInstalacion = "DEFAULT",
                TipoUsoPosibleSoloVerifactu = "S",
                TipoUsoPosibleMultiOT = "S",
                IndicadorMultiplesOT = "S"
            }
        };
}
