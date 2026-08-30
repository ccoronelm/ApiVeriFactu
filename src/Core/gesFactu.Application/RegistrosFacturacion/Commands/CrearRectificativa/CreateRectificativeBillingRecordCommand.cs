using MediatR;
using gesFactu.Application.Common;

namespace gesFactu.Application.RegistrosFacturacion.Commands.CrearRectificativa;

/// <summary>
/// Crea un RegistroAlta rectificativo R1-R5 referenciado a un registro local
/// previamente aceptado por AEAT.
/// </summary>
public sealed record CreateRectificativeBillingRecordCommand(
    int SourceBillingRecordId,
    string InvoiceSeries,
    string InvoiceNumber,
    string IssueDate,
    string InvoiceType,
    string RectificationType,
    string Description,
    decimal TotalAmount,
    decimal TotalTaxAmount,
    IReadOnlyList<BillingTaxDetailInput>? TaxDetails = null)
    : IRequest<Result<CreateRectificativeBillingRecordResponse>>;

public sealed record CreateRectificativeBillingRecordResponse(
    int BillingRecordId,
    int SourceBillingRecordId,
    string InvoiceIdentifier,
    string InvoiceType,
    string RectificationType,
    string Status,
    string ComputedHash,
    DateTime CreatedAt);
