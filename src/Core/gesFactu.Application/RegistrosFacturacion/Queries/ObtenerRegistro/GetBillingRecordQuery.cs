using MediatR;
using gesFactu.Application.Common;

namespace gesFactu.Application.RegistrosFacturacion.Queries.ObtenerRegistro;

/// <summary>
/// Query para obtener un registro de facturación por ID.
/// </summary>
public sealed record GetBillingRecordQuery(int BillingRecordId) : IRequest<Result<BillingRecordDto>>;

/// <summary>
/// DTO de respuesta con los datos del registro.
/// </summary>
public sealed record BillingRecordDto(
    int Id,
    string InvoiceIdentifier,
    string IssuerName,
    string RecipientNif,
    string RecipientName,
    string Description,
    decimal TotalAmount,
    decimal TotalTaxAmount,
    string Status,
    string? ComputedHash,
    string? PreviousRecordHash,
    string? AeatSubmissionId,
    string? SubmissionCorrelationId,
    bool IsSubmitted,
    DateTime? CreatedAt,
    string? CreatedBy,
    string RecordType,
    string InvoiceType,
    int? SubsanatesBillingRecordId,
    bool IsSubsanacion,
    int? CancelsBillingRecordId,
    bool IsCancellation,
    int? RectifiesBillingRecordId,
    string? RectificationType,
    decimal? RectifiedBaseAmount,
    decimal? RectifiedTaxAmount,
    decimal? RectifiedSurchargeAmount,
    bool IsRectificative
);
