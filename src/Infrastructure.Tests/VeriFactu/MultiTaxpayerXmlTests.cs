using System.Xml.Linq;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Infrastructure.Integrations.VeriFactu;
using gesFactu.Infrastructure.Integrations.VeriFactu.Validation;
using gesFactu.Infrastructure.Integrations.VeriFactu.XmlGeneration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace gesFactu.Infrastructure.Tests.VeriFactu;

public sealed class MultiTaxpayerXmlTests
{
    [Theory]
    [InlineData("12345678A", "EMPRESA A SL", "INST-A")]
    [InlineData("87654321B", "EMPRESA B SL", "INST-B")]
    public async Task RegistroAlta_ResuelveObligadoEInstalacionIndependientes(
        string nif,
        string name,
        string installation)
    {
        var builder = new RegistroAltaXmlBuilderAdapter(
            Options.Create(CreateOptions()));

        var xml = builder.BuildRegFactuXml(CreateData(nif, name));

        var ns = RegistroAltaXmlBuilder.NsSf;
        var doc = XDocument.Parse(xml);

        Assert.Equal(
            nif,
            doc.Descendants(ns + "ObligadoEmision")
                .Single()
                .Element(ns + "NIF")!
                .Value);

        Assert.Equal(
            name,
            doc.Descendants(ns + "NombreRazonEmisor")
                .Single()
                .Value);

        Assert.Equal(
            installation,
            doc.Descendants(ns + "NumeroInstalacion")
                .Single()
                .Value);

        Assert.Equal(
            "S",
            doc.Descendants(ns + "TipoUsoPosibleMultiOT")
                .Single()
                .Value);
        Assert.Equal(
            "S",
            doc.Descendants(ns + "IndicadorMultiplesOT")
                .Single()
                .Value);

        var validator = new XmlSchemaValidator(
            NullLogger<XmlSchemaValidator>.Instance,
            Path.Combine(AppContext.BaseDirectory, "VERIFACTU"));

        var validation = await validator.ValidateAsync(
            xml,
            VeriFactuXmlSchemaType.BillingRecord);

        Assert.True(
            validation.IsValid,
            string.Join(" | ", validation.Errors.Select(x => x.Message)));
    }

    private static RegistroAltaData CreateData(string nif, string name)
        => new()
        {
            IssuerNif = nif,
            IssuerName = name,
            InvoiceSeries = "A/",
            InvoiceNumber = "0001",
            IssueDate = new DateOnly(2026, 8, 30),
            RecipientNif = "B98588544",
            RecipientName = "INICIATIVAS EN ANALISIS CLINICOS SL",
            IsSubsanacion = false,
            TipoFactura = "F1",
            Description = "Prueba multiobligado",
            CuotaTotal = 21m,
            ImporteTotal = 121m,
            Detalles =
            [
                new DetalleDesgloseData
                {
                    Impuesto = "01",
                    ClaveRegimen = "01",
                    CalificacionOperacion = "S1",
                    TipoImpositivo = 21m,
                    BaseImponible = 100m,
                    CuotaRepercutida = 21m
                }
            ],
            ComputedHash =
                "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
            FechaHoraHusoGenRegistro = "2026-08-30T16:00:00+02:00"
        };

    private static VeriFactuOptions CreateOptions()
        => new()
        {
            Environment = VeriFactuEntorno.Test,
            Taxpayers =
            [
                new VeriFactuTaxpayerProfileOptions
                {
                    Key = "empresa-a",
                    Nif = "12345678A",
                    Name = "EMPRESA A SL",
                    InstallationNumber = "INST-A"
                },
                new VeriFactuTaxpayerProfileOptions
                {
                    Key = "empresa-b",
                    Nif = "87654321B",
                    Name = "EMPRESA B SL",
                    InstallationNumber = "INST-B"
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
