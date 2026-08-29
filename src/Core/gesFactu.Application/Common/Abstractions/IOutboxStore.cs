using gesFactu.Domain.Entities;

namespace gesFactu.Application.Common.Abstractions;

/// <summary>
/// Puerto para la persistencia del Transactional Outbox.
/// 
/// El procesador background accede al outbox a través de este puerto
/// para obtener mensajes sin procesar, marcarlos como procesados y
/// registrar intentos fallidos.
/// </summary>
public interface IOutboxStore
{
    /// <summary>
    /// Obtiene un lote de mensajes pendientes de procesar.
    /// </summary>
    /// <param name="batchSize">Número máximo de mensajes a retornar.</param>
    /// <param name="maxAttempts">Máximo número de intentos permitidos antes de descartar.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Lista de mensajes sin procesar.</returns>
    Task<IReadOnlyList<OutboxMessage>> GetPendingMessagesAsync(
        int batchSize = 50,
        int maxAttempts = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca un mensaje como procesado exitosamente.
    /// </summary>
    /// <param name="messageId">ID del mensaje en el outbox.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task MarkAsProcessedAsync(long messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra un intento fallido de procesamiento.
    /// </summary>
    /// <param name="messageId">ID del mensaje en el outbox.</param>
    /// <param name="errorMessage">Descripción del error.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task MarkAsFailedAsync(long messageId, string errorMessage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene un mensaje por su ID de correlación.
    /// Usado para detectar duplicados en reintentos.
    /// </summary>
    /// <param name="correlationId">GUID único del mensaje.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>El mensaje si existe, null en caso contrario.</returns>
    Task<OutboxMessage?> GetByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Agrega un nuevo mensaje de outbox.
    /// Se usa internamente por los handlers de commands.
    /// </summary>
    /// <param name="message">Mensaje a agregar.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}
