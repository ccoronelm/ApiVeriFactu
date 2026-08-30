using MediatR;
using gesFactu.Application.Common;

namespace gesFactu.Application.RegistrosFacturacion.Commands.CrearSubsanacion;

/// <summary>
/// Genera un nuevo RegistroAlta de subsanación a partir de un registro que ya
/// existe en AEAT. Los campos null conservan el valor del registro de origen.
/// La clave fiscal no puede cambiar.
/// </summary>
public sealed record CreateBillingRecordSubsanationCommand(
    int SourceBillingRecordId,
    string? RecipientNif,
    string? RecipientName,
    string? Description,
    decimal? TotalAmount,
    decimal? TotalTaxAmount,
    IReadOnlyList<BillingTaxDetailInput>? TaxDetails = null)
    : IRequest<Result<CreateBillingRecordSubsanationResponse>>;

public sealed record CreateBillingRecordSubsanationResponse(
    int BillingRecordId,
    int SourceBillingRecordId,
    string InvoiceIdentifier,
    string Status,
    string ComputedHash,
    DateTime CreatedAt);
