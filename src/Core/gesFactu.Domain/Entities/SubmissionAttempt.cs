namespace gesFactu.Domain.Entities;

/// <summary>
/// Representa un intento de envío de un registro a AEAT.
/// Auditoría completa de cada comunicación con el AEAT.
/// </summary>
public class SubmissionAttempt
{
    public Guid Id { get; set; }

    /// <summary>
    /// ID del registro de facturación (FK).
    /// </summary>
    public int BillingRecordId { get; set; }

    /// <summary>
    /// Número secuencial de intento (1, 2, 3, ...).
    /// </summary>
    public int AttemptNumber { get; set; }

    /// <summary>
    /// Payload enviado a AEAT (JSON serializado).
    /// </summary>
    public string RequestPayload { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp en que se realizó el envío.
    /// </summary>
    public DateTime SubmittedAt { get; set; }

    /// <summary>
    /// Código HTTP o código AEAT de respuesta.
    /// </summary>
    public string? ResponseCode { get; set; }

    /// <summary>
    /// Descripción de la respuesta (error o success message).
    /// </summary>
    public string? ResponseDescription { get; set; }

    /// <summary>
    /// XML o JSON completo de la respuesta de AEAT (puede ser large).
    /// </summary>
    public string? ResponsePayload { get; set; }

    /// <summary>
    /// SubmissionId retornado por AEAT (si la solicitud fue exitosa).
    /// </summary>
    public string? AeatSubmissionId { get; set; }

    /// <summary>
    /// Estado del intento.
    /// </summary>
    public SubmissionAttemptStatus Status { get; set; }

    /// <summary>
    /// Timestamp en que se recibió la respuesta.
    /// </summary>
    public DateTime? RespondedAt { get; set; }

    /// <summary>
    /// Duración de la comunicación en milisegundos.
    /// </summary>
    public int? DurationMilliseconds { get; set; }

    /// <summary>
    /// Notas adicionales (ej: error details, retry reason, etc).
    /// </summary>
    public string? Notes { get; set; }

    // Navegación
    public virtual BillingRecord? BillingRecord { get; set; }
}

/// <summary>
/// Estados posibles de un intento de envío a AEAT.
/// </summary>
public enum SubmissionAttemptStatus
{
    /// <summary>
    /// Enviado y esperando respuesta.
    /// </summary>
    Pending,

    /// <summary>
    /// Respuesta recibida y procesada exitosamente.
    /// </summary>
    Success,

    /// <summary>
    /// Error permanente (no se reintentará).
    /// </summary>
    PermanentFailure,

    /// <summary>
    /// Error transitorio (se reintentará después).
    /// </summary>
    TransientFailure,

    /// <summary>
    /// Timeout o error de comunicación.
    /// </summary>
    CommunicationError
}
