using MediatR;
using gesFactu.Application.Common.Abstractions;

namespace gesFactu.Application.Auditoría.Queries.ObtenerHistorialEnvíos;

/// <summary>
/// Handler para la query de historial de envíos.
/// </summary>
public sealed class ObtenerHistorialEnvíosQueryHandler
    : IRequestHandler<ObtenerHistorialEnvíosQuery, ObtenerHistorialEnvíosResult>
{
    private readonly ISubmissionAttemptStore _attemptStore;

    public ObtenerHistorialEnvíosQueryHandler(ISubmissionAttemptStore attemptStore)
    {
        _attemptStore = attemptStore ?? throw new ArgumentNullException(nameof(attemptStore));
    }

    public async Task<ObtenerHistorialEnvíosResult> Handle(
        ObtenerHistorialEnvíosQuery request,
        CancellationToken cancellationToken)
    {
        var intentos = await _attemptStore.GetByBillingRecordIdAsync(
            request.BillingRecordId,
            cancellationToken);

        var tieneÉxito = intentos.Any(a => a.Estado == "Success");
        var primeraFecha = intentos.FirstOrDefault()?.FechaEnvío;
        var ultimo = intentos.LastOrDefault();
        var últimaFecha = ultimo?.FechaRespuesta ?? ultimo?.FechaEnvío;

        return new ObtenerHistorialEnvíosResult(
            request.BillingRecordId,
            intentos,
            tieneÉxito,
            primeraFecha,
            últimaFecha);
    }
}
