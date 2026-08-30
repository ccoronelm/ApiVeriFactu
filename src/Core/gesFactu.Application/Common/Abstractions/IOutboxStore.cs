using gesFactu.Domain.Entities;

namespace gesFactu.Application.Common.Abstractions;

/// <summary>
/// Puerto de persistencia del Transactional Outbox.
/// </summary>
public interface IOutboxStore
{
    /// <summary>
    /// Lectura de pendientes disponible para consultas/tests.
    /// El worker debe usar ClaimPendingMessagesAsync.
    /// </summary>
    Task<IReadOnlyList<OutboxMessage>> GetPendingMessagesAsync(
        int batchSize = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reclama de forma exclusiva un lote durante una concesión temporal.
    /// Dos workers no deben recibir el mismo mensaje mientras la concesión siga vigente.
    /// </summary>
    Task<IReadOnlyList<OutboxMessage>> ClaimPendingMessagesAsync(
        string workerId,
        int batchSize,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    Task MarkAsProcessedAsync(
        long messageId,
        CancellationToken cancellationToken = default);

    Task MarkAsFailedAsync(
        long messageId,
        string errorMessage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra fallo transitorio y programa el próximo intento.
    /// </summary>
    Task ScheduleRetryAsync(
        long messageId,
        string errorMessage,
        DateTime nextAttemptAtUtc,
        CancellationToken cancellationToken = default);

    Task<OutboxMessage?> GetByCorrelationIdAsync(
        Guid correlationId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsForAggregateEventAsync(
        string aggregateType,
        int aggregateId,
        string eventType,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        OutboxMessage message,
        CancellationToken cancellationToken = default);
}
