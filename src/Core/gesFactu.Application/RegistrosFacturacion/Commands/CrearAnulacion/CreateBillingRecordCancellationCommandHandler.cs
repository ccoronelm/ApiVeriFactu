using System.Globalization;
using MediatR;
using Microsoft.Extensions.Logging;
using gesFactu.Application.Common;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Domain.Entities;
using gesFactu.Domain.ValueObjects;

namespace gesFactu.Application.RegistrosFacturacion.Commands.CrearAnulacion;

/// <summary>
/// Crea un RegistroAnulacion inmutable que participa en la misma cadena de RF.
/// </summary>
public sealed class CreateBillingRecordCancellationCommandHandler
    : IRequestHandler<CreateBillingRecordCancellationCommand, Result<CreateBillingRecordCancellationResponse>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IBillingRecordRepository _repository;
    private readonly IHashCalculator _hashCalculator;
    private readonly ILogger<CreateBillingRecordCancellationCommandHandler> _logger;

    public CreateBillingRecordCancellationCommandHandler(
        IApplicationDbContext dbContext,
        IBillingRecordRepository repository,
        IHashCalculator hashCalculator,
        ILogger<CreateBillingRecordCancellationCommandHandler> logger)
    {
        _dbContext = dbContext;
        _repository = repository;
        _hashCalculator = hashCalculator;
        _logger = logger;
    }

    public async Task<Result<CreateBillingRecordCancellationResponse>> Handle(
        CreateBillingRecordCancellationCommand command,
        CancellationToken cancellationToken)
    {
        var source = await _repository.GetByIdAsync(
            command.SourceBillingRecordId,
            cancellationToken);

        if (source is null)
        {
            return new Result<CreateBillingRecordCancellationResponse>.NotFoundError(
                "BillingRecord",
                command.SourceBillingRecordId.ToString(CultureInfo.InvariantCulture));
        }

        if (source.RecordType != BillingRecord.AltaRecordType)
        {
            return new Result<CreateBillingRecordCancellationResponse>.ConflictError(
                "Solo se puede anular un RegistroAlta.");
        }

        if (!CanBeCancelled(source.Status))
        {
            return new Result<CreateBillingRecordCancellationResponse>.ConflictError(
                "La anulación requiere que el registro exista en AEAT con estado aceptado.");
        }

        var issuerNifResult = TaxpayerNif.Create(source.IssuerNif);
        if (issuerNifResult is ValueObjectResult<TaxpayerNif>.ValidationError issuerError)
            return Validation(nameof(source.IssuerNif), issuerError.Message);

        var issuerNif =
            ((ValueObjectResult<TaxpayerNif>.SuccessWithValue)issuerNifResult).Value;

        var seriesResult = InvoiceSeries.Create(source.InvoiceSeries);
        if (seriesResult is ValueObjectResult<InvoiceSeries>.ValidationError seriesError)
            return Validation(nameof(source.InvoiceSeries), seriesError.Message);

        var series =
            ((ValueObjectResult<InvoiceSeries>.SuccessWithValue)seriesResult).Value;

        var numberResult = InvoiceNumber.Create(source.InvoiceNumber);
        if (numberResult is ValueObjectResult<InvoiceNumber>.ValidationError numberError)
            return Validation(nameof(source.InvoiceNumber), numberError.Message);

        var number =
            ((ValueObjectResult<InvoiceNumber>.SuccessWithValue)numberResult).Value;

        var identifierResult = InvoiceIdentifier.Create(
            issuerNif,
            series,
            number,
            source.IssueDate);

        if (identifierResult is ValueObjectResult<InvoiceIdentifier>.ValidationError identifierError)
            return Validation("InvoiceIdentifier", identifierError.Message);

        var identifier =
            ((ValueObjectResult<InvoiceIdentifier>.SuccessWithValue)identifierResult).Value;

        var totalResult = Money.Create(source.TotalAmount);
        if (totalResult is ValueObjectResult<Money>.ValidationError totalError)
            return Validation(nameof(source.TotalAmount), totalError.Message);

        var taxResult = Money.Create(source.TotalTaxAmount);
        if (taxResult is ValueObjectResult<Money>.ValidationError taxError)
            return Validation(nameof(source.TotalTaxAmount), taxError.Message);

        var total = ((ValueObjectResult<Money>.SuccessWithValue)totalResult).Value;
        var tax = ((ValueObjectResult<Money>.SuccessWithValue)taxResult).Value;

        try
        {
            await using var transaction =
                await _dbContext.BeginTransactionAsync(cancellationToken);

            await _dbContext.AcquireExclusiveLockAsync(
                $"VERIFACTU_CHAIN:{source.IssuerNif}",
                cancellationToken);

            var existingCancellation =
                await _repository.GetCancellationForFiscalIdentityAsync(
                    source.IssuerNif,
                    source.FiscalInvoiceNumber,
                    source.IssueDate,
                    cancellationToken);

            if (existingCancellation is not null)
            {
                await transaction.RollbackAsync(cancellationToken);

                return new Result<CreateBillingRecordCancellationResponse>.ConflictError(
                    $"Ya existe un RegistroAnulacion para esta factura: {existingCancellation.Id}.");
            }

            var previousRecord = await _repository.GetLastGeneratedRecordAsync(
                source.IssuerNif,
                cancellationToken);

            if (previousRecord is null ||
                string.IsNullOrWhiteSpace(previousRecord.ComputedHash))
            {
                await transaction.RollbackAsync(cancellationToken);

                return new Result<CreateBillingRecordCancellationResponse>.DomainError(
                    "BROKEN_CHAIN",
                    "No se puede determinar la huella del RF inmediatamente anterior.");
            }

            var registerTimestamp = DateTimeOffset.Now.ToString(
                "yyyy-MM-ddTHH:mm:sszzz",
                CultureInfo.InvariantCulture);

            // Reutilizamos los datos de la factura únicamente para conservar el
            // agregado y su identidad fiscal. El XML/hash de anulación ignoran
            // destinatario, descripción e importes.
            var cancellation = BillingRecord.Create(
                identifier,
                source.IssuerName,
                source.RecipientNif,
                source.RecipientName,
                source.Description,
                total,
                tax,
                previousRecord.Id,
                previousRecord.ComputedHash,
                registerTimestamp);

            cancellation.RecordType = BillingRecord.CancellationRecordType;
            cancellation.CancelsBillingRecordId = source.Id;

            cancellation.SetComputedHash(
                _hashCalculator.CalculateCancellationHash(
                    new CancellationRecordHashInput
                    {
                        PreviousHash = cancellation.PreviousRecordHash ?? string.Empty,
                        IssuerNif = cancellation.IssuerNif,
                        InvoiceSeries = cancellation.InvoiceSeries,
                        InvoiceNumber = cancellation.InvoiceNumber,
                        IssueDate = cancellation.IssueDate.ToString(
                            "dd-MM-yyyy",
                            CultureInfo.InvariantCulture),
                        RegisterTimestamp = cancellation.RegisterTimestamp
                    }));

            await _repository.AddAsync(cancellation, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "RegistroAnulacion {CancellationId} creado para {SourceId}. PreviousRecordId={PreviousRecordId}, Hash={Hash}",
                cancellation.Id,
                source.Id,
                previousRecord.Id,
                cancellation.ComputedHash);

            return new Result<CreateBillingRecordCancellationResponse>.SuccessWithValue(
                new CreateBillingRecordCancellationResponse(
                    cancellation.Id,
                    source.Id,
                    $"{cancellation.IssuerNif}/{cancellation.FiscalInvoiceNumber}",
                    cancellation.Status,
                    cancellation.ComputedHash!,
                    cancellation.CreateDate ?? DateTime.UtcNow));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error al crear RegistroAnulacion para {SourceId}",
                source.Id);

            return new Result<CreateBillingRecordCancellationResponse>.UnexpectedError(
                $"Error al crear la anulación: {ex.Message}");
        }
    }

    private static bool CanBeCancelled(string status)
        => status is
            "Aceptado" or
            "AceptadoConErrores" or
            "AceptadoPorDuplicadoAEAT" or
            "AceptadoConErroresPorDuplicadoAEAT";

    private static Result<CreateBillingRecordCancellationResponse>.ValidationError Validation(
        string property,
        string message)
        => new(property, message);
}
