using MediatR;
using gesFactu.Application.Common;

namespace gesFactu.Application.RegistrosFacturacion.Commands.CrearRegistro;

/// <summary>
/// Comando para crear un nuevo registro de facturación.
/// </summary>
public sealed record CreateBillingRecordCommand(
    // Identificación de la factura
    string IssuerNif,
    string InvoiceSeries,
    string InvoiceNumber,
    string IssueDate, // formato: dd-MM-yyyy

    // Datos del registro
    string IssuerName,
    string Description,
    decimal TotalAmount,
    decimal TotalTaxAmount,

    // Encadenamiento
    string? PreviousRecordHash = null
) : IRequest<Result<CreateBillingRecordResponse>>;

/// <summary>
/// Respuesta al crear un registro de facturación.
/// </summary>
public sealed record CreateBillingRecordResponse(
    int BillingRecordId,
    string InvoiceIdentifier,
    string Status,
    string? ComputedHash = null,
    DateTime CreatedAt = default
);
