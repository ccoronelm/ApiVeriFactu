using System.Xml.Linq;
using gesFactu.Application.Common.Abstractions;
using Microsoft.Extensions.Logging;

namespace gesFactu.Infrastructure.Integrations.VeriFactu.XmlGeneration;

/// <summary>
/// Generador de documentos XML conforme a especificación AEAT VERI*FACTU.
/// 
/// Transforma datos de negocio en XML validable contra XSD official.
/// Incluye:
/// - Namespace correcto (http://www.aeat.gob.es/VeriFacTuSF)
/// - Formato de decimales correcto (2 dígitos)
/// - Fechas en ISO 8601 (yyyy-MM-dd, HH:mm:ssZ)
/// - Elementos requeridos según especificación
/// - Encadeanamiento de hashes/huellas
/// 
/// Ref: /VERIFACTU/SuministroInformacion.xsd
/// Ref: /VERIFACTU - Ejemplos de registros
/// </summary>
public sealed class VeriFactuXmlGenerator
{
    private readonly ILogger<VeriFactuXmlGenerator> _logger;
    private readonly IHashCalculator _hashCalculator;

    // Namespace oficial AEAT
    private static readonly XNamespace NS = "http://www.aeat.gob.es/VeriFacTuSF";

    public VeriFactuXmlGenerator(
        ILogger<VeriFactuXmlGenerator> logger,
        IHashCalculator hashCalculator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _hashCalculator = hashCalculator ?? throw new ArgumentNullException(nameof(hashCalculator));
    }

