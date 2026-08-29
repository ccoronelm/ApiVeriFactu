using MediatR;
using Microsoft.Extensions.Logging;
using gesFactu.Application.Common;
using gesFactu.Application.Common.Abstractions;

namespace gesFactu.Application.RegistrosFacturacion.Commands.EnviarAEAT;

/// <summary>
/// Handler para enviar un registro de facturación a AEAT.
/// 
/// Orquesta:
/// 1. Obtención del registro desde repositorio
/// 2. Validación de precondiciones (debe estar pendiente, con hash)
/// 3. Mapeo a solicitud AEAT
/// 4. Llamada a gateway AEAT
/// 5. Actualización de estado del registro
/// 6. Persistencia
/// 
/// Ref: /VERIFACTU - Reglamentación de envío y estados
/// </summary>
public sealed class EnviarRegistroAEATCommandHandler
    : IRequestHandler<EnviarRegistroAEATCommand, Result<EnviarRegistroAEATResponse>>
{
    private readonly IBillingRecordRepository _repository;
    private readonly IVeriFactuGateway _veriFactuGateway;
    private readonly IApplicationDbContext _dbContext;
    private readonly ILogger<EnviarRegistroAEATCommandHandler> _logger;

    public EnviarRegistroAEATCommandHandler(
        IBillingRecordRepository repository,
        IVeriFactuGateway veriFactuGateway,
        IApplicationDbContext dbContext,
        ILogger<EnviarRegistroAEATCommandHandler> logger)
    {
        _repository = repository;
        _veriFactuGateway = veriFactuGateway;
        _dbContext = dbContext;
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
            // 3. Mapear a solicitud AEAT
            var request = BillingRecordToVeriFactuMapper.MapToSubmissionRequest(record, "");

            // 4. Enviar a AEAT
            _logger.LogInformation(
                "Enviando solicitud a AEAT para registro {BillingRecordId}",
                command.BillingRecordId);

            var aeatResult = await _veriFactuGateway.SubmitBillingRecordAsync(request, cancellationToken);

            // 5. Actualizar estado del registro según respuesta AEAT
            record.MarkAsSubmitted(aeatResult.SubmissionId);

            if (aeatResult.IsAccepted)
            {
                record.MarkAsAccepted();
                _logger.LogInformation(
                    "Registro aceptado por AEAT: {BillingRecordId}, SubmissionId: {SubmissionId}",
                    command.BillingRecordId,
                    aeatResult.SubmissionId);
            }
            else
            {
                record.MarkAsRejected(aeatResult.StatusDescription ?? "Error desconocido de AEAT");
                _logger.LogWarning(
                    "Registro rechazado por AEAT: {BillingRecordId}, Motivo: {StatusDescription}",
                    command.BillingRecordId,
                    aeatResult.StatusDescription);
            }

            // 6. Persistir cambios
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Cambios persistidos para registro {BillingRecordId}",
                command.BillingRecordId);

            return new Result<EnviarRegistroAEATResponse>.SuccessWithValue(
                new EnviarRegistroAEATResponse
                {
                    BillingRecordId = command.BillingRecordId,
                    AeatSubmissionId = aeatResult.SubmissionId,
                    IsAccepted = aeatResult.IsAccepted,
                    Status = aeatResult.StatusCode,
                    StatusDescription = aeatResult.StatusDescription,
                    Details = aeatResult.AdditionalDetails
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error inesperado al enviar registro {BillingRecordId} a AEAT",
                command.BillingRecordId);

            return new Result<EnviarRegistroAEATResponse>.ExternalServiceError(
                "Error de comunicación con AEAT",
                ex.Message);
        }
    }
}
