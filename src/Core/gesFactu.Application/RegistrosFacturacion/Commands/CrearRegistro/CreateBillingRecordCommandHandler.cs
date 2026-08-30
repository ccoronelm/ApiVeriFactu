using MediatR;
using Microsoft.Extensions.Logging;
using gesFactu.Application.Common;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Domain.Entities;
using gesFactu.Domain.ValueObjects;

namespace gesFactu.Application.RegistrosFacturacion.Commands.CrearRegistro;

/// <summary>
/// Handler para crear un nuevo registro de facturación.
/// Orquesta validación, generación del timestamp fiscal, cálculo de huella y persistencia.
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
        _logger.LogInformation(
            "Creando registro de facturación para factura {Series}/{Number} del contribuyente {Nif}",
            command.InvoiceSeries,
            command.InvoiceNumber,
            command.IssuerNif);

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

        var identifierResult = InvoiceIdentifier.Create(nif, series, number, issueDate);
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
            // Este valor se genera una sola vez y se persiste.
            // El mismo valor se usa tanto en la huella como en FechaHoraHusoGenRegistro del XML.
            var registerTimestamp = DateTimeOffset.Now.ToString(
                "yyyy-MM-ddTHH:mm:sszzz",
                System.Globalization.CultureInfo.InvariantCulture);

            var billingRecord = BillingRecord.Create(
                identifier,
                command.IssuerName,
                command.Description,
                totalAmount,
                totalTaxAmount,
                command.PreviousRecordHash,
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

            var calculatedHash = _hashCalculator.CalculateChainHash(hashInput);
            billingRecord.SetComputedHash(calculatedHash);

            await _repository.AddAsync(billingRecord, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Registro de facturación creado: {RecordId} con hash {Hash}",
                billingRecord.Id,
                calculatedHash);

            var response = new CreateBillingRecordResponse(
                billingRecord.Id,
                $"{identifier.IssuerNif.Value}/{identifier.Series.Value}-{identifier.Number.Value}",
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
