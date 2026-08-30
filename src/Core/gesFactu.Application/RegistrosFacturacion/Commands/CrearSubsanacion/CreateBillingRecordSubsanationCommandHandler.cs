using System.Globalization;
using MediatR;
using Microsoft.Extensions.Logging;
using gesFactu.Application.Common;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Domain.Entities;
using gesFactu.Domain.ValueObjects;

namespace gesFactu.Application.RegistrosFacturacion.Commands.CrearSubsanacion;

/// <summary>
/// Crea una subsanación sin alterar el registro original.
/// Ref. AEAT VERI*FACTU: alta de subsanación de un registro ya existente:
/// Subsanacion=S y RechazoPrevio omitido/N.
/// </summary>
public sealed class CreateBillingRecordSubsanationCommandHandler
    : IRequestHandler<CreateBillingRecordSubsanationCommand, Result<CreateBillingRecordSubsanationResponse>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IBillingRecordRepository _repository;
    private readonly IHashCalculator _hashCalculator;
    private readonly ILogger<CreateBillingRecordSubsanationCommandHandler> _logger;

    public CreateBillingRecordSubsanationCommandHandler(
        IApplicationDbContext dbContext,
        IBillingRecordRepository repository,
        IHashCalculator hashCalculator,
        ILogger<CreateBillingRecordSubsanationCommandHandler> logger)
    {
        _dbContext = dbContext;
        _repository = repository;
        _hashCalculator = hashCalculator;
        _logger = logger;
    }

    public async Task<Result<CreateBillingRecordSubsanationResponse>> Handle(
        CreateBillingRecordSubsanationCommand command,
        CancellationToken cancellationToken)
    {
        var source = await _repository.GetByIdAsync(
            command.SourceBillingRecordId,
            cancellationToken);

        if (source is null)
        {
            return new Result<CreateBillingRecordSubsanationResponse>.NotFoundError(
                "BillingRecord",
                command.SourceBillingRecordId.ToString(CultureInfo.InvariantCulture));
        }

        if (!CanBeSubsanated(source.Status))
        {
            return new Result<CreateBillingRecordSubsanationResponse>.ConflictError(
                "La subsanación habitual requiere que el registro exista en AEAT con estado Aceptado o AceptadoConErrores.");
        }

        var recipientNifText = (command.RecipientNif ?? source.RecipientNif).Trim();
        var recipientName = (command.RecipientName ?? source.RecipientName).Trim();
        var description = (command.Description ?? source.Description).Trim();
        var totalAmountValue = command.TotalAmount ?? source.TotalAmount;
        var totalTaxAmountValue = command.TotalTaxAmount ?? source.TotalTaxAmount;

        var issuerNifResult = TaxpayerNif.Create(source.IssuerNif);
        if (issuerNifResult is ValueObjectResult<TaxpayerNif>.ValidationError issuerError)
            return Validation(nameof(source.IssuerNif), issuerError.Message);
        var issuerNif = ((ValueObjectResult<TaxpayerNif>.SuccessWithValue)issuerNifResult).Value;

        var hasRecipientNif = !string.IsNullOrWhiteSpace(recipientNifText);
        var hasRecipientName = !string.IsNullOrWhiteSpace(recipientName);

        if (source.InvoiceType == "F1" && (!hasRecipientNif || !hasRecipientName))
            return Validation(nameof(command.RecipientNif), "F1 requiere destinatario.");

        if (hasRecipientNif != hasRecipientName)
            return Validation(nameof(command.RecipientNif), "NIF y nombre del destinatario deben informarse juntos.");

        if (hasRecipientNif)
        {
            var recipientNifResult = TaxpayerNif.Create(recipientNifText);
            if (recipientNifResult is ValueObjectResult<TaxpayerNif>.ValidationError recipientError)
                return Validation(nameof(command.RecipientNif), recipientError.Message);

            recipientNifText =
                ((ValueObjectResult<TaxpayerNif>.SuccessWithValue)recipientNifResult).Value.Value;

            if (string.Equals(issuerNif.Value, recipientNifText, StringComparison.OrdinalIgnoreCase))
                return Validation(nameof(command.RecipientNif), "El NIF del destinatario debe ser distinto del NIF del obligado emisor.");

            if (recipientName.Length > 120)
                return Validation(nameof(command.RecipientName), "El nombre del destinatario no puede superar 120 caracteres.");
        }

        if (string.IsNullOrWhiteSpace(description) || description.Length > 500)
            return Validation(nameof(command.Description), "La descripción es obligatoria y no puede superar 500 caracteres.");

        var totalAmountResult = Money.Create(totalAmountValue);
        if (totalAmountResult is ValueObjectResult<Money>.ValidationError totalError)
            return Validation(nameof(command.TotalAmount), totalError.Message);
        var totalAmount = ((ValueObjectResult<Money>.SuccessWithValue)totalAmountResult).Value;

        var taxAmountResult = Money.Create(totalTaxAmountValue);
        if (taxAmountResult is ValueObjectResult<Money>.ValidationError taxError)
            return Validation(nameof(command.TotalTaxAmount), taxError.Message);
        var totalTaxAmount = ((ValueObjectResult<Money>.SuccessWithValue)taxAmountResult).Value;

        if (totalTaxAmount.Amount > totalAmount.Amount)
            return Validation(nameof(command.TotalTaxAmount), "La cuota de impuesto no puede ser mayor que el importe total.");

        var seriesResult = InvoiceSeries.Create(source.InvoiceSeries);
        if (seriesResult is ValueObjectResult<InvoiceSeries>.ValidationError seriesError)
            return Validation(nameof(source.InvoiceSeries), seriesError.Message);
        var series = ((ValueObjectResult<InvoiceSeries>.SuccessWithValue)seriesResult).Value;

        var numberResult = InvoiceNumber.Create(source.InvoiceNumber);
        if (numberResult is ValueObjectResult<InvoiceNumber>.ValidationError numberError)
            return Validation(nameof(source.InvoiceNumber), numberError.Message);
        var number = ((ValueObjectResult<InvoiceNumber>.SuccessWithValue)numberResult).Value;

        var identifierResult = InvoiceIdentifier.Create(
            issuerNif,
            series,
            number,
            source.IssueDate);

        if (identifierResult is ValueObjectResult<InvoiceIdentifier>.ValidationError identifierError)
            return Validation("InvoiceIdentifier", identifierError.Message);

        var identifier =
            ((ValueObjectResult<InvoiceIdentifier>.SuccessWithValue)identifierResult).Value;

        try
        {
            await using var transaction =
                await _dbContext.BeginTransactionAsync(cancellationToken);

            await _dbContext.AcquireExclusiveLockAsync(
                $"VERIFACTU_CHAIN:{source.IssuerNif}",
                cancellationToken);

            var pending = await _repository.GetPendingSubsanationForSourceAsync(
                source.Id,
                cancellationToken);

            if (pending is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new Result<CreateBillingRecordSubsanationResponse>.ConflictError(
                    $"Ya existe una subsanación pendiente para el registro {source.Id}: {pending.Id}.");
            }

            var previousRecord = await _repository.GetLastGeneratedRecordAsync(
                source.IssuerNif,
                cancellationToken);

            if (previousRecord is null || string.IsNullOrWhiteSpace(previousRecord.ComputedHash))
            {
                await transaction.RollbackAsync(cancellationToken);
                return new Result<CreateBillingRecordSubsanationResponse>.DomainError(
                    "BROKEN_CHAIN",
                    "No se puede determinar la huella del RF inmediatamente anterior.");
            }

            var registerTimestamp = DateTimeOffset.Now.ToString(
                "yyyy-MM-ddTHH:mm:sszzz",
                CultureInfo.InvariantCulture);

            var subsanation = BillingRecord.Create(
                identifier,
                source.IssuerName,
                recipientNifText,
                recipientName,
                description,
                totalAmount,
                totalTaxAmount,
                previousRecord.Id,
                previousRecord.ComputedHash,
                registerTimestamp,
                source.InvoiceType);

            subsanation.SubsanatesBillingRecordId = source.Id;

            var hashInput = new BillingRecordHashInput
            {
                PreviousHash = subsanation.PreviousRecordHash ?? string.Empty,
                IssuerNif = subsanation.IssuerNif,
                InvoiceSeries = subsanation.InvoiceSeries,
                InvoiceNumber = subsanation.InvoiceNumber,
                IssueDate = subsanation.IssueDate.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture),
                InvoiceType = source.InvoiceType,
                TotalAmount = subsanation.TotalAmount,
                TotalTaxAmount = subsanation.TotalTaxAmount,
                RegisterTimestamp = subsanation.RegisterTimestamp
            };

            subsanation.SetComputedHash(
                _hashCalculator.CalculateChainHash(hashInput));

            await _repository.AddAsync(subsanation, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Subsanación {SubsanationId} creada para {SourceId}. PreviousRecordId={PreviousRecordId}, Hash={Hash}",
                subsanation.Id,
                source.Id,
                previousRecord.Id,
                subsanation.ComputedHash);

            return new Result<CreateBillingRecordSubsanationResponse>.SuccessWithValue(
                new CreateBillingRecordSubsanationResponse(
                    subsanation.Id,
                    source.Id,
                    $"{subsanation.IssuerNif}/{subsanation.FiscalInvoiceNumber}",
                    subsanation.Status,
                    subsanation.ComputedHash!,
                    subsanation.CreateDate ?? DateTime.UtcNow));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error al crear subsanación para el registro {SourceId}",
                source.Id);

            return new Result<CreateBillingRecordSubsanationResponse>.UnexpectedError(
                $"Error al crear la subsanación: {ex.Message}");
        }
    }

    private static bool CanBeSubsanated(string status)
        => status is
            "Aceptado" or
            "AceptadoConErrores" or
            "AceptadoPorDuplicadoAEAT" or
            "AceptadoConErroresPorDuplicadoAEAT";

    private static Result<CreateBillingRecordSubsanationResponse>.ValidationError Validation(
        string property,
        string message)
        => new(property, message);
}
