using MediatR;
using gesFactu.Application.Common;

namespace gesFactu.Application.RegistrosFacturacion.Commands.EnviarAEAT;

/// <summary>
/// Comando CQRS para enviar un registro de facturación a AEAT.
/// 
/// Precondiciones:
/// - El registro debe existir y estar pendiente
/// - Debe tener un hash calculado
/// - Puede optarse por incluir datos de certificado o usar del contexto
/// </summary>
public sealed record EnviarRegistroAEATCommand(int BillingRecordId) : IRequest<Result<EnviarRegistroAEATResponse>>;

/// <summary>
/// Respuesta del comando de envío a AEAT.
/// </summary>
public sealed record EnviarRegistroAEATResponse
{
    /// <summary>
    /// ID único del registro de facturación.
    /// </summary>
    public required int BillingRecordId { get; init; }

    /// <summary>
    /// ID asignado por AEAT a este envío.
    /// </summary>
    public required string AeatSubmissionId { get; init; }

    /// <summary>
    /// Indica si fue aceptado por AEAT.
    /// </summary>
    public required bool IsAccepted { get; init; }

    /// <summary>
    /// Estado/código retornado por AEAT.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Descripción del estado.
    /// </summary>
    public required string StatusDescription { get; init; }

    /// <summary>
    /// Detalles adicionales si hay validaciones o errores.
    /// </summary>
    public string? Details { get; init; }
}
