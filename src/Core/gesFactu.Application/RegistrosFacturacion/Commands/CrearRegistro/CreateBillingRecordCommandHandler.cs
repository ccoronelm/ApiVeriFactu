using MediatR;
using Microsoft.Extensions.Logging;
using gesFactu.Application.Common;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Domain.Entities;
using gesFactu.Domain.ValueObjects;

namespace gesFactu.Application.RegistrosFacturacion.Commands.CrearRegistro;

/// <summary>
/// Crea un RegistroAlta y resuelve internamente su encadenamiento VERI*FACTU.
/// La selección del RF anterior y la inserción del nuevo RF se ejecutan dentro
/// de una transacción SERIALIZABLE para proteger la secuencia frente a concurrencia.
/// </summary>
public sealed class CreateBillingRecordCommandHandler
    : IRequestHandler<CreateBillingRecordCommand, Result<CreateBillingRecordResponse>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IBillingRecordRepository _repository;
    private readonly IHashCalculator _hashCalculator;
    private readonly ILogger<CreateBillingRecordCommandHandler> _logger;

    public CreateBillingRecordCommandHandler(
        IApplicationDbContext dbContext,
        IBillingRecordRepository repository,
        IHashCalculator hashCalculator,
        ILogger<CreateBillingRecordCommandHandler> logger)
    {
        _dbContext = dbContext;
        _repository = repository;
        _hashCalculator = hashCalculator;
        _logger = logger;
    }

    public async Task<Result<CreateBillingRecordResponse>> Handle(
        CreateBillingRecordCommand command,
        CancellationToken cancellationToken)
    {
        var nifResult = TaxpayerNif.Create(command.IssuerNif);
        if (nifResult is ValueObjectResult<TaxpayerNif>.ValidationError nifError)
        {
            return new Result<CreateBillingRecordResponse>.ValidationError(
                nameof(command.IssuerNif),
                nifError.Message);
        }

        var nif = ((ValueObjectResult<TaxpayerNif>.SuccessWithValue)nifResult).Value;

        var seriesResult = InvoiceSeries.Create(command.InvoiceSeries);
        if (seriesResult is ValueObjectResult<InvoiceSeries>.ValidationError seriesError)
        {
            return new Result<CreateBillingRecordResponse>.ValidationError(
                nameof(command.InvoiceSeries),
                seriesError.Message);
        }

        var series = ((ValueObjectResult<InvoiceSeries>.SuccessWithValue)seriesResult).Value;

        var numberResult = InvoiceNumber.Create(command.InvoiceNumber);
        if (numberResult is ValueObjectResult<InvoiceNumber>.ValidationError numberError)
        {
            return new Result<CreateBillingRecordResponse>.ValidationError(
                nameof(command.InvoiceNumber),
                numberError.Message);
        }

        var number = ((ValueObjectResult<InvoiceNumber>.SuccessWithValue)numberResult).Value;

        var fiscalInvoiceNumber = series.Value.Trim() + number.Value.Trim();
        if (fiscalInvoiceNumber.Length > 60)
        {
            return new Result<CreateBillingRecordResponse>.ValidationError(
                nameof(command.InvoiceNumber),
                "La combinación serie+número (NumSerieFactura) no puede superar 60 caracteres.");
        }

        if (!DateTime.TryParseExact(
                command.IssueDate,
                "dd-MM-yyyy",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var issueDateTime))
        {
            return new Result<CreateBillingRecordResponse>.ValidationError(
                nameof(command.IssueDate),
                "Fecha de expedición inválida");
        }

        var issueDate = DateOnly.FromDateTime(issueDateTime);

        var identifierResult = InvoiceIdentifier.Create(
            nif,
            series,
            number,
            issueDate);

        if (identifierResult is ValueObjectResult<InvoiceIdentifier>.ValidationError identifierError)
        {
            return new Result<CreateBillingRecordResponse>.ValidationError(
                nameof(command),
                identifierError.Message);
        }

        var identifier =
            ((ValueObjectResult<InvoiceIdentifier>.SuccessWithValue)identifierResult).Value;

        var totalAmountResult = Money.Create(command.TotalAmount);
        if (totalAmountResult is ValueObjectResult<Money>.ValidationError amountError)
        {
            return new Result<CreateBillingRecordResponse>.ValidationError(
                nameof(command.TotalAmount),
                amountError.Message);
        }

        var totalAmount =
            ((ValueObjectResult<Money>.SuccessWithValue)totalAmountResult).Value;

        var taxAmountResult = Money.Create(command.TotalTaxAmount);
        if (taxAmountResult is ValueObjectResult<Money>.ValidationError taxError)
        {
            return new Result<CreateBillingRecordResponse>.ValidationError(
                nameof(command.TotalTaxAmount),
                taxError.Message);
        }

        var totalTaxAmount =
            ((ValueObjectResult<Money>.SuccessWithValue)taxAmountResult).Value;

        try
        {
            await using var transaction =
                await _dbContext.BeginSerializableTransactionAsync(cancellationToken);

            // Idempotencia por la identidad fiscal que se envía a AEAT.
            var existing = await _repository.GetByFiscalIdentityAsync(
                nif.Value,
                fiscalInvoiceNumber,
                issueDate,
                cancellationToken);

            if (existing is not null)
            {
                await transaction.RollbackAsync(cancellationToken);

                return new Result<CreateBillingRecordResponse>.IdempotencyConflictError(
                    $"Ya existe un registro para la factura {nif.Value}/{fiscalInvoiceNumber}/{command.IssueDate}.");
            }

            // VERI*FACTU encadena contra el RF inmediatamente anterior generado
            // por el mismo SIF para este obligado tributario, sin separar por serie.
            var previousRecord = await _repository.GetLastGeneratedRecordAsync(
                nif.Value,
                cancellationToken);

            if (previousRecord is not null &&
                string.IsNullOrWhiteSpace(previousRecord.ComputedHash))
            {
                await transaction.RollbackAsync(cancellationToken);

                return new Result<CreateBillingRecordResponse>.DomainError(
                    "BROKEN_CHAIN",
                    "El último registro generado no tiene huella y no se puede encadenar el siguiente.");
            }

            var registerTimestamp = DateTimeOffset.Now.ToString(
                "yyyy-MM-ddTHH:mm:sszzz",
                System.Globalization.CultureInfo.InvariantCulture);

            var billingRecord = BillingRecord.Create(
                identifier,
                command.IssuerName,
                command.Description,
                totalAmount,
                totalTaxAmount,
                previousRecord?.Id,
                previousRecord?.ComputedHash,
                registerTimestamp);

            var hashInput = new BillingRecordHashInput
            {
                PreviousHash = billingRecord.PreviousRecordHash ?? string.Empty,
                IssuerNif = billingRecord.IssuerNif,
                InvoiceSeries = billingRecord.InvoiceSeries,
                InvoiceNumber = billingRecord.InvoiceNumber,
                IssueDate = billingRecord.IssueDate.ToString(
                    "dd-MM-yyyy",
                    System.Globalization.CultureInfo.InvariantCulture),
                InvoiceType = "F1",
                TotalAmount = billingRecord.TotalAmount,
                TotalTaxAmount = billingRecord.TotalTaxAmount,
                RegisterTimestamp = billingRecord.RegisterTimestamp
            };

            billingRecord.SetComputedHash(
                _hashCalculator.CalculateChainHash(hashInput));

            await _repository.AddAsync(billingRecord, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Registro {RecordId} creado. PreviousRecordId={PreviousRecordId}, Hash={Hash}",
                billingRecord.Id,
                billingRecord.PreviousBillingRecordId,
                billingRecord.ComputedHash);

            var response = new CreateBillingRecordResponse(
                billingRecord.Id,
                $"{identifier.IssuerNif.Value}/{fiscalInvoiceNumber}",
                billingRecord.Status,
                billingRecord.ComputedHash,
                billingRecord.CreateDate ?? DateTime.UtcNow);

            return new Result<CreateBillingRecordResponse>.SuccessWithValue(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear registro de facturación");

            return new Result<CreateBillingRecordResponse>.UnexpectedError(
                $"Error al crear el registro: {ex.Message}");
        }
    }
}
