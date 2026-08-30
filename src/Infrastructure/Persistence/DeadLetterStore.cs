using Microsoft.EntityFrameworkCore;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Domain.Entities;

namespace gesFactu.Infrastructure.Persistence;

/// <summary>
/// Implementación de IDeadLetterStore usando EF Core.
/// Gestiona mensajes que han agotado sus reintentos.
/// </summary>
public class DeadLetterStore : IDeadLetterStore
{
    private readonly ApplicationDbContext _dbContext;

    public DeadLetterStore(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task MoveMessageToDlqAsync(
        long originalMessageId,
        string correlationId,
        string payload,
        string failureReason,
        string? lastErrorResponse,
        int processingAttempts,
        DateTime createdAt,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.DeadLetterMessages
            .FirstOrDefaultAsync(
                x => x.CorrelationId == correlationId,
                cancellationToken);

        if (existing is not null)
        {
            // Idempotencia: una recuperación tras caída del worker no debe duplicar DLQ.
            existing.FailureReason = failureReason;
            existing.LastErrorResponse = lastErrorResponse;
            existing.ProcessingAttempts = Math.Max(
                existing.ProcessingAttempts,
                processingAttempts);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var dlqMessage = new DeadLetterMessage
        {
            Id = Guid.NewGuid(),
            OriginalMessageId = originalMessageId,
            CorrelationId = correlationId,
            Payload = payload,
            FailureReason = failureReason,
            LastErrorResponse = lastErrorResponse,
            ProcessingAttempts = processingAttempts,
            CreatedAt = createdAt,
            MovedToDlqAt = DateTime.UtcNow,
            IsReviewed = false
        };

        _dbContext.DeadLetterMessages.Add(dlqMessage);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<DeadLetterMessage>> GetUnreviewedMessagesAsync(
        int pageSize,
        CancellationToken cancellationToken)
    {
        return await _dbContext.DeadLetterMessages
            .Where(x => !x.IsReviewed)
            .OrderBy(x => x.MovedToDlqAt)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public Task<DeadLetterMessage?> GetByIdAsync(
        Guid dlqMessageId,
        CancellationToken cancellationToken)
        => _dbContext.DeadLetterMessages
            .FirstOrDefaultAsync(
                x => x.Id == dlqMessageId,
                cancellationToken);

    public async Task MarkAsReviewedAsync(
        Guid dlqMessageId,
        string? notes,
        CancellationToken cancellationToken)
    {
        var message = await _dbContext.DeadLetterMessages
            .FirstOrDefaultAsync(x => x.Id == dlqMessageId, cancellationToken)
            ?? throw new InvalidOperationException($"Dead letter message {dlqMessageId} not found");

        message.IsReviewed = true;
        message.ReviewNotes = notes;

        _dbContext.DeadLetterMessages.Update(message);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
