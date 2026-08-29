using gesFactu.Domain.Entities;

namespace gesFactu.Application.Common.Abstractions;

/// <summary>
/// Puerto para persistencia de intentos de envío a AEAT (auditoría).
/// </summary>
public interface ISubmissionAttemptStore
{
    /// <summary>
    /// Registra un nuevo intento de envío.
    /// </summary>
    Task<SubmissionAttemptDto> CreateAsync(
        int billingRecordId,
        int attemptNumber,
        string requestPayload,
        CancellationToken cancellationToken);

    /// <summary>
    /// Marca un intento como exitoso.
    /// </summary>
    Task MarkAsSuccessAsync(
        Guid attemptId,
        string responseCode,
        string? responseDescription,
        string? responsePayload,
        string? aeatSubmissionId,
        int durationMilliseconds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Marca un intento como fallo permanente.
    /// </summary>
    Task MarkAsPermanentFailureAsync(
        Guid attemptId,
        string responseCode,
        string? responseDescription,
        string? responsePayload,
        string? notes,
        int durationMilliseconds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Marca un intento como fallo transitorio.
    /// </summary>
    Task MarkAsTransientFailureAsync(
        Guid attemptId,
        string responseCode,
        string? responseDescription,
        string? notes,
        int durationMilliseconds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Obtiene todos los intentos de un registro.
    /// </summary>
    Task<List<SubmissionAttemptDto>> GetByBillingRecordIdAsync(
        int billingRecordId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Obtiene el último intento de un registro.
    /// </summary>
    Task<SubmissionAttemptDto?> GetLastAttemptAsync(
        int billingRecordId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Obtiene los intentos fallidos de un registro.
    /// </summary>
    Task<List<SubmissionAttemptDto>> GetFailedAttemptsAsync(
        int billingRecordId,
        CancellationToken cancellationToken);
}

/// <summary>
/// DTO de un intento de envío (agnóstico a Entity Framework).
/// </summary>
public record SubmissionAttemptDto(
    Guid Id,
    int Número,
    string Estado,
    DateTime FechaEnvío,
    DateTime? FechaRespuesta,
    string? CódigoRespuesta,
    string? DescripciónRespuesta,
    int? DuraciónMs,
    string? SubmissionIdAEAT
);
