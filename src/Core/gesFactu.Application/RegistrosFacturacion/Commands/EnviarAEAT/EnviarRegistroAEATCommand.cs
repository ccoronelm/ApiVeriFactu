using MediatR;
using gesFactu.Application.Common;

namespace gesFactu.Application.RegistrosFacturacion.Commands.EnviarAEAT;

public sealed record EnviarRegistroAEATCommand(int BillingRecordId)
    : IRequest<Result<EnviarRegistroAEATResponse>>;

/// <summary>
/// Confirmación de que la remisión ha quedado encolada.
/// Todavía no existe CSV AEAT hasta que el worker reciba una respuesta aceptada.
/// </summary>
public sealed record EnviarRegistroAEATResponse
{
    public required int BillingRecordId { get; init; }

    /// <summary>
    /// Identificador local para seguimiento del Outbox.
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// CSV real de AEAT. Será null mientras el envío esté pendiente.
    /// </summary>
    public string? AeatSubmissionId { get; init; }

    public required bool IsAccepted { get; init; }
    public required string Status { get; init; }
    public required string StatusDescription { get; init; }
    public string? Details { get; init; }
}
