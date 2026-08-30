namespace gesFactu.Application.Common.Abstractions;

/// <summary>
/// Puerto para persistencia de mensajes en Dead Letter Queue.
/// </summary>
public interface IDeadLetterStore
{
    /// <summary>
    /// Mueve un mensaje de outbox a la Dead Letter Queue.
    /// </summary>
    Task MoveMessageToDlqAsync(
        long originalMessageId,
        string correlationId,
        string payload,
        string failureReason,
        string? lastErrorResponse,
        int processingAttempts,
        DateTime createdAt,
        CancellationToken cancellationToken);

    /// <summary>
    /// Obtiene los mensajes más antiguos en la DLQ que no han sido revisados.
    /// </summary>
    Task<List<gesFactu.Domain.Entities.DeadLetterMessage>> GetUnreviewedMessagesAsync(
        int pageSize,
        CancellationToken cancellationToken);

    Task<gesFactu.Domain.Entities.DeadLetterMessage?> GetByIdAsync(
        Guid dlqMessageId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Marca un mensaje en DLQ como revisado.
    /// </summary>
    Task MarkAsReviewedAsync(
        Guid dlqMessageId,
        string? notes,
        CancellationToken cancellationToken);
}
