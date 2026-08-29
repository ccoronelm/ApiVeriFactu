namespace gesFactu.Infrastructure.Integrations.VeriFactu.XmlGeneration;

/// <summary>
/// Tipos de datos para la generación de registros de facturación conforme a AEAT VERI*FACTU.
/// 
/// Estos tipos mapean directamente a la estructura XSD oficial.
/// No deben ser expuestos fuera de Infrastructure (Anti-Corruption Layer).
/// 
/// Ref: /VERIFACTU/SuministroInformacion.xsd
/// Ref: /VERIFACTU - Ejemplos de registros
/// </summary>

/// <summary>
/// Registro de facturación conforme a AEAT.
/// Elemento raíz: SuministroInformacion
/// </summary>
public sealed class VeriFactuBillingRecord
{
    /// <summary>
    /// Cabecera del registro con metadata.
    /// </summary>
    public required VeriFactuCabecera Cabecera { get; set; }

    /// <summary>
    /// Detalles de las facturas/documentos.
    /// </summary>
    public required List<VeriFactuDetalle> Detalles { get; set; } = new();
}

/// <summary>
/// Cabecera del registro de facturación.
/// </summary>
public sealed class VeriFactuCabecera
{
    /// <summary>
    /// Versión de VERI*FACTU (ej: "1.0").
    /// </summary>
    public string Versión { get; set; } = "1.0";

    /// <summary>
    /// NIF/CIF del contribuyente que emite la factura.
    /// </summary>
    public required string NifEmisor { get; set; }

    /// <summary>
    /// Nombre/razón social del emisor.
    /// </summary>
    public string? NombreEmisor { get; set; }

    /// <summary>
    /// Periodo de facturación (ej: "01/2024", "202401").
    /// </summary>
    public required string PeriodoFacturacion { get; set; }

    /// <summary>
    /// Identificador único de este suministro (generado por el sistema).
    /// </summary>
    public string? IdSuministro { get; set; }

    /// <summary>
    /// Timestamp de creación del suministro.
    /// Formato: yyyy-MM-ddTHH:mm:ssZ (UTC)
    /// </summary>
    public DateTime? FechaGeneracion { get; set; }

    /// <summary>
    /// Indica si es un reenvío de suministro anterior.
    /// </summary>
    public bool EsReenvio { get; set; }

    /// <summary>
    /// Huella/hash del suministro anterior (si es reenvío).
    /// </summary>
    public string? HuellaAnterior { get; set; }
}

/// <summary>
/// Detalle de un registro de facturación individual.
/// </summary>
public sealed class VeriFactuDetalle
{
    /// <summary>
    /// Tipo de documento:
    /// - "Factura" / "F"
    /// - "Factura simplificada" / "FS"
    /// - "Rectificativa" / "R"
    /// - "Resumen" / "Res"
    /// </summary>
    public required string TipoDocumento { get; set; }

    /// <summary>
    /// Número de serie de la factura.
    /// </summary>
    public string? Serie { get; set; }

    /// <summary>
    /// Número de factura.
    /// </summary>
    public required string Numero { get; set; }

    /// <summary>
    /// Fecha de emisión (yyyy-MM-dd).
    /// </summary>
    public required DateOnly FechaExpedicion { get; set; }

    /// <summary>
    /// Fecha de operación (puede diferir de expedición).
    /// </summary>
    public DateOnly? FechaOperacion { get; set; }

    /// <summary>
    /// Descripción del concepto.
    /// </summary>
    public string? Descripcion { get; set; }

    /// <summary>
    /// Base imponible (sin IVA).
    /// Formato: decimal con máximo 2 decimales.
    /// </summary>
    public required decimal BaseImponible { get; set; }

    /// <summary>
    /// Cuota de IVA a aplicar.
    /// </summary>
    public required decimal CuotaIva { get; set; }

    /// <summary>
    /// Tipo de IVA (% - ej: 21.00).
    /// </summary>
    public required decimal PorcentajeIva { get; set; }

