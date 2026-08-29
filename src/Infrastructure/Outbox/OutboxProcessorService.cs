using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using gesFactu.Application.Common.Abstractions;

namespace gesFactu.Infrastructure.Outbox;

/// <summary>
/// Servicio background que procesa mensajes del outbox con resiliencia avanzada.
/// 
/// Características:
/// - Exponential backoff con jitter para reintentos
/// - Circuit breaker para detectar cuando AEAT está caído
/// - Dead Letter Queue para mensajes irrecuperables
/// - Procesamiento por lotes
/// 
/// Flujo:
/// 1. Obtiene lotes de mensajes sin procesar
/// 2. Verifica circuit breaker (si está abierto, espera)
/// 3. Intenta enviar cada uno a AEAT mediante el gateway
/// 4. Si éxito: marca como procesado, resetea circuit breaker
/// 5. Si error permanente: mueve a DLQ
/// 6. Si error transiente: programa reintento con backoff exponencial
/// 7. Si max intentos alcanzado: mueve a DLQ
/// 
/// Este servicio es tolerante a fallos transientes y se reinicia automáticamente.
/// </summary>
public class OutboxProcessorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxProcessorService> _logger;
    private readonly OutboxProcessorOptions _options;

    public OutboxProcessorService(
        IServiceProvider serviceProvider,
        ILogger<OutboxProcessorService> logger,
        OutboxProcessorOptions? options = null)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new OutboxProcessorOptions();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox processor service iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var outboxStore = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
                var veriFactuGateway = scope.ServiceProvider.GetRequiredService<IVeriFactuGateway>();
                var deadLetterStore = scope.ServiceProvider.GetRequiredService<IDeadLetterStore>();

                // Verificar circuit breaker
                if (_options.CircuitBreaker.State == CircuitBreakerState.Open)
                {
                    // Circuit abierto, verificar si podemos intentar pasar a HalfOpen
                    if (_options.CircuitBreaker.AllowTestRequest())
                    {
                        _logger.LogInformation("Circuit breaker en HalfOpen, intentando una solicitud de prueba.");
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Circuit breaker abierto (AEAT parece estar caído). Reintentando en {DelayMs}ms.",
                            _options.ErrorDelayMilliseconds);

                        await Task.Delay(_options.ErrorDelayMilliseconds, stoppingToken);
                        continue;
                    }
                }

                var pendingMessages = await outboxStore.GetPendingMessagesAsync(
                    _options.BatchSize,
                    cancellationToken: stoppingToken);

                if (pendingMessages.Count == 0)
                {
                    // No hay mensajes, esperar antes de reintentar
                    await Task.Delay(_options.IdleDelayMilliseconds, stoppingToken);
                    continue;
                }

                _logger.LogInformation("Procesando {Count} mensajes del outbox.", pendingMessages.Count);

                foreach (var message in pendingMessages)
                {
                    await ProcessMessageAsync(message, outboxStore, veriFactuGateway, deadLetterStore, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Outbox processor service fue cancelado.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error no manejado en el procesador de outbox. Se reintenará en {DelayMs}ms.",
                    _options.ErrorDelayMilliseconds);

                try
                {
                    await Task.Delay(_options.ErrorDelayMilliseconds, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("Outbox processor service detenido.");
    }

    private async Task ProcessMessageAsync(
        gesFactu.Domain.Entities.OutboxMessage message,
        IOutboxStore outboxStore,
        IVeriFactuGateway veriFactuGateway,
        IDeadLetterStore deadLetterStore,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Procesando mensaje de outbox {MessageId} (CorrelationId: {CorrelationId}, intento {Attempt})",
                message.Id,
                message.CorrelationId,
                message.ProcessingAttempts + 1);

            // Deserializar payload
            var payload = JsonSerializer.Deserialize<VeriFactuSubmissionRequest>(
                message.Payload,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException($"No se pudo deserializar el payload del mensaje {message.Id}");

            // Enviar a AEAT
            var result = await veriFactuGateway.SubmitBillingRecordAsync(payload, cancellationToken);

            // Evaluar respuesta
            if (result.IsAccepted)
            {
                // Éxito: marcar como procesado y resetear circuit breaker
                await outboxStore.MarkAsProcessedAsync(message.Id, cancellationToken);
                _options.CircuitBreaker.RecordSuccess();

                _logger.LogInformation(
                    "Mensaje de outbox {MessageId} procesado exitosamente. SubmissionId: {SubmissionId}",
                    message.Id,
                    result.SubmissionId);
            }
            else if (result.ResponseCode.IsPermanent())
            {
                // Error permanente: no reintentar, mover a DLQ
                var failureReason = $"Error permanente de AEAT: {result.ResponseCode}";
                await deadLetterStore.MoveMessageToDlqAsync(
                    message.Id,
                    message.CorrelationId.ToString(),
                    message.Payload,
                    failureReason,
                    result.StatusDescription,
                    message.ProcessingAttempts + 1,
                    message.CreatedAt,
                    cancellationToken);

                // Marcar como procesado en outbox para no volver a intentar
                await outboxStore.MarkAsProcessedAsync(message.Id, cancellationToken);

                _logger.LogWarning(
                    "Mensaje de outbox {MessageId} rechazado permanentemente y movido a DLQ. Código: {Code}, Motivo: {Description}",
                    message.Id,
                    result.ResponseCode,
                    result.StatusDescription);
            }
            else if (result.ResponseCode.IsTransient())
            {
                // Error transiente: verifica si hemos alcanzado el máximo de intentos
                var nextAttempt = message.ProcessingAttempts + 1;

                if (nextAttempt >= _options.RetryPolicy.MaxAttempts)
                {
                    // Máximo de intentos alcanzado
                    var failureReason = $"Máximo de intentos alcanzado ({_options.RetryPolicy.MaxAttempts}). Último error: {result.ResponseCode}";
                    await deadLetterStore.MoveMessageToDlqAsync(
                        message.Id,
                        message.CorrelationId.ToString(),
                        message.Payload,
                        failureReason,
                        result.StatusDescription,
                        nextAttempt,
                        message.CreatedAt,
                        cancellationToken);

                    // Marcar como procesado en outbox
                    await outboxStore.MarkAsProcessedAsync(message.Id, cancellationToken);

                    _logger.LogWarning(
                        "Mensaje de outbox {MessageId} agotó intentos ({MaxAttempts}) y fue movido a DLQ.",
                        message.Id,
                        _options.RetryPolicy.MaxAttempts);
                }
                else
                {
                    // Aún hay intentos disponibles: registrar fallo y programar reintento
                    var delayMs = _options.RetryPolicy.CalculateDelayMilliseconds(message.ProcessingAttempts);
                    await outboxStore.MarkAsFailedAsync(message.Id, $"{result.ResponseCode}: {result.StatusDescription}", cancellationToken);

                    _logger.LogWarning(
                        "Mensaje de outbox {MessageId} falló temporalmente. Reintentará en {DelayMs}ms (intento {Attempt}/{MaxAttempts}). Código: {Code}",
                        message.Id,
                        delayMs,
                        nextAttempt,
                        _options.RetryPolicy.MaxAttempts,
                        result.ResponseCode);

                    // Registrar fallo en circuit breaker
                    _options.CircuitBreaker.RecordFailure();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Error al procesar mensaje de outbox {MessageId}. Intento {Attempt} de {MaxAttempts}.",
                message.Id,
                message.ProcessingAttempts + 1,
                _options.RetryPolicy.MaxAttempts);

            // Registrar el fallo para reintentar después
            await outboxStore.MarkAsFailedAsync(message.Id, ex.Message, cancellationToken);

            // Incrementar contador de fallos en circuit breaker
            _options.CircuitBreaker.RecordFailure();
        }
    }
}

/// <summary>
/// Opciones de configuración para el procesador de outbox.
/// </summary>
public class OutboxProcessorOptions
{
    /// <summary>
    /// Número de mensajes a procesar por lote. Default: 50.
    /// </summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>
    /// Milisegundos a esperar si no hay mensajes pendientes. Default: 5000 (5 segundos).
    /// </summary>
    public int IdleDelayMilliseconds { get; set; } = 5000;

    /// <summary>
    /// Milisegundos a esperar después de un error no manejado. Default: 10000 (10 segundos).
    /// </summary>
    public int ErrorDelayMilliseconds { get; set; } = 10000;

    /// <summary>
    /// Política de reintentos con backoff exponencial y jitter.
    /// </summary>
    public RetryPolicy RetryPolicy { get; set; } = new();

    /// <summary>
    /// Circuit breaker para detectar cuando AEAT está caído.
    /// </summary>
    public CircuitBreaker CircuitBreaker { get; set; } = new();
}
