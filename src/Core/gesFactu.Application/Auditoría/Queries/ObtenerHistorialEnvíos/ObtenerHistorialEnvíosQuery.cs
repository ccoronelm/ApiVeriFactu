using MediatR;
using gesFactu.Application.Common.Abstractions;

namespace gesFactu.Application.Auditoría.Queries.ObtenerHistorialEnvíos;

/// <summary>
/// Query para obtener el historial completo de intentos de envío de un registro.
/// </summary>
public record ObtenerHistorialEnvíosQuery(int BillingRecordId) : IRequest<ObtenerHistorialEnvíosResult>;

/// <summary>
/// Resultado de la query con el historial de envíos.
/// </summary>
public record ObtenerHistorialEnvíosResult(
    int BillingRecordId,
    List<SubmissionAttemptDto> Intentos,
    bool TieneÉxito,
    DateTime? PrimeraFecha,
    DateTime? ÚltimaFecha
);
