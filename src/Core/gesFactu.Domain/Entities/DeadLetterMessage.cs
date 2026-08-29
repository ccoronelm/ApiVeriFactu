namespace gesFactu.Domain.Entities;

/// <summary>
/// Representa un mensaje que ha agotado sus reintentos o tiene un error permanente.
/// Se almacena en la Dead Letter Queue para análisis y potencial re-procesamiento manual.
/// </summary>
public class DeadLetterMessage
{
    public Guid Id { get; set; }

    /// <summary>
    /// ID del mensaje de outbox original.
    /// </summary>
    public long OriginalMessageId { get; set; }

    /// <summary>
    /// Correlation ID del registro de facturación para auditoría.
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Payload serializado que no se pudo entregar.
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// Razón por la que se movió a DLQ (ej: "Máximo de intentos alcanzado", "Error permanente de AEAT", etc).
    /// </summary>
    public string FailureReason { get; set; } = string.Empty;

    /// <summary>
    /// Última respuesta de error recibida de AEAT (si aplica).
    /// </summary>
    public string? LastErrorResponse { get; set; }

    /// <summary>
    /// Número de intentos que se realizaron antes de enviar a DLQ.
    /// </summary>
    public int ProcessingAttempts { get; set; }

    /// <summary>
    /// Cuándo se creó este mensaje en el outbox original.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Cuándo se movió a DLQ.
    /// </summary>
    public DateTime MovedToDlqAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Indica si el mensaje ha sido revisado/procesado manualmente.
    /// </summary>
    public bool IsReviewed { get; set; }

    /// <summary>
    /// Notas de auditoría o resolución manual (si las hay).
    /// </summary>
    public string? ReviewNotes { get; set; }
}
