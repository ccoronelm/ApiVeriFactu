using MediatR;
using gesFactu.Application.Common;

namespace gesFactu.Application.RegistrosFacturacion.Commands.CrearRegistro;

/// <summary>
/// Comando para crear un RegistroAlta F1 o F2.
/// El encadenamiento VERI*FACTU se resuelve internamente por gesFactu.
/// </summary>
public sealed record CreateBillingRecordCommand(
    string IssuerNif,
    string InvoiceSeries,
    string InvoiceNumber,
    string IssueDate,
    string IssuerName,
    string? RecipientNif,
    string? RecipientName,
    string Description,
    decimal TotalAmount,
    decimal TotalTaxAmount,
    string InvoiceType = "F1",
    IReadOnlyList<BillingTaxDetailInput>? TaxDetails = null
) : IRequest<Result<CreateBillingRecordResponse>>;

public sealed record CreateBillingRecordResponse(
    int BillingRecordId,
    string InvoiceIdentifier,
    string Status,
    string? ComputedHash = null,
    DateTime CreatedAt = default);
