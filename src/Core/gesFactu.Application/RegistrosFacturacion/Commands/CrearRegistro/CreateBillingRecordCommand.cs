using MediatR;
using gesFactu.Application.Common;

namespace gesFactu.Application.RegistrosFacturacion.Commands.CrearRegistro;

/// <summary>
/// Comando para crear un nuevo registro de facturación.
/// El encadenamiento VERI*FACTU se resuelve internamente por gesFactu.
/// </summary>
public sealed record CreateBillingRecordCommand(
    string IssuerNif,
    string InvoiceSeries,
    string InvoiceNumber,
    string IssueDate,
    string IssuerName,
    string Description,
    decimal TotalAmount,
    decimal TotalTaxAmount
) : IRequest<Result<CreateBillingRecordResponse>>;

public sealed record CreateBillingRecordResponse(
    int BillingRecordId,
    string InvoiceIdentifier,
    string Status,
    string? ComputedHash = null,
    DateTime CreatedAt = default);
