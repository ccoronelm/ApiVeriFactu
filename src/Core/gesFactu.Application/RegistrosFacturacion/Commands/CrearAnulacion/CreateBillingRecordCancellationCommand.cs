using MediatR;
using gesFactu.Application.Common;

namespace gesFactu.Application.RegistrosFacturacion.Commands.CrearAnulacion;

/// <summary>
/// Genera un RegistroAnulacion para una factura que ya existe en AEAT.
/// </summary>
public sealed record CreateBillingRecordCancellationCommand(
    int SourceBillingRecordId)
    : IRequest<Result<CreateBillingRecordCancellationResponse>>;

public sealed record CreateBillingRecordCancellationResponse(
    int BillingRecordId,
    int SourceBillingRecordId,
    string InvoiceIdentifier,
    string Status,
    string ComputedHash,
    DateTime CreatedAt);
