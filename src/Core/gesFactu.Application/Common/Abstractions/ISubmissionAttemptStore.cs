namespace gesFactu.Application.Common.Abstractions;

/// <summary>
/// Puerto de auditoría persistente de cada intento de comunicación con AEAT.
/// </summary>
public interface ISubmissionAttemptStore
{
    Task<SubmissionAttemptDto> CreateAsync(
        int billingRecordId,
        int attemptNumber,
        string requestPayload,
        CancellationToken cancellationToken);

    Task MarkAsSuccessAsync(
        Guid attemptId,
        string responseCode,
        string? responseDescription,
        string? responsePayload,
        string? aeatSubmissionId,
        int durationMilliseconds,
        CancellationToken cancellationToken);

    Task MarkAsPermanentFailureAsync(
        Guid attemptId,
        string responseCode,
        string? responseDescription,
        string? responsePayload,
        string? notes,
        int durationMilliseconds,
        CancellationToken cancellationToken);

    Task MarkAsTransientFailureAsync(
        Guid attemptId,
        string responseCode,
        string? responseDescription,
        string? notes,
        int durationMilliseconds,
        CancellationToken cancellationToken);

    Task MarkAsCommunicationErrorAsync(
        Guid attemptId,
        string responseCode,
        string? responseDescription,
        string? responsePayload,
        string? notes,
        int durationMilliseconds,
        CancellationToken cancellationToken);

    Task<List<SubmissionAttemptDto>> GetByBillingRecordIdAsync(
        int billingRecordId,
        CancellationToken cancellationToken);

    Task<SubmissionAttemptDto?> GetLastAttemptAsync(
        int billingRecordId,
        CancellationToken cancellationToken);

    Task<List<SubmissionAttemptDto>> GetFailedAttemptsAsync(
        int billingRecordId,
        CancellationToken cancellationToken);
}

public record SubmissionAttemptDto(
    Guid Id,
    int Número,
    string Estado,
    DateTime FechaEnvío,
    DateTime? FechaRespuesta,
    string? CódigoRespuesta,
    string? DescripciónRespuesta,
    int? DuraciónMs,
    string? SubmissionIdAEAT,
    string? Notas);
