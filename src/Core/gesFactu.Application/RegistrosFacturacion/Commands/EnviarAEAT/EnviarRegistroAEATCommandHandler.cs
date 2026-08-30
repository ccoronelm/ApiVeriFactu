using MediatR;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using gesFactu.Application.Common;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Domain.Entities;

namespace gesFactu.Application.RegistrosFacturacion.Commands.EnviarAEAT;

/// <summary>
/// Handler para enviar un registro de facturación a AEAT.
/// 
/// Implementa Transactional Outbox:
/// 1. Obtención del registro desde repositorio
/// 2. Validación de precondiciones (debe estar pendiente, con hash)
/// 3. Mapeo a solicitud AEAT
/// 4. Creación de mensaje en outbox (atómicamente con actualización de estado)
/// 5. El procesador background maneja el envío a AEAT y reintentos
/// 
/// Ventajas:
/// - La operación es idempotente (no hay duplicados por reintentos)
/// - La entrega a AEAT es confiable (se reintentar si falla)
/// - Sin bloqueos mientras se espera respuesta de AEAT
/// - Separación clara entre cambio de estado y comunicación externa
/// 
/// Ref: /VERIFACTU - Reglamentación de envío y estados
/// </summary>
public sealed class EnviarRegistroAEATCommandHandler
    : IRequestHandler<EnviarRegistroAEATCommand, Result<EnviarRegistroAEATResponse>>
{
    private readonly IBillingRecordRepository _repository;
    private readonly IOutboxStore _outboxStore;
    private readonly IApplicationDbContext _dbContext;
    private readonly IRegistroAltaXmlBuilder _xmlBuilder;
    private readonly ILogger<EnviarRegistroAEATCommandHandler> _logger;

    public EnviarRegistroAEATCommandHandler(
        IBillingRecordRepository repository,
        IOutboxStore outboxStore,
        IApplicationDbContext dbContext,
        IRegistroAltaXmlBuilder xmlBuilder,
        ILogger<EnviarRegistroAEATCommandHandler> logger)
    {
        _repository = repository;
        _outboxStore = outboxStore;
        _dbContext = dbContext;
        _xmlBuilder = xmlBuilder;
        _logger = logger;
    }

    public async Task<Result<EnviarRegistroAEATResponse>> Handle(
        EnviarRegistroAEATCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Iniciando envío de registro {BillingRecordId} a AEAT",
            command.BillingRecordId);

        // 1. Obtener registro
        var record = await _repository.GetByIdAsync(command.BillingRecordId, cancellationToken);

        if (record == null)
        {
            _logger.LogWarning("Registro no encontrado: {BillingRecordId}", command.BillingRecordId);
            return new Result<EnviarRegistroAEATResponse>.NotFoundError(
                "BillingRecord",
                command.BillingRecordId.ToString());
        }

        // 2. Validar precondiciones
        if (record.IsSubmitted)
        {
            _logger.LogWarning(
                "Registro ya fue enviado a AEAT: {BillingRecordId}, SubmissionId: {SubmissionId}",
                command.BillingRecordId,
                record.AeatSubmissionId);

            return new Result<EnviarRegistroAEATResponse>.ConflictError(
                "El registro ya fue enviado a AEAT");
        }

        if (string.IsNullOrWhiteSpace(record.ComputedHash))
        {
            _logger.LogWarning(
                "Registro sin hash calculado: {BillingRecordId}",
                command.BillingRecordId);

            return new Result<EnviarRegistroAEATResponse>.DomainError(
                "NO_HASH",
                "El registro debe tener un hash calculado antes de enviar a AEAT");
        }

        try
        {
            // 3. Mapear a solicitud AEAT (el XML lo genera Infrastructure via IRegistroAltaXmlBuilder)
            var request = BillingRecordToVeriFactuMapper.MapToSubmissionRequest(record, _xmlBuilder);

            // Crear correlationId único para este intento
            var correlationId = Guid.NewGuid();

            // 4. Marcar registro como en envío y crear mensaje de outbox (atómicamente)
            record.MarkAsSubmitted(correlationId.ToString());

            var outboxMessage = new OutboxMessage
            {
                CorrelationId = correlationId,
                AggregateId = record.Id,
                AggregateType = "BillingRecord",
                EventType = "BillingRecordSubmittedToAEAT",
                Payload = JsonSerializer.Serialize(request),
                CreatedAt = DateTime.UtcNow,
                IsProcessed = false,
                ProcessingAttempts = 0
            };

            _dbContext.AddOutboxMessage(outboxMessage);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Mensaje de outbox creado para registro {BillingRecordId}, CorrelationId: {CorrelationId}",
                command.BillingRecordId,
                correlationId);

            // Retornar respuesta inmediata al cliente
            // El procesamiento real ocurre en background via OutboxProcessorService
            return new Result<EnviarRegistroAEATResponse>.SuccessWithValue(
                new EnviarRegistroAEATResponse
                {
                    BillingRecordId = command.BillingRecordId,
                    AeatSubmissionId = correlationId.ToString(),
                    IsAccepted = false, // No sabemos aún, será actualizado por el processor
                    Status = "PENDING",
                    StatusDescription = "Enviando a AEAT (procesamiento en background)",
                    Details = "El registro se procesará asincronamente. Use el CorrelationId para rastrear el estado."
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error inesperado al preparar envío de registro {BillingRecordId}",
                command.BillingRecordId);

            return new Result<EnviarRegistroAEATResponse>.ExternalServiceError(
                "Error al preparar envío a AEAT",
                ex.Message);
        }
    }
}
