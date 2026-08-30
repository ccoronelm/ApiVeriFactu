using gesFactu.Application.Common.Abstractions;
using gesFactu.Infrastructure.Integrations.VeriFactu;
using gesFactu.Infrastructure.Integrations.VeriFactu.Validation;
using gesFactu.Infrastructure.Integrations.VeriFactu.XmlGeneration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace gesFactu.Infrastructure.Tests.VeriFactu;

public sealed class RegistroAltaXmlBuilderAdapterTests
{
    [Fact]
    public async Task BuildRegFactuXml_GeneraXmlValidoContraXsdOficial()
    {
        var options = Options.Create(new VeriFactuOptions
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
        });

        var xmlBuilder = new RegistroAltaXmlBuilderAdapter(options);

        var data = new RegistroAltaData
        {
            IssuerNif = "89890001K",
            IssuerName = "CERTIFICADO UNO TELEMATICAS",
            InvoiceSeries = "A",
            InvoiceNumber = "00000001",
            IssueDate = new DateOnly(2025, 2, 3),
            TipoFactura = "F1",
            Description = "Servicio de pruebas",
            CuotaTotal = 21.00m,
            ImporteTotal = 121.00m,
            Detalles =
            [
                new DetalleDesgloseData
                {
                    Impuesto = "01",
                    ClaveRegimen = "01",
                    CalificacionOperacion = "S1",
                    TipoImpositivo = 21m,
                    BaseImponible = 100.00m,
                    CuotaRepercutida = 21.00m
                }
            ],
            ComputedHash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
            PreviousRecordHash = null,
            PreviousIssueDate = null,
            PreviousIssuerNif = null,
            PreviousInvoiceSeries = null,
            PreviousInvoiceNumber = null,
            FechaHoraHusoGenRegistro = "2025-02-03T14:30:00+01:00"
        };

        var xml = xmlBuilder.BuildRegFactuXml(data);

        var solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var xsdPath = Path.Combine(solutionRoot, "VERIFACTU");
        var validator = new XmlSchemaValidator(NullLogger<XmlSchemaValidator>.Instance, xsdPath);

        var result = await validator.ValidateAsync(xml, VeriFactuXmlSchemaType.BillingRecord);

        Assert.True(result.IsValid, string.Join(" | ", result.Errors.Select(e => e.Message)));
    }

    [Fact]
    public async Task ValidateAsync_SiFaltaHuella_DevuelveInvalido()
    {
        var options = Options.Create(new VeriFactuOptions
        {
            Taxpayer = new ObligadoTributarioOptions { Nif = "89890001K", Name = "CERTIFICADO UNO TELEMATICAS" },
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
        });

        var xmlBuilder = new RegistroAltaXmlBuilderAdapter(options);
        var xml = xmlBuilder.BuildRegFactuXml(new RegistroAltaData
        {
            IssuerNif = "89890001K",
            IssuerName = "CERTIFICADO UNO TELEMATICAS",
            InvoiceSeries = "A",
            InvoiceNumber = "00000002",
            IssueDate = new DateOnly(2025, 2, 3),
            TipoFactura = "F1",
            Description = "Servicio de pruebas",
            CuotaTotal = 21.00m,
            ImporteTotal = 121.00m,
            Detalles =
            [
                new DetalleDesgloseData
                {
                    Impuesto = "01",
                    ClaveRegimen = "01",
                    CalificacionOperacion = "S1",
                    TipoImpositivo = 21m,
                    BaseImponible = 100.00m,
                    CuotaRepercutida = 21.00m
                }
            ],
            ComputedHash = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
            PreviousRecordHash = null,
            PreviousIssueDate = null,
            PreviousIssuerNif = null,
            PreviousInvoiceSeries = null,
            PreviousInvoiceNumber = null,
            FechaHoraHusoGenRegistro = "2025-02-03T14:30:00+01:00"
        });

        var nsSf = (System.Xml.Linq.XNamespace)"https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/SuministroInformacion.xsd";
        var doc = System.Xml.Linq.XDocument.Parse(xml);
        doc.Descendants(nsSf + "Huella").Last().Remove();

        var solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var xsdPath = Path.Combine(solutionRoot, "VERIFACTU");
        var validator = new XmlSchemaValidator(NullLogger<XmlSchemaValidator>.Instance, xsdPath);

        var result = await validator.ValidateAsync(doc.ToString(), VeriFactuXmlSchemaType.BillingRecord);

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }
}
