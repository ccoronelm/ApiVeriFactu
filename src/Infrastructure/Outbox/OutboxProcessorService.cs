using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using gesFactu.Application.Common.Abstractions;

namespace gesFactu.Infrastructure.Outbox;

/// <summary>
/// Servicio background que procesa mensajes del outbox.
/// 
/// Flujo:
/// 1. Obtiene lotes de mensajes sin procesar
/// 2. Intenta enviar cada uno a AEAT mediante el gateway
/// 3. Marca como procesado si tiene éxito
/// 4. Registra el intento fallido con error si falla (reintentar más tarde)
/// 5. Respeta máximo de intentos para evitar loops infinitos
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

                var pendingMessages = await outboxStore.GetPendingMessagesAsync(
                    _options.BatchSize,
                    _options.MaxAttempts,
                    stoppingToken);

                if (pendingMessages.Count == 0)
                {
                    // No hay mensajes, esperar antes de reintentar
                    await Task.Delay(_options.IdleDelayMilliseconds, stoppingToken);
                    continue;
                }

                _logger.LogInformation("Procesando {Count} mensajes del outbox.", pendingMessages.Count);

                foreach (var message in pendingMessages)
                {
                    await ProcessMessageAsync(message, outboxStore, veriFactuGateway, stoppingToken);
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

            // Marcar como procesado
            await outboxStore.MarkAsProcessedAsync(message.Id, cancellationToken);

            _logger.LogInformation(
                "Mensaje de outbox {MessageId} procesado exitosamente. SubmissionId: {SubmissionId}",
                message.Id,
                result.SubmissionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Error al procesar mensaje de outbox {MessageId}. Intento {Attempt} de {MaxAttempts}.",
                message.Id,
                message.ProcessingAttempts + 1,
                _options.MaxAttempts);

            // Registrar el fallo para reintentar después
            await outboxStore.MarkAsFailedAsync(message.Id, ex.Message, cancellationToken);
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
    /// Número máximo de intentos antes de descartar un mensaje. Default: 5.
    /// </summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>
    /// Milisegundos a esperar si no hay mensajes pendientes. Default: 5000 (5 segundos).
    /// </summary>
    public int IdleDelayMilliseconds { get; set; } = 5000;

    /// <summary>
    /// Milisegundos a esperar después de un error no manejado. Default: 10000 (10 segundos).
    /// </summary>
    public int ErrorDelayMilliseconds { get; set; } = 10000;
}
