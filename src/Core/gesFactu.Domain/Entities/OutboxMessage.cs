namespace gesFactu.Domain.Entities;

/// <summary>
/// Mensaje de outbox para garantizar entrega confiable a AEAT.
/// 
/// Implementa el patrón Transactional Outbox:
/// - Se crea atomicamente con BillingRecord en la misma transacción
/// - Se procesa asincronamente por un worker background
/// - Garantiza idempotencia y no duplicados
/// 
/// Ref: /VERIFACTU - Garantía de entrega de registros
/// </summary>
public class OutboxMessage
{
    /// <summary>
    /// Identificador único del mensaje.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// ID correlativo para rastrear este envío a través del sistema.
    /// Única en la tabla para detectar duplicados.
    /// </summary>
    public required Guid CorrelationId { get; set; }

    /// <summary>
    /// ID del agregado (BillingRecord) relacionado.
    /// </summary>
    public required int AggregateId { get; set; }

    /// <summary>
    /// Tipo del agregado. Siempre "BillingRecord" en gesFactu.
    /// </summary>
    public required string AggregateType { get; set; } = "BillingRecord";

    /// <summary>
    /// Tipo de evento. Ej: "BillingRecordSubmittedToAEAT".
    /// </summary>
    public required string EventType { get; set; }

    /// <summary>
    /// Payload del evento serializado a JSON.
    /// Contiene los datos necesarios para reintentarlo.
    /// </summary>
    public required string Payload { get; set; }

    /// <summary>
    /// Timestamp de creación (UTC).
    /// </summary>
    public required DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp de procesamiento exitoso (UTC).
    /// Null si no ha sido procesado.
    /// </summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// Número de intentos de procesamiento realizados.
    /// </summary>
    public int ProcessingAttempts { get; set; }

    /// <summary>
    /// Último error de procesamiento (si aplica).
    /// </summary>
    public string? LastProcessingError { get; set; }

    /// <summary>
    /// Indica si el mensaje fue procesado exitosamente.
    /// </summary>
    public bool IsProcessed { get; set; }
}
