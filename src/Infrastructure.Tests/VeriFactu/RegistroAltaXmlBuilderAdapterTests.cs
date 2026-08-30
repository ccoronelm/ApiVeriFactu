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

    private static RegistroAltaData CreateRegistroAltaData(string invoiceNumber, string hash) => new()
    {
        IssuerNif = "89890001K",
        IssuerName = "CERTIFICADO UNO TELEMATICAS",
        InvoiceSeries = "A",
        InvoiceNumber = invoiceNumber,
        IssueDate = new DateOnly(2025, 2, 3),
        RecipientNif = "87654321B",
        RecipientName = "DESTINATARIO PRUEBAS",
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
        ComputedHash = hash,
        PreviousRecordHash = null,
        PreviousIssueDate = null,
        PreviousIssuerNif = null,
        PreviousInvoiceSeries = null,
        PreviousInvoiceNumber = null,
        FechaHoraHusoGenRegistro = "2025-02-03T14:30:00+01:00"
    };

    private static XmlSchemaValidator CreateValidator()
    {
        var xsdPath = Path.Combine(AppContext.BaseDirectory, "VERIFACTU");
        Assert.True(
            Directory.Exists(xsdPath),
            $"No existe el directorio XSD copiado al output: {xsdPath}");

        return new XmlSchemaValidator(
            NullLogger<XmlSchemaValidator>.Instance,
            xsdPath);
    }

    [Fact]
    public async Task BuildRegFactuXml_GeneraXmlValidoContraXsdOficial()
    {
        var xmlBuilder = new RegistroAltaXmlBuilderAdapter(
            Options.Create(CreateOptions()));

        var xml = xmlBuilder.BuildRegFactuXml(
            CreateRegistroAltaData(
                "00000001",
                "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"));

        var result = await CreateValidator().ValidateAsync(
            xml,
            VeriFactuXmlSchemaType.BillingRecord);

        Assert.True(
            result.IsValid,
            string.Join(" | ", result.Errors.Select(e => e.Message)));
    }

    [Fact]
    public async Task ValidateAsync_SiFaltaHuella_DevuelveInvalido()
    {
        var xmlBuilder = new RegistroAltaXmlBuilderAdapter(
            Options.Create(CreateOptions()));

        var xml = xmlBuilder.BuildRegFactuXml(
            CreateRegistroAltaData(
                "00000002",
                "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB"));

        var nsSf = (System.Xml.Linq.XNamespace)
            "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/SuministroInformacion.xsd";

        var doc = System.Xml.Linq.XDocument.Parse(xml);
        doc.Descendants(nsSf + "Huella").Last().Remove();

        var result = await CreateValidator().ValidateAsync(
            doc.ToString(),
            VeriFactuXmlSchemaType.BillingRecord);

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task ValidateAsync_SiNoExistenLosXsd_FallaCerrado()
    {
        var missingPath = Path.Combine(
            Path.GetTempPath(),
            "gesFactu-missing-xsd-" + Guid.NewGuid().ToString("N"));

        var validator = new XmlSchemaValidator(
            NullLogger<XmlSchemaValidator>.Instance,
            missingPath);

        var result = await validator.ValidateAsync(
            "<root />",
            VeriFactuXmlSchemaType.BillingRecord);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            e => e.Message.Contains("XSD oficial requerido no encontrado", StringComparison.Ordinal));
    }
}