    /// <summary>
    /// Genera XML de registro de facturación.
    /// </summary>
    public async Task<string> GenerateBillingRecordXmlAsync(
        VeriFactuBillingRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        _logger.LogInformation("Generando XML de registro de facturación");

        await Task.Delay(10, cancellationToken); // Simular procesamiento

        try
        {
            var doc = new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                BuildSuministroInformacion(record));

            var xmlString = doc.ToString();

            _logger.LogDebug("XML de registro generado exitosamente. Longitud: {Length}", xmlString.Length);

            return xmlString;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar XML de registro de facturación");
            throw;
        }
    }

    /// <summary>
    /// Genera XML de registro de anulación.
    /// </summary>
    public async Task<string> GenerateCancellationRecordXmlAsync(
        VeriFactuCancellationRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        _logger.LogInformation("Generando XML de registro de anulación");

        await Task.Delay(10, cancellationToken);

        try
        {
            var doc = new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                BuildSuministroLR(record));

            return doc.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar XML de registro de anulación");
            throw;
        }
    }

    /// <summary>
    /// Construye el elemento SuministroInformacion (registro de facturación).
    /// </summary>
    private XElement BuildSuministroInformacion(VeriFactuBillingRecord record)
    {
        var cabecera = record.Cabecera;

        var element = new XElement(NS + "SuministroInformacion",
            new XAttribute("xmlns", NS.NamespaceName),
            new XAttribute("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance"),
            new XAttribute("xsi:schemaLocation", $"{NS.NamespaceName} SuministroInformacion.xsd"),

            // Cabecera
            new XElement(NS + "Cabecera",
                new XElement(NS + "Versión", cabecera.Versión),
                new XElement(NS + "NifEmisor", cabecera.NifEmisor),
                new XElement(NS + "NombreEmisor", cabecera.NombreEmisor),
                new XElement(NS + "PeriodoFacturacion", cabecera.PeriodoFacturacion),
                cabecera.IdSuministro != null ? new XElement(NS + "IdSuministro", cabecera.IdSuministro) : null,
                cabecera.FechaGeneracion.HasValue 
                    ? new XElement(NS + "FechaGeneracion", cabecera.FechaGeneracion.Value.ToString("O"))
                    : null,
                cabecera.EsReenvio 
                    ? new XElement(NS + "EsReenvio", "S")
                    : null,
                cabecera.HuellaAnterior != null
                    ? new XElement(NS + "HuellaAnterior", cabecera.HuellaAnterior)
                    : null),

            // Registros de facturación
            new XElement(NS + "Registros",
                record.Detalles.Select(BuildRegistroFacturacion).ToArray())
        );

        return element;
    }

    /// <summary>
    /// Construye un registro de facturación individual.
    /// </summary>
    private XElement BuildRegistroFacturacion(VeriFactuDetalle detalle)
    {
        var registro = new XElement(NS + "RegistroFacturacion",
            new XElement(NS + "TipoDocumento", detalle.TipoDocumento),
            detalle.Serie != null 
                ? new XElement(NS + "Serie", detalle.Serie)
                : null,
            new XElement(NS + "Numero", detalle.Numero),
            new XElement(NS + "FechaExpedicion", detalle.FechaExpedicion.ToString("yyyy-MM-dd")),
            detalle.FechaOperacion.HasValue
                ? new XElement(NS + "FechaOperacion", detalle.FechaOperacion.Value.ToString("yyyy-MM-dd"))
                : null,
            new XElement(NS + "Descripcion", detalle.Descripcion ?? ""),

            // Importes
            new XElement(NS + "BaseImponible", FormatDecimal(detalle.BaseImponible)),
            new XElement(NS + "CuotaIva", FormatDecimal(detalle.CuotaIva)),
            new XElement(NS + "PorcentajeIva", FormatDecimal(detalle.PorcentajeIva)),
            new XElement(NS + "ImporteTotal", FormatDecimal(detalle.ImporteTotal)),

            // Cliente
            detalle.Cliente != null ? BuildCliente(detalle.Cliente) : null,

            // Impuestos
            detalle.Impuestos.Any()
                ? new XElement(NS + "Impuestos",
                    detalle.Impuestos.Select(BuildImpuesto).ToArray())
                : null,

            // Cadena de huellas
            detalle.HuellaRegistroAnterior != null
                ? new XElement(NS + "HuellaRegistroAnterior", detalle.HuellaRegistroAnterior)
                : null,
            detalle.NumeroRegistro.HasValue
                ? new XElement(NS + "NumeroRegistro", detalle.NumeroRegistro.Value)
                : null);

        return registro;
    }

    /// <summary>
    /// Construye elemento de cliente.
    /// </summary>
    private XElement BuildCliente(VeriFactuCliente cliente)
    {
        return new XElement(NS + "Cliente",
            new XElement(NS + "Nif", cliente.Nif),
            cliente.Nombre != null
                ? new XElement(NS + "Nombre", cliente.Nombre)
                : null,
            cliente.TipoDocumento != null
                ? new XElement(NS + "TipoDocumento", cliente.TipoDocumento)
                : null);
    }

    /// <summary>
    /// Construye elemento de impuesto.
    /// </summary>
    private XElement BuildImpuesto(VeriFactuImpuesto impuesto)
    {
        return new XElement(NS + "Impuesto",
            new XElement(NS + "Tipo", impuesto.Tipo),
            new XElement(NS + "Base", FormatDecimal(impuesto.Base)),
            new XElement(NS + "Porcentaje", FormatDecimal(impuesto.Porcentaje)),
            new XElement(NS + "Cuota", FormatDecimal(impuesto.Cuota)));
    }

    /// <summary>
    /// Construye el elemento SuministroLR (registro de anulación).
    /// </summary>
    private XElement BuildSuministroLR(VeriFactuCancellationRecord record)
    {
        var cabecera = record.Cabecera;

        return new XElement(NS + "SuministroLR",
            new XAttribute("xmlns", NS.NamespaceName),
            new XAttribute("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance"),
            new XAttribute("xsi:schemaLocation", $"{NS.NamespaceName} SuministroLR.xsd"),

            new XElement(NS + "Cabecera",
                new XElement(NS + "NifEmisor", cabecera.NifEmisor),
                new XElement(NS + "PeriodoFacturacion", cabecera.PeriodoFacturacion),
                new XElement(NS + "MotivoAnulacion", cabecera.MotivoAnulacion)),

            new XElement(NS + "Registros",
                record.Detalles.Select(BuildRegistroAnulacion).ToArray()));
    }

    /// <summary>
    /// Construye un registro individual de anulación.
    /// </summary>
    private XElement BuildRegistroAnulacion(VeriFactuDetalleAnulacion detalle)
    {
        return new XElement(NS + "RegistroAnulacion",
            new XElement(NS + "NumeroRegistro", detalle.NumeroRegistro),
            new XElement(NS + "NumeroFactura", detalle.NumeroFactura),
            detalle.Serie != null
                ? new XElement(NS + "Serie", detalle.Serie)
                : null,
            new XElement(NS + "FechaExpedicion", detalle.FechaExpedicion.ToString("yyyy-MM-dd")),
            detalle.Descripcion != null
                ? new XElement(NS + "Descripcion", detalle.Descripcion)
                : null);
    }

    /// <summary>
    /// Formatea un valor decimal con 2 decimales, usando punto como separador decimal.
    /// Conforme a especificación AEAT (InvariantCulture).
    /// </summary>
    private static string FormatDecimal(decimal value)
    {
        return value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
    }
}
