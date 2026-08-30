namespace gesFactu.Application.Common.Abstractions;

/// <summary>
/// Puerto para la construcción del XML de un RegistroAlta conforme a los XSD oficiales AEAT.
///
/// La implementación reside en Infrastructure (RegistroAltaXmlBuilder).
/// Application solo conoce este contrato — nunca los detalles XSD/namespace.
///
/// Ref: /VERIFACTU/SuministroLR.xsd.xml
/// Ref: /VERIFACTU/SuministroInformacion.xsd.xml
/// </summary>
public interface IRegistroAltaXmlBuilder
{
    /// <summary>
    /// Construye el XML completo RegFactuSistemaFacturacion listo para enviarse por SOAP.
    /// El XML generado es validable contra SuministroLR.xsd.
    /// </summary>
    string BuildRegFactuXml(RegistroAltaData data);
}

/// <summary>
/// Datos necesarios para construir un RegistroAlta.
/// Este DTO es agnóstico de XML/XSD — no contiene namespaces ni tipos AEAT.
/// </summary>
public sealed class RegistroAltaData
{
    // ?? Datos del emisor (obligado tributario) ????????????????????????????????
    public required string IssuerNif    { get; init; }
    public required string IssuerName   { get; init; }
    public required string InvoiceSeries { get; init; }
    public required string InvoiceNumber { get; init; }
    public required DateOnly IssueDate  { get; init; }

    // Destinatario obligatorio para F1.
    public required string RecipientNif { get; init; }
    public required string RecipientName { get; init; }

    // ?? Datos fiscales ????????????????????????????????????????????????????????
    /// <summary>
    /// Indica un alta de subsanación de un registro ya existente en AEAT.
    /// </summary>
    public bool IsSubsanacion { get; init; }

    /// <summary>Tipo de factura AEAT: F1, F2 o R1-R5.</summary>
    public required string TipoFactura  { get; init; }

    /// <summary>Tipo de rectificativa: I (por diferencias) o S (sustitutiva).</summary>
    public string? TipoRectificativa { get; init; }

    /// <summary>Facturas identificadas como rectificadas. AEAT permite hasta 1000.</summary>
    public IReadOnlyList<FacturaRectificadaData> FacturasRectificadas { get; init; } =
        Array.Empty<FacturaRectificadaData>();

    /// <summary>Importes de la factura sustituida. Obligatorio para TipoRectificativa=S.</summary>
    public ImporteRectificacionData? ImporteRectificacion { get; init; }

    public required string Description  { get; init; }
    public required decimal CuotaTotal  { get; init; }
    public required decimal ImporteTotal { get; init; }

    // ?? Desglose (al menos uno requerido) ?????????????????????????????????????
    public required IReadOnlyList<DetalleDesgloseData> Detalles { get; init; }

    // ?? Encadenamiento ????????????????????????????????????????????????????????
    public required string?  ComputedHash       { get; init; }
    public required string?  PreviousRecordHash { get; init; }
    public required DateOnly? PreviousIssueDate { get; init; }
    public required string?  PreviousIssuerNif  { get; init; }
    public required string?  PreviousInvoiceSeries { get; init; }
    public required string?  PreviousInvoiceNumber { get; init; }

    // ?? Timestamp de generación ???????????????????????????????????????????????
    /// <summary>
    /// FechaHoraHusoGenRegistro en formato dateTime con huso horario.
    /// Ej: "2025-02-03T14:30:00+01:00"
    /// </summary>
    public required string FechaHoraHusoGenRegistro { get; init; }
}

/// <summary>
/// Un elemento DetalleDesglose para el RegistroAlta.
/// </summary>
public sealed class DetalleDesgloseData
{
    /// <summary>Impuesto: "01"=IVA (default). Ref: SuministroInformacion.xsd ImpuestoType.</summary>
    public string? Impuesto { get; init; }

    /// <summary>Clave de régimen: "01"=Régimen general IVA.</summary>
    public string? ClaveRegimen { get; init; }

    /// <summary>CalificacionOperacion: "S1","S2","N1","N2".</summary>
    public string CalificacionOperacion { get; init; } = "S1";

    /// <summary>OperacionExenta: "E1"…"E8". Si se informa, no se informa CalificacionOperacion.</summary>
    public string? OperacionExenta { get; init; }

    /// <summary>Tipo impositivo (%). Ej: 21m para IVA 21%.</summary>
    public decimal? TipoImpositivo { get; init; }

    /// <summary>Base imponible o importe no sujeto.</summary>
    public required decimal BaseImponible { get; init; }

    /// <summary>Cuota repercutida.</summary>
    public decimal? CuotaRepercutida { get; init; }

    /// <summary>Porcentaje de recargo de equivalencia, cuando corresponda.</summary>
    public decimal? TipoRecargoEquivalencia { get; init; }

    /// <summary>Cuota del recargo de equivalencia.</summary>
    public decimal? CuotaRecargoEquivalencia { get; init; }
}


/// <summary>
/// Identidad fiscal de una factura rectificada.
/// </summary>
public sealed class FacturaRectificadaData
{
    public required string IssuerNif { get; init; }
    public required string InvoiceSeries { get; init; }
    public required string InvoiceNumber { get; init; }
    public required DateOnly IssueDate { get; init; }
}

/// <summary>
/// Desglose de los importes sustituidos en una rectificativa S.
/// </summary>
public sealed class ImporteRectificacionData
{
    public required decimal BaseRectificada { get; init; }
    public required decimal CuotaRectificada { get; init; }
    public decimal? CuotaRecargoRectificado { get; init; }
}
