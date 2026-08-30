using System.Xml.Linq;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Infrastructure.Integrations.VeriFactu;
using gesFactu.Infrastructure.Integrations.VeriFactu.Validation;
using gesFactu.Infrastructure.Integrations.VeriFactu.XmlGeneration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace gesFactu.Infrastructure.Tests.VeriFactu;

public sealed class RegistroAnulacionXmlBuilderAdapterTests
{
    [Fact]
    public async Task BuildRegFactuXml_RegistroAnulacion_ValidaXsdOficial()
    {
        var builder = new RegistroAnulacionXmlBuilderAdapter(
            Options.Create(CreateOptions()));

        var xml = builder.BuildRegFactuXml(
            new RegistroAnulacionData
            {
                IssuerNif = "89890001K",
                IssuerName = "CERTIFICADO UNO TELEMATICAS",
                InvoiceSeries = "A/",
                InvoiceNumber = "000001",
                IssueDate = new DateOnly(2026, 8, 30),
                ComputedHash =
                    "177547C0D57AC74748561D054A9CEC14B4C4EA23D1BEFD6F2E69E3A388F90C68",
                PreviousRecordHash =
                    "F7B94CFD8924EDFF273501B01EE5153E4CE8F259766F88CF6ACB8935802A2B97",
                PreviousIssueDate = new DateOnly(2026, 8, 30),
                PreviousIssuerNif = "89890001K",
                PreviousInvoiceSeries = "A/",
                PreviousInvoiceNumber = "000001",
                FechaHoraHusoGenRegistro = "2026-08-30T14:30:00+02:00"
            });

        var ns = (XNamespace)
            "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/SuministroInformacion.xsd";

        var doc = XDocument.Parse(xml);
        var cancellation = doc.Descendants(ns + "RegistroAnulacion").Single();

        Assert.Equal(
            "89890001K",
            cancellation
                .Descendants(ns + "IDEmisorFacturaAnulada")
                .Single()
                .Value);

        Assert.Equal(
            "A/000001",
            cancellation
                .Descendants(ns + "NumSerieFacturaAnulada")
                .Single()
                .Value);

        Assert.Empty(cancellation.Descendants(ns + "SinRegistroPrevio"));
        Assert.Empty(cancellation.Descendants(ns + "RechazoPrevio"));

        var validator = new XmlSchemaValidator(
            NullLogger<XmlSchemaValidator>.Instance,
            Path.Combine(AppContext.BaseDirectory, "VERIFACTU"));

        var result = await validator.ValidateAsync(
            xml,
            VeriFactuXmlSchemaType.BillingRecord);

        Assert.True(
            result.IsValid,
            string.Join(" | ", result.Errors.Select(e => e.Message)));
    }

    private static VeriFactuOptions CreateOptions() => new()
    {
        Taxpayer = new ObligadoTributarioOptions
        {
            Nif = "89890001K",
            Name = "CERTIFICADO UNO TELEMATICAS"
        },
        SistemaInformatico = new SistemaInformaticoOptions
        {
            NombreRazon = "CERTIFICADO UNO TELEMATICAS",
            Nif = "89890001K",
            NombreSistemaInformatico = "gesFactu",
            IdSistemaInformatico = "77",
            Version = "1.0.0",
            NumeroInstalacion = "383",
            TipoUsoPosibleSoloVerifactu = "S",
            TipoUsoPosibleMultiOT = "N",
            IndicadorMultiplesOT = "N"
        }
    };
}
