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

    private static RegistroAltaData CreateRegistroAltaData(
        string invoiceNumber,
        string hash,
        bool isSubsanacion = false) => new()
    {
        IssuerNif = "89890001K",
        IssuerName = "CERTIFICADO UNO TELEMATICAS",
        InvoiceSeries = "A",
        InvoiceNumber = invoiceNumber,
        IssueDate = new DateOnly(2025, 2, 3),
        RecipientNif = "87654321B",
        RecipientName = "DESTINATARIO PRUEBAS",
        IsSubsanacion = isSubsanacion,
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
    public void BuildRegFactuXml_F1_IncluyeDestinatario()
    {
        var xmlBuilder = new RegistroAltaXmlBuilderAdapter(
            Options.Create(CreateOptions()));

        var xml = xmlBuilder.BuildRegFactuXml(
            CreateRegistroAltaData(
                "00000003",
                "CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC"));

        var nsSf = (System.Xml.Linq.XNamespace)
            "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/SuministroInformacion.xsd";

        var doc = System.Xml.Linq.XDocument.Parse(xml);
        var destinatario = doc.Descendants(nsSf + "IDDestinatario").Single();

        Assert.Equal(
            "DESTINATARIO PRUEBAS",
            destinatario.Element(nsSf + "NombreRazon")?.Value);
        Assert.Equal(
            "87654321B",
            destinatario.Element(nsSf + "NIF")?.Value);
    }

    [Fact]
    public async Task BuildRegFactuXml_F2_SinDestinatario_ValidaXsdOficial()
    {
        var xmlBuilder = new RegistroAltaXmlBuilderAdapter(
            Options.Create(CreateOptions()));

        var data = CreateRegistroAltaData(
            "000000F2",
            "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE");

        data = new RegistroAltaData
        {
            IssuerNif = data.IssuerNif,
            IssuerName = data.IssuerName,
            InvoiceSeries = data.InvoiceSeries,
            InvoiceNumber = data.InvoiceNumber,
            IssueDate = data.IssueDate,
            RecipientNif = string.Empty,
            RecipientName = string.Empty,
            IsSubsanacion = false,
            TipoFactura = "F2",
            Description = data.Description,
            CuotaTotal = data.CuotaTotal,
            ImporteTotal = data.ImporteTotal,
            Detalles = data.Detalles,
            ComputedHash = data.ComputedHash,
            PreviousRecordHash = data.PreviousRecordHash,
            PreviousIssueDate = data.PreviousIssueDate,
            PreviousIssuerNif = data.PreviousIssuerNif,
            PreviousInvoiceSeries = data.PreviousInvoiceSeries,
            PreviousInvoiceNumber = data.PreviousInvoiceNumber,
            FechaHoraHusoGenRegistro = data.FechaHoraHusoGenRegistro
        };

        var xml = xmlBuilder.BuildRegFactuXml(data);

        var nsSf = (System.Xml.Linq.XNamespace)
            "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/SuministroInformacion.xsd";

        var doc = System.Xml.Linq.XDocument.Parse(xml);

        Assert.Equal(
            "F2",
            doc.Descendants(nsSf + "TipoFactura").Single().Value);
        Assert.Empty(doc.Descendants(nsSf + "Destinatarios"));

        var result = await CreateValidator().ValidateAsync(
            xml,
            VeriFactuXmlSchemaType.BillingRecord);

        Assert.True(
            result.IsValid,
            string.Join(" | ", result.Errors.Select(e => e.Message)));
    }

    [Fact]
    public async Task BuildRegFactuXml_Subsanacion_IncluyeIndicadorYValidaXsd()
    {
        var xmlBuilder = new RegistroAltaXmlBuilderAdapter(
            Options.Create(CreateOptions()));

        var xml = xmlBuilder.BuildRegFactuXml(
            CreateRegistroAltaData(
                "00000004",
                "DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD",
                isSubsanacion: true));

        var nsSf = (System.Xml.Linq.XNamespace)
            "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/SuministroInformacion.xsd";

        var doc = System.Xml.Linq.XDocument.Parse(xml);

        Assert.Equal(
            "S",
            doc.Descendants(nsSf + "Subsanacion").Single().Value);
        Assert.Empty(doc.Descendants(nsSf + "RechazoPrevio"));

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
    [Fact]
    public async Task BuildRegFactuXml_R1S_IncluyeRectificacionYValidaXsd()
    {
        var xmlBuilder = new RegistroAltaXmlBuilderAdapter(
            Options.Create(CreateOptions()));

        var baseData = CreateRegistroAltaData(
            "00000R1S",
            "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF");

        var data = new RegistroAltaData
        {
            IssuerNif = baseData.IssuerNif,
            IssuerName = baseData.IssuerName,
            InvoiceSeries = "R/",
            InvoiceNumber = "00000R1S",
            IssueDate = baseData.IssueDate,
            RecipientNif = baseData.RecipientNif,
            RecipientName = baseData.RecipientName,
            IsSubsanacion = false,
            TipoFactura = "R1",
            TipoRectificativa = "S",
            FacturasRectificadas =
            [
                new FacturaRectificadaData
                {
                    IssuerNif = baseData.IssuerNif,
                    InvoiceSeries = "A",
                    InvoiceNumber = "00000001",
                    IssueDate = baseData.IssueDate
                }
            ],
            ImporteRectificacion = new ImporteRectificacionData
            {
                BaseRectificada = 100.00m,
                CuotaRectificada = 21.00m
            },
            Description = "Rectificación sustitutiva",
            CuotaTotal = 16.80m,
            ImporteTotal = 96.80m,
            Detalles =
            [
                new DetalleDesgloseData
                {
                    Impuesto = "01",
                    ClaveRegimen = "01",
                    CalificacionOperacion = "S1",
                    TipoImpositivo = 21m,
                    BaseImponible = 80.00m,
                    CuotaRepercutida = 16.80m
                }
            ],
            ComputedHash = baseData.ComputedHash,
            PreviousRecordHash = null,
            PreviousIssueDate = null,
            PreviousIssuerNif = null,
            PreviousInvoiceSeries = null,
            PreviousInvoiceNumber = null,
            FechaHoraHusoGenRegistro = baseData.FechaHoraHusoGenRegistro
        };

        var xml = xmlBuilder.BuildRegFactuXml(data);
        var nsSf = (System.Xml.Linq.XNamespace)
            "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/SuministroInformacion.xsd";
        var doc = System.Xml.Linq.XDocument.Parse(xml);

        Assert.Equal("R1", doc.Descendants(nsSf + "TipoFactura").Single().Value);
        Assert.Equal("S", doc.Descendants(nsSf + "TipoRectificativa").Single().Value);
        Assert.Single(doc.Descendants(nsSf + "IDFacturaRectificada"));
        Assert.Equal("100.00", doc.Descendants(nsSf + "BaseRectificada").Single().Value);
        Assert.Equal("21.00", doc.Descendants(nsSf + "CuotaRectificada").Single().Value);

        var result = await CreateValidator().ValidateAsync(
            xml,
            VeriFactuXmlSchemaType.BillingRecord);

        Assert.True(
            result.IsValid,
            string.Join(" | ", result.Errors.Select(e => e.Message)));
    }

    [Fact]
    public async Task BuildRegFactuXml_R4I_AdmiteImportesNegativosYOmitirImporteRectificacion()
    {
        var xmlBuilder = new RegistroAltaXmlBuilderAdapter(
            Options.Create(CreateOptions()));

        var baseData = CreateRegistroAltaData(
            "00000R4I",
            "9999999999999999999999999999999999999999999999999999999999999999");

        var data = new RegistroAltaData
        {
            IssuerNif = baseData.IssuerNif,
            IssuerName = baseData.IssuerName,
            InvoiceSeries = "R/",
            InvoiceNumber = "00000R4I",
            IssueDate = baseData.IssueDate,
            RecipientNif = baseData.RecipientNif,
            RecipientName = baseData.RecipientName,
            IsSubsanacion = false,
            TipoFactura = "R4",
            TipoRectificativa = "I",
            FacturasRectificadas = Array.Empty<FacturaRectificadaData>(),
            ImporteRectificacion = null,
            Description = "Rectificación por diferencias",
            CuotaTotal = -4.20m,
            ImporteTotal = -24.20m,
            Detalles =
            [
                new DetalleDesgloseData
                {
                    Impuesto = "01",
                    ClaveRegimen = "01",
                    CalificacionOperacion = "S1",
                    TipoImpositivo = 21m,
                    BaseImponible = -20.00m,
                    CuotaRepercutida = -4.20m
                }
            ],
            ComputedHash = baseData.ComputedHash,
            PreviousRecordHash = null,
            PreviousIssueDate = null,
            PreviousIssuerNif = null,
            PreviousInvoiceSeries = null,
            PreviousInvoiceNumber = null,
            FechaHoraHusoGenRegistro = baseData.FechaHoraHusoGenRegistro
        };

        var xml = xmlBuilder.BuildRegFactuXml(data);
        var nsSf = (System.Xml.Linq.XNamespace)
            "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/SuministroInformacion.xsd";
        var doc = System.Xml.Linq.XDocument.Parse(xml);

        Assert.Equal("I", doc.Descendants(nsSf + "TipoRectificativa").Single().Value);
        Assert.Empty(doc.Descendants(nsSf + "ImporteRectificacion"));
        Assert.Equal("-24.20", doc.Descendants(nsSf + "ImporteTotal").Single().Value);

        var result = await CreateValidator().ValidateAsync(
            xml,
            VeriFactuXmlSchemaType.BillingRecord);

        Assert.True(
            result.IsValid,
            string.Join(" | ", result.Errors.Select(e => e.Message)));
    }


}
