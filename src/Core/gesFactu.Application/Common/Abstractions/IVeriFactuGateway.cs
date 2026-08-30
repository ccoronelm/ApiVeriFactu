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

    /// <summary>
    /// IdPeticionRegistroDuplicado devuelto por AEAT cuando Error 3000 identifica
    /// el registro previamente almacenado.
    /// </summary>
    public string? DuplicateRequestId { get; init; }

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
    public required string TaxpayerName { get; init; }
    public required string FiscalYear { get; init; }
    public required string Period { get; init; }

    /// <summary>Número serie+factura completo tal como lo identifica AEAT.</summary>
    public string? InvoiceNumber { get; init; }

    public string? CounterpartyNif { get; init; }
    public string? CounterpartyName { get; init; }

    public DateOnly? IssueDate { get; init; }
    public DateOnly? IssueDateFrom { get; init; }
    public DateOnly? IssueDateTo { get; init; }

    public string? ExternalReference { get; init; }

    public VeriFactuSystemFilter? System { get; init; }

    /// <summary>
    /// Clave devuelta por AEAT en la página anterior.
    /// </summary>
    public VeriFactuPaginationKey? PaginationKey { get; init; }

    public bool ShowIssuerName { get; init; }
    public bool ShowSystemInformation { get; init; }
}

public record VeriFactuSystemFilter
{
    public required string ProducerName { get; init; }
    public required string ProducerNif { get; init; }
    public required string SystemId { get; init; }
    public required string InstallationNumber { get; init; }
    public string? SystemName { get; init; }
    public string? Version { get; init; }
}

public record VeriFactuPaginationKey
{
    public required string IssuerNif { get; init; }
    public required string InvoiceNumber { get; init; }
    public required DateOnly IssueDate { get; init; }
}

public record VeriFactuQueryResult
{
    public required string FiscalYear { get; init; }
    public required string Period { get; init; }
    public required string Result { get; init; }
    public required bool HasMorePages { get; init; }
    public VeriFactuPaginationKey? NextPageKey { get; init; }
    public required IReadOnlyList<VeriFactuQueryRecord> Records { get; init; }
    public string? RawResponsePayload { get; init; }
}

public record VeriFactuQueryRecord
{
    public required string IssuerNif { get; init; }
    public required string InvoiceNumber { get; init; }
    public required DateOnly IssueDate { get; init; }
    public string? IssuerName { get; init; }
    public string? InvoiceType { get; init; }
    public string? RectificationType { get; init; }
    public string? Description { get; init; }
    public decimal? TotalTaxAmount { get; init; }
    public decimal? TotalAmount { get; init; }
    public string? Hash { get; init; }
    public string? RegisterTimestamp { get; init; }
    public required string RecordStatus { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorDescription { get; init; }
    public DateTimeOffset? LastModifiedAt { get; init; }
}
