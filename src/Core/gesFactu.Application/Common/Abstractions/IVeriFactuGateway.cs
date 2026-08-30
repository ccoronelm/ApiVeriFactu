namespace gesFactu.Application.Common.Abstractions;

/// <summary>
/// Puerto de integración con AEAT VERI*FACTU.
/// Infrastructure encapsula SOAP/XML/WSDL/XSD y autenticación mTLS.
/// </summary>
public interface IVeriFactuGateway
{
    Task<VeriFactuSubmissionResult> SubmitBillingRecordAsync(
        VeriFactuSubmissionRequest request,
        CancellationToken cancellationToken = default);

    Task<VeriFactuQueryResult> QueryBillingRecordAsync(
        VeriFactuQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<VeriFactuCancellationResult> CancelBillingRecordAsync(
        VeriFactuCancellationRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Solicitud de remisión de un RegistroAlta.
/// El XML no implica firma XAdES: en modalidad VERI*FACTU la autenticación es mTLS.
/// </summary>
public record VeriFactuSubmissionRequest
{
    public required string TaxpayerNif { get; init; }

    /// <summary>
    /// XML RegistroAlta construido por Infrastructure.
    /// Se conserva el nombre por compatibilidad interna, pero no significa XML firmado.
    /// </summary>
    public required string SignedXmlContent { get; init; }

    public string? PreviousRecordHash { get; init; }
}

/// <summary>
/// Resultado de una remisión AEAT de un único registro.
/// </summary>
public record VeriFactuSubmissionResult
{
    /// <summary>
    /// CSV del envío. Es opcional porque AEAT no lo genera cuando rechaza el envío.
    /// </summary>
    public string? SubmissionId { get; init; }

    /// <summary>
    /// True cuando el EstadoRegistro es Correcto o AceptadoConErrores.
    /// </summary>
    public required bool IsAccepted { get; init; }

    public required AeatResponseCode ResponseCode { get; init; }

    /// <summary>
    /// Estado global del envío: Correcto, ParcialmenteCorrecto o Incorrecto.
    /// </summary>
    public required string StatusCode { get; init; }

    /// <summary>
    /// Estado del registro individual: Correcto, AceptadoConErrores o Incorrecto.
    /// </summary>
    public string? RecordStatus { get; init; }

    public required string StatusDescription { get; init; }

    /// <summary>
    /// Primer código de error de registro, si existe.
    /// </summary>
    public string? ErrorCode { get; init; }

    public bool IsDuplicate { get; init; }

    /// <summary>
    /// Estado que AEAT informa para el registro ya existente cuando devuelve duplicado.
    /// </summary>
    public string? DuplicateRecordStatus { get; init; }

    public string? AdditionalDetails { get; init; }

    /// <summary>
    /// TimestampPresentacion devuelto por AEAT cuando existe.
    /// </summary>
    public DateTime? ServerTimestamp { get; init; }

    /// <summary>
    /// XML SOAP completo recibido. Se usa para auditoría persistente.
    /// </summary>
    public string? RawResponsePayload { get; init; }
}

public record VeriFactuQueryRequest
{
    public required string TaxpayerNif { get; init; }
    public required string SubmissionId { get; init; }
}

public record VeriFactuQueryResult
{
    public required string SubmissionId { get; init; }
    public required string Status { get; init; }
    public required string StatusDescription { get; init; }
    public string? AdditionalDetails { get; init; }
}

public record VeriFactuCancellationRequest
{
    public required string TaxpayerNif { get; init; }
    public required string SubmissionId { get; init; }
    public required string CancellationReason { get; init; }
    public string? CancellationXmlContent { get; init; }
}

public record VeriFactuCancellationResult
{
    public required bool IsAccepted { get; init; }
    public required string StatusCode { get; init; }
    public required string StatusDescription { get; init; }
    public string? CancellationId { get; init; }
    public required AeatResponseCode ResponseCode { get; init; }
    public string? AdditionalDetails { get; init; }
    public DateTime? ServerTimestamp { get; init; }
}
