using Microsoft.EntityFrameworkCore;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Domain.Entities;

namespace gesFactu.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación EF Core del almacén de outbox.
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
        var messages = await _context.OutboxMessages
            .Where(m => !m.IsProcessed)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        return messages.AsReadOnly();
    }

    public async Task MarkAsProcessedAsync(long messageId, CancellationToken cancellationToken = default)
    {
        var message = await _context.OutboxMessages.FindAsync(new object?[] { messageId }, cancellationToken: cancellationToken);

        if (message is not null)
        {
            message.IsProcessed = true;
            message.ProcessedAt = DateTime.UtcNow;
            message.LastProcessingError = null;

            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkAsFailedAsync(long messageId, string errorMessage, CancellationToken cancellationToken = default)
    {
        var message = await _context.OutboxMessages.FindAsync(new object?[] { messageId }, cancellationToken: cancellationToken);

        if (message is not null)
        {
            message.ProcessingAttempts++;
            message.LastProcessingError = errorMessage;

            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<OutboxMessage?> GetByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken = default)
    {
        return await _context.OutboxMessages
            .FirstOrDefaultAsync(m => m.CorrelationId == correlationId, cancellationToken);
    }

    public async Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        _context.OutboxMessages.Add(message);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
