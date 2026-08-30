using gesFactu.Application.Common.Abstractions;
using gesFactu.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace gesFactu.Infrastructure.Persistence;

public sealed class OperationalMetricsStore : IOperationalMetricsStore
{
    private readonly ApplicationDbContext _dbContext;

    public OperationalMetricsStore(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<OperationalMetricsSnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var since = now.AddHours(-1);

        var pendingOutbox = await _dbContext.OutboxMessages
            .CountAsync(x => !x.IsProcessed, cancellationToken);

        var oldestPending = await _dbContext.OutboxMessages
            .Where(x => !x.IsProcessed)
            .OrderBy(x => x.CreatedAt)
            .Select(x => (DateTime?)x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var unreviewedDlq = await _dbContext.DeadLetterMessages
            .CountAsync(x => !x.IsReviewed, cancellationToken);

        var failures = await _dbContext.SubmissionAttempts
            .CountAsync(
                x => x.SubmittedAt >= since &&
                     x.Status != SubmissionAttemptStatus.Pending &&
                     x.Status != SubmissionAttemptStatus.Success,
                cancellationToken);

        var successes = await _dbContext.SubmissionAttempts
            .CountAsync(
                x => x.SubmittedAt >= since &&
                     x.Status == SubmissionAttemptStatus.Success,
                cancellationToken);

        var pendingAttempts = await _dbContext.SubmissionAttempts
            .CountAsync(
                x => x.Status == SubmissionAttemptStatus.Pending,
                cancellationToken);

        return new OperationalMetricsSnapshot(
            pendingOutbox,
            oldestPending,
            unreviewedDlq,
            failures,
            successes,
            pendingAttempts,
            now);
    }
}
