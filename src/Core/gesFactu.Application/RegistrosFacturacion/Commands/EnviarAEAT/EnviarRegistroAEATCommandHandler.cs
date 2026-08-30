using MediatR;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using gesFactu.Application.Common;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Domain.Entities;

namespace gesFactu.Application.RegistrosFacturacion.Commands.EnviarAEAT;

/// <summary>
/// Prepara el envío de un registro a AEAT mediante Transactional Outbox.
///
/// Flujo:
/// 1. Carga y valida el registro.
/// 2. Serializa la preparación con la cadena fiscal del obligado.
/// 3. Si FechaHoraHusoGenRegistro está envejecida, refresca timestamp y huella
///    siempre que el RF siga siendo el último de la cadena.
/// 4. Genera y valida el XML RegistroAlta contra el XSD oficial.
/// 5. Persiste atomícamente el RF preparado y el mensaje de Outbox.
/// </summary>
public sealed class EnviarRegistroAEATCommandHandler
    : IRequestHandler<EnviarRegistroAEATCommand, Result<EnviarRegistroAEATResponse>>
{
    private readonly IBillingRecordRepository _repository;
    private readonly IApplicationDbContext _dbContext;
    private readonly IOutboxStore _outboxStore;
    private readonly IRegistroAltaXmlBuilder _xmlBuilder;
    private readonly IXmlSchemaValidator _xmlSchemaValidator;
    private readonly IHashCalculator _hashCalculator;
    private readonly ILogger<EnviarRegistroAEATCommandHandler> _logger;

    public EnviarRegistroAEATCommandHandler(
        IBillingRecordRepository repository,
        IApplicationDbContext dbContext,
        IOutboxStore outboxStore,
        IRegistroAltaXmlBuilder xmlBuilder,
        IXmlSchemaValidator xmlSchemaValidator,
        IHashCalculator hashCalculator,
        ILogger<EnviarRegistroAEATCommandHandler> logger)
    {
        _repository = repository;
        _dbContext = dbContext;
        _outboxStore = outboxStore;
        _xmlBuilder = xmlBuilder;
        _xmlSchemaValidator = xmlSchemaValidator;
        _hashCalculator = hashCalculator;
        _logger = logger;
    }

    public async Task<Result<EnviarRegistroAEATResponse>> Handle(
        EnviarRegistroAEATCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Preparando envío del registro {BillingRecordId} a AEAT",
            command.BillingRecordId);

        var record = await _repository.GetByIdAsync(
            command.BillingRecordId,
            cancellationToken);

        if (record is null)
        {
            return new Result<EnviarRegistroAEATResponse>.NotFoundError(
                "BillingRecord",
                command.BillingRecordId.ToString());
        }

        if (record.IsSubmitted)
        {
            return new Result<EnviarRegistroAEATResponse>.ConflictError(
                "El registro ya fue preparado/enviado a AEAT");
        }

        if (string.IsNullOrWhiteSpace(record.ComputedHash))
        {
            return new Result<EnviarRegistroAEATResponse>.DomainError(
                "NO_HASH",
                "El registro debe tener una huella calculada antes de enviarse a AEAT");
        }

        if (string.IsNullOrWhiteSpace(record.RegisterTimestamp))
        {
            return new Result<EnviarRegistroAEATResponse>.DomainError(
                "NO_REGISTER_TIMESTAMP",
                "El registro debe tener FechaHoraHusoGenRegistro persistida antes de enviarse a AEAT");
        }

        try
        {
            await using var transaction =
                await _dbContext.BeginTransactionAsync(cancellationToken);

            // Mismo lock que utiliza la creación de RF. Así no puede aparecer un
            // descendiente mientras decidimos si es seguro refrescar esta huella.
            await _dbContext.AcquireExclusiveLockAsync(
                $"VERIFACTU_CHAIN:{record.IssuerNif}",
                cancellationToken);

            await _dbContext.AcquireExclusiveLockAsync(
                $"VERIFACTU_SUBMIT:{record.Id}",
                cancellationToken);

            // Revalidación dentro de la sección crítica para cubrir dos submits concurrentes.
            if (record.IsSubmitted)
            {
                await transaction.RollbackAsync(cancellationToken);

                return new Result<EnviarRegistroAEATResponse>.ConflictError(
                    "El registro ya fue preparado/enviado a AEAT");
            }

            var alreadyQueued = await _outboxStore.ExistsForAggregateEventAsync(
                "BillingRecord",
                record.Id,
                "BillingRecordSubmittedToAEAT",
                cancellationToken);

            if (alreadyQueued)
            {
                await transaction.RollbackAsync(cancellationToken);

                return new Result<EnviarRegistroAEATResponse>.ConflictError(
                    "El registro ya tiene una remisión AEAT en el Outbox.");
            }

            bool requiresRefresh;
            try
            {
                requiresRefresh = SubmissionTimestampPolicy.RequiresRefresh(
                    record,
                    DateTimeOffset.Now);
            }
            catch (InvalidOperationException ex)
            {
                await transaction.RollbackAsync(cancellationToken);

                return new Result<EnviarRegistroAEATResponse>.DomainError(
                    "INVALID_REGISTER_TIMESTAMP",
                    ex.Message);
            }

            if (requiresRefresh)
            {
                var lastGeneratedRecord =
                    await _repository.GetLastGeneratedRecordAsync(
                        record.IssuerNif,
                        cancellationToken);

                if (lastGeneratedRecord is null ||
                    lastGeneratedRecord.Id != record.Id)
                {
                    await transaction.RollbackAsync(cancellationToken);

                    return new Result<EnviarRegistroAEATResponse>.DomainError(
                        "STALE_RECORD_NOT_CHAIN_TAIL",
                        "FechaHoraHusoGenRegistro ha envejecido y no puede refrescarse porque el registro ya tiene RF posteriores en la cadena.");
                }

                var previousHash = record.ComputedHash;

                SubmissionTimestampPolicy.RefreshTimestampAndHash(
                    record,
                    _hashCalculator,
                    DateTimeOffset.Now);

                _logger.LogInformation(
                    "Refrescados FechaHoraHusoGenRegistro y huella del registro {BillingRecordId} antes de encolar. PreviousComputedHash={PreviousComputedHash}, NewComputedHash={NewComputedHash}",
                    record.Id,
                    previousHash,
                    record.ComputedHash);
            }

            BillingRecord? previousRecord = null;

            if (record.PreviousBillingRecordId.HasValue)
            {
                previousRecord = await _repository.GetByIdAsync(
                    record.PreviousBillingRecordId.Value,
                    cancellationToken);

                if (previousRecord is null ||
                    string.IsNullOrWhiteSpace(record.PreviousRecordHash) ||
                    !string.Equals(
                        previousRecord.ComputedHash,
                        record.PreviousRecordHash,
                        StringComparison.Ordinal))
                {
                    await transaction.RollbackAsync(cancellationToken);

                    return new Result<EnviarRegistroAEATResponse>.DomainError(
                        "BROKEN_CHAIN",
                        "No se puede reconstruir de forma íntegra el RegistroAnterior del encadenamiento.");
                }
            }
            else if (!string.IsNullOrWhiteSpace(record.PreviousRecordHash))
            {
                await transaction.RollbackAsync(cancellationToken);

                return new Result<EnviarRegistroAEATResponse>.DomainError(
                    "BROKEN_CHAIN",
                    "El registro tiene huella anterior pero no referencia al RF anterior.");
            }

            var request = BillingRecordToVeriFactuMapper.MapToSubmissionRequest(
                record,
                _xmlBuilder,
                previousRecord);

            var validation = await _xmlSchemaValidator.ValidateAsync(
                request.SignedXmlContent,
                VeriFactuXmlSchemaType.BillingRecord,
                cancellationToken);

            if (!validation.IsValid)
            {
                var details = string.Join(
                    " | ",
                    validation.Errors.Select(e => e.Message));

                _logger.LogError(
                    "XML RegistroAlta inválido para BillingRecord {BillingRecordId}: {ValidationErrors}",
                    record.Id,
                    details);

                await transaction.RollbackAsync(cancellationToken);

                return new Result<EnviarRegistroAEATResponse>.DomainError(
                    "INVALID_AEAT_XML",
                    $"El XML generado no cumple el XSD oficial AEAT: {details}");
            }

            var correlationId = Guid.NewGuid();

            record.MarkAsQueued(correlationId);

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
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Registro {BillingRecordId} validado por XSD y añadido al Outbox. CorrelationId={CorrelationId}",
                record.Id,
                correlationId);

            return new Result<EnviarRegistroAEATResponse>.SuccessWithValue(
                new EnviarRegistroAEATResponse
                {
                    BillingRecordId = record.Id,
                    CorrelationId = correlationId.ToString(),
                    AeatSubmissionId = null,
                    IsAccepted = false,
                    Status = "PENDING",
                    StatusDescription = "Pendiente de envío a AEAT",
                    Details = "XML validado contra XSD oficial. Procesamiento asíncrono mediante Outbox."
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error al preparar el envío del registro {BillingRecordId}",
                command.BillingRecordId);

            return new Result<EnviarRegistroAEATResponse>.ExternalServiceError(
                "Error al preparar envío a AEAT",
                ex.Message);
        }
    }
}
