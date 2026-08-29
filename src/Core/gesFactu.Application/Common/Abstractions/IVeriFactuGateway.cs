namespace gesFactu.Application.Common.Abstractions;

/// <summary>
/// Puerto que define la integración con AEAT VERI*FACTU.
/// 
/// Esta abstracción aísla Application de los detalles SOAP/WSDL/XSD de AEAT.
/// La implementación en Infrastructure manejará la Anti-Corruption Layer,
/// traduciendo entre el modelo de AEAT y los tipos de negocio de gesFactu.
/// 
/// Ver documentación oficial: /VERIFACTU
/// </summary>
public interface IVeriFactuGateway
{
    /// <summary>
    /// Envía un registro de facturación a AEAT.
    /// </summary>
    Task<VeriFactuSubmissionResult> SubmitBillingRecordAsync(
        VeriFactuSubmissionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Consulta el estado de un registro previamente enviado.
    /// </summary>
    Task<VeriFactuQueryResult> QueryBillingRecordAsync(
        VeriFactuQueryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Solicita la cancelación de un registro en AEAT.
    /// Requiere el SubmissionId del registro original.
    /// </summary>
    Task<VeriFactuCancellationResult> CancelBillingRecordAsync(
        VeriFactuCancellationRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Solicitud para enviar un registro de facturación a AEAT.
/// Este modelo es agnóstico a SOAP/XML; la implementación traduce a WSDL.
/// </summary>
public record VeriFactuSubmissionRequest
{
    /// <summary>
    /// NIF/CIF del contribuyente.
    /// </summary>
    public required string TaxpayerNif { get; init; }

    /// <summary>
    /// Datos XML ya firmados del registro de facturación.
    /// La serialización XML corresponde a Infrastructure.
    /// </summary>
    public required string SignedXmlContent { get; init; }

    /// <summary>
    /// Hash del registro anterior en la cadena (si aplica).
    /// </summary>
    public string? PreviousRecordHash { get; init; }
}

/// <summary>
/// Respuesta de AEAT tras enviar un registro.
/// </summary>
public record VeriFactuSubmissionResult
{
    /// <summary>
    /// Identificador único asignado por AEAT a este envío.
    /// </summary>
    public required string SubmissionId { get; init; }

    /// <summary>
    /// Indica si el envío fue aceptado.
    /// </summary>
    public required bool IsAccepted { get; init; }

    /// <summary>
    /// Código de respuesta clasificado (para decisiones de retry).
    /// </summary>
    public required AeatResponseCode ResponseCode { get; init; }

    /// <summary>
    /// Código de estado AEAT (numérico o alfanumérico original).
    /// </summary>
    public required string StatusCode { get; init; }

    /// <summary>
    /// Descripción del estado (puede contener validaciones o errores).
    /// </summary>
    public required string StatusDescription { get; init; }

    /// <summary>
    /// Detalles adicionales de validación o error (JSON/lista).
    /// </summary>
    public string? AdditionalDetails { get; init; }

    /// <summary>
    /// Timestamp de AEAT asociado a este envío.
    /// </summary>
    public DateTime? ServerTimestamp { get; init; }
}

/// <summary>
/// Solicitud para consultar un registro en AEAT.
/// </summary>
public record VeriFactuQueryRequest
{
    /// <summary>
    /// NIF/CIF del contribuyente.
    /// </summary>
    public required string TaxpayerNif { get; init; }

    /// <summary>
    /// Identificador del envío a consultar (recibido en SubmissionResult).
    /// </summary>
    public required string SubmissionId { get; init; }
}

/// <summary>
/// Respuesta de una consulta a AEAT.
/// </summary>
public record VeriFactuQueryResult
{
    /// <summary>
    /// Identificador del envío consultado.
    /// </summary>
    public required string SubmissionId { get; init; }

    /// <summary>
    /// Estado actual del envío en AEAT.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Descripción del estado.
    /// </summary>
    public required string StatusDescription { get; init; }

    /// <summary>
    /// Detalles adicionales.
    /// </summary>
    public string? AdditionalDetails { get; init; }
}

/// <summary>
/// Solicitud para cancelar un registro en AEAT.
/// </summary>
public record VeriFactuCancellationRequest
{
    /// <summary>
    /// NIF/CIF del contribuyente.
    /// </summary>
    public required string TaxpayerNif { get; init; }

    /// <summary>
    /// Identificador del envío a cancelar (recibido en SubmissionResult).
    /// </summary>
    public required string SubmissionId { get; init; }

    /// <summary>
    /// Motivo de la cancelación (requerido por AEAT).
    /// </summary>
    public required string CancellationReason { get; init; }
}

/// <summary>
/// Respuesta de AEAT a una solicitud de cancelación.
/// </summary>
public record VeriFactuCancellationResult
{
    /// <summary>
    /// Indica si la cancelación fue aceptada.
    /// </summary>
    public required bool IsAccepted { get; init; }

    /// <summary>
    /// Código de estado AEAT.
    /// </summary>
    public required string StatusCode { get; init; }

    /// <summary>
    /// Descripción de la respuesta.
    /// </summary>
    public required string StatusDescription { get; init; }

    /// <summary>
    /// ID de cancelación asignado por AEAT (si fue exitosa).
    /// </summary>
    public string? CancellationId { get; init; }
}
