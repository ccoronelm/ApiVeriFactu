using gesFactu.Infrastructure.Integrations.VeriFactu;
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
