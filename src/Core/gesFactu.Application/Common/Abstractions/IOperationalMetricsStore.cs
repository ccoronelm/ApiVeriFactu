namespace gesFactu.Application.Common.Abstractions;

public sealed record OperationalMetricsSnapshot(
    int PendingOutbox,
    DateTime? OldestPendingOutboxUtc,
    int UnreviewedDeadLetters,
    int SubmissionFailuresLastHour,
    int SubmissionSuccessLastHour,
    int PendingSubmissionAttempts,
    DateTime GeneratedAtUtc);

public interface IOperationalMetricsStore
{
    Task<OperationalMetricsSnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default);
}
