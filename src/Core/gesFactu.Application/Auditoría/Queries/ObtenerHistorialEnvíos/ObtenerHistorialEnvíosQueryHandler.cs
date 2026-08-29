using MediatR;
using gesFactu.Application.Common.Abstractions;

namespace gesFactu.Application.Auditoría.Queries.ObtenerHistorialEnvíos;

/// <summary>
/// Handler para la query de historial de envíos.
/// </summary>
public class ObtenerHistorialEnvíosQueryHandler : IRequestHandler<ObtenerHistorialEnvíosQuery, ObtenerHistorialEnvíosResult>
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
        var intentosDto = await _attemptStore.GetByBillingRecordIdAsync(
            request.BillingRecordId,
            cancellationToken);

        var tieneÉxito = intentosDto.Any(a => a.Estado == "Success");
        var primeraFecha = intentosDto.FirstOrDefault()?.FechaEnvío;
        var últimaFecha = intentosDto.LastOrDefault()?.FechaRespuesta ?? intentosDto.LastOrDefault()?.FechaEnvío;

        return new ObtenerHistorialEnvíosResult(
            request.BillingRecordId,
            intentosDto,
            tieneÉxito,
            primeraFecha,
            últimaFecha);
    }
}
