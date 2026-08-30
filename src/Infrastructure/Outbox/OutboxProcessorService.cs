using System.Diagnostics;
using System.Text.Json;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Domain.Entities;
using gesFactu.Infrastructure.Integrations.VeriFactu;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace gesFactu.Infrastructure.Outbox;

/// <summary>
/// Worker de remisión AEAT basado en Transactional Outbox.
///
/// Cada mensaje se reclama con una concesión en SQL Server. Cada llamada externa
/// genera un SubmissionAttempt persistente antes de que el Outbox pueda cerrarse.
/// </summary>
public sealed class OutboxProcessorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxProcessorService> _logger;
    private readonly OutboxProcessorOptions _options;
    private readonly string _workerId =
        $"{Environment.MachineName}-{Guid.NewGuid():N}";

    public OutboxProcessorService(
        IServiceProvider serviceProvider,
        ILogger<OutboxProcessorService> logger,
        OutboxProcessorOptions? options = null)
    {
        _serviceProvider = serviceProvider ??
            throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ??
            throw new ArgumentNullException(nameof(logger));
        _options = options ?? new OutboxProcessorOptions();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Outbox processor iniciado. WorkerId={WorkerId}",
            _workerId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_options.CircuitBreaker.State == CircuitBreakerState.Open &&
                    !_options.CircuitBreaker.AllowTestRequest())
                {
                    await Task.Delay(
                        _options.ErrorDelayMilliseconds,
                        stoppingToken);
                    continue;
                }

                using var scope = _serviceProvider.CreateScope();

                var outboxStore =
                    scope.ServiceProvider.GetRequiredService<IOutboxStore>();
                var gateway =
                    scope.ServiceProvider.GetRequiredService<IVeriFactuGateway>();
                var deadLetterStore =
                    scope.ServiceProvider.GetRequiredService<IDeadLetterStore>();
                var attemptStore =
                    scope.ServiceProvider.GetRequiredService<ISubmissionAttemptStore>();
                var billingRepository =
                    scope.ServiceProvider.GetRequiredService<IBillingRecordRepository>();

                var messages = await outboxStore.ClaimPendingMessagesAsync(
                    _workerId,
                    _options.BatchSize,
                    TimeSpan.FromSeconds(_options.LeaseDurationSeconds),
                    stoppingToken);

                if (messages.Count == 0)
                {
                    await Task.Delay(
                        _options.IdleDelayMilliseconds,
                        stoppingToken);
                    continue;
                }

                foreach (var message in messages)
                {
                    stoppingToken.ThrowIfCancellationRequested();

                    await ProcessMessageAsync(
                        message,
                        outboxStore,
                        gateway,
                        deadLetterStore,
                        attemptStore,
                        billingRepository,
                        stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error no controlado en el Outbox processor. WorkerId={WorkerId}",
                    _workerId);

                try
                {
                    await Task.Delay(
                        _options.ErrorDelayMilliseconds,
                        stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        _logger.LogInformation(
            "Outbox processor detenido. WorkerId={WorkerId}",
            _workerId);
    }

    private async Task ProcessMessageAsync(
        OutboxMessage message,
        IOutboxStore outboxStore,
        IVeriFactuGateway gateway,
        IDeadLetterStore deadLetterStore,
        ISubmissionAttemptStore attemptStore,
        IBillingRecordRepository billingRepository,
        CancellationToken cancellationToken)
    {
        var lastAttempt = await attemptStore.GetLastAttemptAsync(
            message.AggregateId,
            cancellationToken);

        // Recuperación idempotente si el proceso cayó después de persistir
        // el resultado pero antes de cerrar el mensaje de Outbox.
        if (lastAttempt?.Estado == SubmissionAttemptStatus.Success.ToString())
        {
            if (!string.IsNullOrWhiteSpace(lastAttempt.SubmissionIdAEAT))
            {
                await billingRepository.UpdateSubmissionStatusAsync(
                    message.AggregateId,
                    lastAttempt.SubmissionIdAEAT,
                    cancellationToken);
            }

            await billingRepository.UpdateAeatStatusAsync(
                message.AggregateId,
                lastAttempt.CódigoRespuesta == "AceptadoConErrores"
                    ? "AceptadoConErrores"
                    : "Aceptado",
                cancellationToken);

            await outboxStore.MarkAsProcessedAsync(
                message.Id,
                cancellationToken);

            return;
        }

        if (lastAttempt?.Estado == SubmissionAttemptStatus.PermanentFailure.ToString())
        {
            await FinalizePermanentFailureRecoveryAsync(
                message,
                lastAttempt,
                outboxStore,
                deadLetterStore,
                billingRepository,
                cancellationToken);

            return;
        }

        // Un Pending heredado de una caída representa un resultado incierto.
        // Lo cerramos como CommunicationError antes de iniciar otro intento.
        if (lastAttempt?.Estado == SubmissionAttemptStatus.Pending.ToString())
        {
            await attemptStore.MarkAsCommunicationErrorAsync(
                lastAttempt.Id,
                "RECOVERED_PENDING",
                "El proceso terminó antes de persistir el resultado del intento.",
                responsePayload: null,
                notes:
                    "Resultado externo incierto. El siguiente reintento puede recibir un duplicado AEAT.",
                durationMilliseconds: 0,
                cancellationToken: cancellationToken);

            lastAttempt = await attemptStore.GetLastAttemptAsync(
                message.AggregateId,
                cancellationToken);
        }

        var attemptNumber = (lastAttempt?.Número ?? 0) + 1;

        var request = JsonSerializer.Deserialize<VeriFactuSubmissionRequest>(
            message.Payload,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })
            ?? throw new InvalidOperationException(
                $"No se pudo deserializar el payload del Outbox {message.Id}.");

        var attempt = await attemptStore.CreateAsync(
            message.AggregateId,
            attemptNumber,
            message.Payload,
            cancellationToken);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await gateway.SubmitBillingRecordAsync(
                request,
                cancellationToken);

            stopwatch.Stop();
            var duration = ToMilliseconds(stopwatch);

            if (result.IsAccepted)
            {
                await attemptStore.MarkAsSuccessAsync(
                    attempt.Id,
                    result.ErrorCode ?? result.RecordStatus ?? result.StatusCode,
                    result.StatusDescription,
                    result.RawResponsePayload,
                    result.SubmissionId,
                    duration,
                    cancellationToken);

                // Para una respuesta aceptada, el parser exige CSV.
                await billingRepository.UpdateSubmissionStatusAsync(
                    message.AggregateId,
                    result.SubmissionId!,
                    cancellationToken);

                await billingRepository.UpdateAeatStatusAsync(
                    message.AggregateId,
                    result.RecordStatus == "AceptadoConErrores"
                        ? "AceptadoConErrores"
                        : "Aceptado",
                    cancellationToken);

                // Esta última escritura guarda también los cambios del BillingRecord
                // porque todos los stores comparten el mismo DbContext del scope.
                await outboxStore.MarkAsProcessedAsync(
                    message.Id,
                    cancellationToken);

                _options.CircuitBreaker.RecordSuccess();
                return;
            }

            if (result.IsDuplicate &&
                result.DuplicateRecordStatus is "Correcta" or "AceptadaConErrores")
            {
                var reconciledStatus =
                    result.DuplicateRecordStatus == "AceptadaConErrores"
                        ? "AceptadoConErroresPorDuplicadoAEAT"
                        : "AceptadoPorDuplicadoAEAT";

                var reconciliationDescription =
                    result.StatusDescription +
                    " | Reconciliado como éxito: AEAT confirma que el registro duplicado ya existe " +
                    $"con estado {result.DuplicateRecordStatus}." +
                    (string.IsNullOrWhiteSpace(result.DuplicateRequestId)
                        ? string.Empty
                        : $" IdPeticionRegistroDuplicado={result.DuplicateRequestId}.");

                await attemptStore.MarkAsSuccessAsync(
                    attempt.Id,
                    result.ErrorCode ?? "3000",
                    reconciliationDescription,
                    result.RawResponsePayload,
                    aeatSubmissionId: null,
                    durationMilliseconds: duration,
                    cancellationToken: cancellationToken);

                await billingRepository.UpdateAeatStatusAsync(
                    message.AggregateId,
                    reconciledStatus,
                    cancellationToken);

                await outboxStore.MarkAsProcessedAsync(
                    message.Id,
                    cancellationToken);

                _options.CircuitBreaker.RecordSuccess();

                _logger.LogWarning(
                    "Outbox {MessageId} reconciliado por duplicado AEAT. EstadoRegistroDuplicado={DuplicateStatus}, IdPeticion={DuplicateRequestId}",
                    message.Id,
                    result.DuplicateRecordStatus,
                    result.DuplicateRequestId ?? "(sin id)");

                return;
            }

            if (result.ResponseCode.IsTransient())
            {
                await attemptStore.MarkAsTransientFailureAsync(
                    attempt.Id,
                    result.ErrorCode ?? result.StatusCode,
                    result.StatusDescription,
                    result.AdditionalDetails,
                    duration,
                    cancellationToken);

                await ScheduleOrDeadLetterAsync(
                    message,
                    attemptNumber,
                    $"{result.ResponseCode}: {result.StatusDescription}",
                    result.RawResponsePayload,
                    outboxStore,
                    deadLetterStore,
                    billingRepository,
                    cancellationToken);

                _options.CircuitBreaker.RecordFailure();
                return;
            }

            await attemptStore.MarkAsPermanentFailureAsync(
                attempt.Id,
                result.ErrorCode ?? result.StatusCode,
                result.StatusDescription,
                result.RawResponsePayload,
                result.AdditionalDetails,
                duration,
                cancellationToken);

            var localStatus = result.IsDuplicate
                ? $"DuplicadoAEAT:{result.DuplicateRecordStatus ?? "Desconocido"}"
                : $"Rechazado:{result.ErrorCode ?? result.StatusCode}";

            await billingRepository.UpdateAeatStatusAsync(
                message.AggregateId,
                localStatus,
                cancellationToken);

            await MoveToDeadLetterAndCloseAsync(
                message,
                attemptNumber,
                $"Respuesta permanente AEAT: {result.ResponseCode}",
                result.StatusDescription,
                outboxStore,
                deadLetterStore,
                cancellationToken);
        }
        catch (VeriFactuSoapFaultException ex)
        {
            stopwatch.Stop();
            var duration = ToMilliseconds(stopwatch);

            await attemptStore.MarkAsPermanentFailureAsync(
                attempt.Id,
                "SOAP_FAULT",
                ex.Message,
                ex.RawResponsePayload,
                "SOAP Fault devuelto por AEAT.",
                duration,
                cancellationToken);

            await billingRepository.UpdateAeatStatusAsync(
                message.AggregateId,
                "ErrorSOAP",
                cancellationToken);

            await MoveToDeadLetterAndCloseAsync(
                message,
                attemptNumber,
                "SOAP Fault AEAT",
                ex.Message,
                outboxStore,
                deadLetterStore,
                cancellationToken);
        }
        catch (VeriFactuCommunicationException ex)
        {
            stopwatch.Stop();
            var duration = ToMilliseconds(stopwatch);

            await attemptStore.MarkAsCommunicationErrorAsync(
                attempt.Id,
                ex.IsTransient ? "COMM_TRANSIENT" : "COMM_PERMANENT",
                ex.Message,
                responsePayload: null,
                notes: ex.IsTransient
                    ? "Error técnico reintentable."
                    : "Error técnico no reintentable.",
                durationMilliseconds: duration,
                cancellationToken: cancellationToken);

            if (ex.IsTransient)
            {
                await ScheduleOrDeadLetterAsync(
                    message,
                    attemptNumber,
                    ex.Message,
                    lastErrorResponse: null,
                    outboxStore: outboxStore,
                    deadLetterStore: deadLetterStore,
                    billingRepository: billingRepository,
                    cancellationToken: cancellationToken);

                _options.CircuitBreaker.RecordFailure();
            }
            else
            {
                await billingRepository.UpdateAeatStatusAsync(
                    message.AggregateId,
                    "ErrorTecnicoPermanente",
                    cancellationToken);

                await MoveToDeadLetterAndCloseAsync(
                    message,
                    attemptNumber,
                    "Error técnico no reintentable",
                    ex.Message,
                    outboxStore,
                    deadLetterStore,
                    cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // No intentamos escribir usando un token cancelado.
            // La concesión expirará y el Pending será recuperado en el siguiente arranque.
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var duration = ToMilliseconds(stopwatch);

            await attemptStore.MarkAsCommunicationErrorAsync(
                attempt.Id,
                "UNEXPECTED",
                ex.Message,
                responsePayload: null,
                notes: ex.GetType().Name,
                durationMilliseconds: duration,
                cancellationToken: cancellationToken);

            await ScheduleOrDeadLetterAsync(
                message,
                attemptNumber,
                $"Error inesperado: {ex.Message}",
                lastErrorResponse: null,
                outboxStore: outboxStore,
                deadLetterStore: deadLetterStore,
                billingRepository: billingRepository,
                cancellationToken: cancellationToken);

            _options.CircuitBreaker.RecordFailure();
        }
    }

    private async Task ScheduleOrDeadLetterAsync(
        OutboxMessage message,
        int attemptNumber,
        string error,
        string? lastErrorResponse,
        IOutboxStore outboxStore,
        IDeadLetterStore deadLetterStore,
        IBillingRecordRepository billingRepository,
        CancellationToken cancellationToken)
    {
        if (attemptNumber >= _options.RetryPolicy.MaxAttempts)
        {
            await billingRepository.UpdateAeatStatusAsync(
                message.AggregateId,
                "ErrorEnvioAgotado",
                cancellationToken);

            await MoveToDeadLetterAndCloseAsync(
                message,
                attemptNumber,
                $"Máximo de intentos alcanzado ({_options.RetryPolicy.MaxAttempts})",
                lastErrorResponse ?? error,
                outboxStore,
                deadLetterStore,
                cancellationToken);

            return;
        }

        var delayMs = _options.RetryPolicy.CalculateDelayMilliseconds(
            Math.Max(0, attemptNumber - 1));

        await outboxStore.ScheduleRetryAsync(
            message.Id,
            error,
            DateTime.UtcNow.AddMilliseconds(delayMs),
            cancellationToken);
    }

    private static async Task MoveToDeadLetterAndCloseAsync(
        OutboxMessage message,
        int attemptNumber,
        string reason,
        string? lastErrorResponse,
        IOutboxStore outboxStore,
        IDeadLetterStore deadLetterStore,
        CancellationToken cancellationToken)
    {
        await deadLetterStore.MoveMessageToDlqAsync(
            message.Id,
            message.CorrelationId.ToString(),
            message.Payload,
            reason,
            lastErrorResponse,
            attemptNumber,
            message.CreatedAt,
            cancellationToken);

        await outboxStore.MarkAsProcessedAsync(
            message.Id,
            cancellationToken);
    }

    private static async Task FinalizePermanentFailureRecoveryAsync(
        OutboxMessage message,
        SubmissionAttemptDto lastAttempt,
        IOutboxStore outboxStore,
        IDeadLetterStore deadLetterStore,
        IBillingRecordRepository billingRepository,
        CancellationToken cancellationToken)
    {
        await billingRepository.UpdateAeatStatusAsync(
            message.AggregateId,
            "Rechazado",
            cancellationToken);

        await deadLetterStore.MoveMessageToDlqAsync(
            message.Id,
            message.CorrelationId.ToString(),
            message.Payload,
            "Recuperación de un intento permanente ya persistido.",
            lastAttempt.DescripciónRespuesta,
            lastAttempt.Número,
            message.CreatedAt,
            cancellationToken);

        await outboxStore.MarkAsProcessedAsync(
            message.Id,
            cancellationToken);
    }

    private static int ToMilliseconds(Stopwatch stopwatch)
        => (int)Math.Min(
            int.MaxValue,
            Math.Max(0, stopwatch.ElapsedMilliseconds));
}

public sealed class OutboxProcessorOptions
{
    public int BatchSize { get; set; } = 5;
    public int LeaseDurationSeconds { get; set; } = 600;
    public int IdleDelayMilliseconds { get; set; } = 5000;
    public int ErrorDelayMilliseconds { get; set; } = 10000;
    public RetryPolicy RetryPolicy { get; set; } = new();
    public CircuitBreaker CircuitBreaker { get; set; } = new();
}
