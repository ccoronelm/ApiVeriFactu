using System.Data;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace gesFactu.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación PostgreSQL del Transactional Outbox.
/// </summary>
public class OutboxStore : IOutboxStore
{
    private readonly ApplicationDbContext _context;

    public OutboxStore(ApplicationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingMessagesAsync(
        int batchSize = 50,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var messages = await _context.OutboxMessages
            .Where(m =>
                !m.IsProcessed &&
                (m.NextAttemptAt == null || m.NextAttemptAt <= now) &&
                (m.LockedUntil == null || m.LockedUntil <= now))
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .Take(batchSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return messages.AsReadOnly();
    }

    public async Task<IReadOnlyList<OutboxMessage>> ClaimPendingMessagesAsync(
        string workerId,
        int batchSize,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);

        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        if (leaseDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));

        var now = DateTime.UtcNow;
        var lockedUntil = now.Add(leaseDuration);

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        // PostgreSQL: varios workers pueden reclamar lotes sin pisarse.
        var messages = await _context.OutboxMessages
            .FromSqlInterpolated($$"""
                SELECT *
                FROM "OutboxMessages"
                WHERE "IsProcessed" = FALSE
                  AND ("NextAttemptAt" IS NULL OR "NextAttemptAt" <= {{now}})
                  AND ("LockedUntil" IS NULL OR "LockedUntil" <= {{now}})
                ORDER BY "CreatedAt", "Id"
                FOR UPDATE SKIP LOCKED
                LIMIT {{batchSize}}
                """)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            message.LockedBy = workerId;
            message.LockedUntil = lockedUntil;
        }

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return messages.AsReadOnly();
    }

    public async Task MarkAsProcessedAsync(
        long messageId,
        CancellationToken cancellationToken = default)
    {
        var message = await _context.OutboxMessages.FindAsync(
            new object?[] { messageId },
            cancellationToken: cancellationToken);

        if (message is null)
            return;

        message.IsProcessed = true;
        message.ProcessedAt = DateTime.UtcNow;
        message.LastProcessingError = null;
        message.NextAttemptAt = null;
        message.LockedBy = null;
        message.LockedUntil = null;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAsFailedAsync(
        long messageId,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        await ScheduleRetryAsync(
            messageId,
            errorMessage,
            DateTime.UtcNow,
            cancellationToken);
    }

    public async Task ScheduleRetryAsync(
        long messageId,
        string errorMessage,
        DateTime nextAttemptAtUtc,
        CancellationToken cancellationToken = default)
    {
        var message = await _context.OutboxMessages.FindAsync(
            new object?[] { messageId },
            cancellationToken: cancellationToken);

        if (message is null)
            return;

        message.ProcessingAttempts++;
        message.LastProcessingError = errorMessage;
        message.NextAttemptAt = nextAttemptAtUtc.Kind == DateTimeKind.Utc
            ? nextAttemptAtUtc
            : nextAttemptAtUtc.ToUniversalTime();
        message.LockedBy = null;
        message.LockedUntil = null;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<OutboxMessage?> GetByCorrelationIdAsync(
        Guid correlationId,
        CancellationToken cancellationToken = default)
        => _context.OutboxMessages
            .FirstOrDefaultAsync(
                m => m.CorrelationId == correlationId,
                cancellationToken);

    public Task<bool> ExistsForAggregateEventAsync(
        string aggregateType,
        int aggregateId,
        string eventType,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

        return _context.OutboxMessages.AnyAsync(
            m => m.AggregateType == aggregateType
                 && m.AggregateId == aggregateId
                 && m.EventType == eventType,
            cancellationToken);
    }

    public async Task AddAsync(
        OutboxMessage message,
        CancellationToken cancellationToken = default)
    {
        _context.OutboxMessages.Add(message);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
