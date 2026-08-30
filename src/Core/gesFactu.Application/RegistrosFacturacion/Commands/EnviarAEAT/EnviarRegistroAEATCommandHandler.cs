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
/// 2. Genera el XML RegistroAlta.
/// 3. Valida obligatoriamente el XML contra el XSD oficial.
/// 4. Solo si el XML es válido crea el mensaje de Outbox.
/// </summary>
public sealed class EnviarRegistroAEATCommandHandler
    : IRequestHandler<EnviarRegistroAEATCommand, Result<EnviarRegistroAEATResponse>>
{
    private readonly IBillingRecordRepository _repository;
    private readonly IApplicationDbContext _dbContext;
    private readonly IRegistroAltaXmlBuilder _xmlBuilder;
    private readonly IXmlSchemaValidator _xmlSchemaValidator;
    private readonly ILogger<EnviarRegistroAEATCommandHandler> _logger;

    public EnviarRegistroAEATCommandHandler(
        IBillingRecordRepository repository,
        IApplicationDbContext dbContext,
        IRegistroAltaXmlBuilder xmlBuilder,
        IXmlSchemaValidator xmlSchemaValidator,
        ILogger<EnviarRegistroAEATCommandHandler> logger)
    {
        _repository = repository;
        _dbContext = dbContext;
        _xmlBuilder = xmlBuilder;
        _xmlSchemaValidator = xmlSchemaValidator;
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
                    return new Result<EnviarRegistroAEATResponse>.DomainError(
                        "BROKEN_CHAIN",
                        "No se puede reconstruir de forma íntegra el RegistroAnterior del encadenamiento.");
                }
            }
            else if (!string.IsNullOrWhiteSpace(record.PreviousRecordHash))
            {
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

                return new Result<EnviarRegistroAEATResponse>.DomainError(
                    "INVALID_AEAT_XML",
                    $"El XML generado no cumple el XSD oficial AEAT: {details}");
            }

            var correlationId = Guid.NewGuid();

            // Aquí AeatSubmissionId todavía contiene el identificador local de correlación.
            // Se sustituirá por el CSV/identificador real cuando el Outbox procese la respuesta AEAT.
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
                "Registro {BillingRecordId} validado por XSD y añadido al Outbox. CorrelationId={CorrelationId}",
                record.Id,
                correlationId);

            return new Result<EnviarRegistroAEATResponse>.SuccessWithValue(
                new EnviarRegistroAEATResponse
                {
                    BillingRecordId = record.Id,
                    AeatSubmissionId = correlationId.ToString(),
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
