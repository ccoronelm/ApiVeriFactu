namespace gesFactu.Domain.Entities;

/// <summary>
/// Mensaje de Transactional Outbox para una remisión a AEAT.
/// </summary>
public class OutboxMessage
{
    public long Id { get; set; }
    public required Guid CorrelationId { get; set; }
    public required int AggregateId { get; set; }
    public required string AggregateType { get; set; } = "BillingRecord";
    public required string EventType { get; set; }
    public required string Payload { get; set; }
    public required DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public int ProcessingAttempts { get; set; }
    public string? LastProcessingError { get; set; }
    public bool IsProcessed { get; set; }

    /// <summary>
    /// Próximo instante UTC en el que puede volver a intentarse.
    /// Null significa disponible inmediatamente.
    /// </summary>
    public DateTime? NextAttemptAt { get; set; }

    /// <summary>
    /// Identificador de la instancia/worker que ha reclamado temporalmente el mensaje.
    /// </summary>
    public string? LockedBy { get; set; }

    /// <summary>
    /// Fin UTC de la concesión. Permite recuperar mensajes tras caída del worker.
    /// </summary>
    public DateTime? LockedUntil { get; set; }
}