    /// <summary>
    /// Importe total de la factura (base + IVA).
    /// </summary>
    public required decimal ImporteTotal { get; set; }

    /// <summary>
    /// Información del cliente/sujeto pasivo.
    /// </summary>
    public VeriFactuCliente? Cliente { get; set; }

    /// <summary>
    /// Desglose de impuestos (detalle por tipo de impuesto).
    /// </summary>
    public List<VeriFactuImpuesto> Impuestos { get; set; } = new();

    /// <summary>
    /// Huella/hash del registro anterior en la cadena.
    /// </summary>
    public string? HuellaRegistroAnterior { get; set; }

    /// <summary>
    /// Contador secuencial único para este contribuyente.
    /// </summary>
    public long? NumeroRegistro { get; set; }
}

/// <summary>
/// Información del cliente / sujeto pasivo.
/// </summary>
public sealed class VeriFactuCliente
{
    /// <summary>
    /// NIF/CIF del cliente.
    /// </summary>
    public required string Nif { get; set; }

    /// <summary>
    /// Nombre/razón social del cliente.
    /// </summary>
    public string? Nombre { get; set; }

    /// <summary>
    /// Tipo de documento (NIF, NIE, Pasaporte, etc.).
    /// </summary>
    public string? TipoDocumento { get; set; }
}

/// <summary>
/// Desglose de un impuesto específico.
/// </summary>
public sealed class VeriFactuImpuesto
{
    /// <summary>
    /// Tipo de impuesto (ej: "VAT", "IVA").
    /// </summary>
    public required string Tipo { get; set; }

    /// <summary>
    /// Base imponible para este impuesto.
    /// </summary>
    public required decimal Base { get; set; }

    /// <summary>
    /// Porcentaje del impuesto.
    /// </summary>
    public required decimal Porcentaje { get; set; }

    /// <summary>
    /// Cuota/importe del impuesto.
    /// </summary>
    public required decimal Cuota { get; set; }
}

/// <summary>
/// Registro de anulación/cancelación conforme a AEAT.
/// Elemento raíz: SuministroLR
/// </summary>
public sealed class VeriFactuCancellationRecord
{
    /// <summary>
    /// Cabecera del registro de anulación.
    /// </summary>
    public required VeriFactuCabeceraAnulacion Cabecera { get; set; }

    /// <summary>
    /// Detalles de registros a anular.
    /// </summary>
    public required List<VeriFactuDetalleAnulacion> Detalles { get; set; } = new();
}

/// <summary>
/// Cabecera de un registro de anulación.
/// </summary>
public sealed class VeriFactuCabeceraAnulacion
{
    /// <summary>
    /// NIF del contribuyente.
    /// </summary>
    public required string NifEmisor { get; set; }

    /// <summary>
    /// Período de facturación a anular.
    /// </summary>
    public required string PeriodoFacturacion { get; set; }

    /// <summary>
    /// Motivo de la anulación.
    /// Valores: "1" = Error de apreciación, "2" = Extornornada, etc.
    /// </summary>
    public required string MotivoAnulacion { get; set; }
}

/// <summary>
/// Detalle de un registro individual a anular.
/// </summary>
public sealed class VeriFactuDetalleAnulacion
{
    /// <summary>
    /// Número de registro a anular (referencia).
    /// </summary>
    public required long NumeroRegistro { get; set; }

    /// <summary>
    /// Número de factura a anular.
    /// </summary>
    public required string NumeroFactura { get; set; }

    /// <summary>
    /// Serie de la factura.
    /// </summary>
    public string? Serie { get; set; }

    /// <summary>
    /// Fecha de expedición original.
    /// </summary>
    public required DateOnly FechaExpedicion { get; set; }

    /// <summary>
    /// Descripción del motivo de anulación.
    /// </summary>
    public string? Descripcion { get; set; }
}
