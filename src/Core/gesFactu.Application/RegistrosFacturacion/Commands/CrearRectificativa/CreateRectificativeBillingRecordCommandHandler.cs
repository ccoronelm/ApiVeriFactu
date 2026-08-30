using System.Globalization;
using MediatR;
using Microsoft.Extensions.Logging;
using gesFactu.Application.Common;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Domain.Entities;
using gesFactu.Domain.ValueObjects;

namespace gesFactu.Application.RegistrosFacturacion.Commands.CrearRectificativa;

public sealed class CreateRectificativeBillingRecordCommandHandler
    : IRequestHandler<CreateRectificativeBillingRecordCommand, Result<CreateRectificativeBillingRecordResponse>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IBillingRecordRepository _repository;
    private readonly IHashCalculator _hashCalculator;
    private readonly ILogger<CreateRectificativeBillingRecordCommandHandler> _logger;

    public CreateRectificativeBillingRecordCommandHandler(
        IApplicationDbContext dbContext,
        IBillingRecordRepository repository,
        IHashCalculator hashCalculator,
        ILogger<CreateRectificativeBillingRecordCommandHandler> logger)
    {
        _dbContext = dbContext;
        _repository = repository;
        _hashCalculator = hashCalculator;
        _logger = logger;
    }

    public async Task<Result<CreateRectificativeBillingRecordResponse>> Handle(
        CreateRectificativeBillingRecordCommand command,
        CancellationToken cancellationToken)
    {
        var source = await _repository.GetByIdAsync(
            command.SourceBillingRecordId,
            cancellationToken);

        if (source is null)
        {
            return new Result<CreateRectificativeBillingRecordResponse>.NotFoundError(
                "BillingRecord",
                command.SourceBillingRecordId.ToString(CultureInfo.InvariantCulture));
        }

        if (source.RecordType != BillingRecord.AltaRecordType)
        {
            return new Result<CreateRectificativeBillingRecordResponse>.ConflictError(
                "Solo se puede rectificar un RegistroAlta.");
        }

        if (!CanBeRectified(source.Status))
        {
            return new Result<CreateRectificativeBillingRecordResponse>.ConflictError(
                "La rectificación requiere que el registro origen exista en AEAT con estado aceptado.");
        }

        var invoiceType = command.InvoiceType.Trim().ToUpperInvariant();
        if (invoiceType is not ("R1" or "R2" or "R3" or "R4" or "R5"))
            return Validation(nameof(command.InvoiceType), "TipoFactura rectificativa debe ser R1, R2, R3, R4 o R5.");

        if (invoiceType == "R5" && source.InvoiceType != "F2")
            return Validation(nameof(command.InvoiceType), "R5 se reserva para rectificación de facturas simplificadas F2.");

        if (invoiceType != "R5" && source.InvoiceType == "F2")
            return Validation(nameof(command.InvoiceType), "Una factura simplificada F2 debe rectificarse mediante R5.");

        var rectificationType = command.RectificationType.Trim().ToUpperInvariant();
        if (rectificationType is not ("I" or "S"))
            return Validation(nameof(command.RectificationType), "TipoRectificativa debe ser I (incremental) o S (sustitutiva).");

        if (string.IsNullOrWhiteSpace(command.Description) ||
            command.Description.Trim().Length > 500)
        {
            return Validation(
                nameof(command.Description),
                "La descripción es obligatoria y no puede superar 500 caracteres.");
        }

        var issuerNifResult = TaxpayerNif.Create(source.IssuerNif);
        if (issuerNifResult is ValueObjectResult<TaxpayerNif>.ValidationError issuerError)
            return Validation(nameof(source.IssuerNif), issuerError.Message);

        var issuerNif =
            ((ValueObjectResult<TaxpayerNif>.SuccessWithValue)issuerNifResult).Value;

        var seriesResult = InvoiceSeries.Create(command.InvoiceSeries);
        if (seriesResult is ValueObjectResult<InvoiceSeries>.ValidationError seriesError)
            return Validation(nameof(command.InvoiceSeries), seriesError.Message);

        var series =
            ((ValueObjectResult<InvoiceSeries>.SuccessWithValue)seriesResult).Value;

        var numberResult = InvoiceNumber.Create(command.InvoiceNumber);
        if (numberResult is ValueObjectResult<InvoiceNumber>.ValidationError numberError)
            return Validation(nameof(command.InvoiceNumber), numberError.Message);

        var number =
            ((ValueObjectResult<InvoiceNumber>.SuccessWithValue)numberResult).Value;

        var fiscalInvoiceNumber = series.Value.Trim() + number.Value.Trim();
        if (fiscalInvoiceNumber.Length > 60)
        {
            return Validation(
                nameof(command.InvoiceNumber),
                "La combinación serie+número no puede superar 60 caracteres.");
        }

        if (!DateTime.TryParseExact(
                command.IssueDate,
                "dd-MM-yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var issueDateTime))
        {
            return Validation(nameof(command.IssueDate), "Fecha de expedición inválida.");
        }

        var issueDate = DateOnly.FromDateTime(issueDateTime);

        var identifierResult = InvoiceIdentifier.Create(
            issuerNif,
            series,
            number,
            issueDate);

        if (identifierResult is ValueObjectResult<InvoiceIdentifier>.ValidationError identifierError)
            return Validation("InvoiceIdentifier", identifierError.Message);

        var identifier =
            ((ValueObjectResult<InvoiceIdentifier>.SuccessWithValue)identifierResult).Value;

        var totalResult = Money.Create(command.TotalAmount);
        if (totalResult is ValueObjectResult<Money>.ValidationError totalError)
            return Validation(nameof(command.TotalAmount), totalError.Message);

        var taxResult = Money.Create(command.TotalTaxAmount);
        if (taxResult is ValueObjectResult<Money>.ValidationError taxError)
            return Validation(nameof(command.TotalTaxAmount), taxError.Message);

        var total = ((ValueObjectResult<Money>.SuccessWithValue)totalResult).Value;
        var tax = ((ValueObjectResult<Money>.SuccessWithValue)taxResult).Value;

        if (tax.Amount > total.Amount)
            return Validation(nameof(command.TotalTaxAmount), "La cuota no puede superar el importe total.");

        try
        {
            await using var transaction =
                await _dbContext.BeginTransactionAsync(cancellationToken);

            await _dbContext.AcquireExclusiveLockAsync(
                $"VERIFACTU_CHAIN:{source.IssuerNif}",
                cancellationToken);

            var existing = await _repository.GetByFiscalIdentityAsync(
                source.IssuerNif,
                fiscalInvoiceNumber,
                issueDate,
                cancellationToken);

            if (existing is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new Result<CreateRectificativeBillingRecordResponse>.ConflictError(
                    $"Ya existe un RegistroAlta para {source.IssuerNif}/{fiscalInvoiceNumber}/{command.IssueDate}.");
            }

            var previous = await _repository.GetLastGeneratedRecordAsync(
                source.IssuerNif,
                cancellationToken);

            if (previous is null || string.IsNullOrWhiteSpace(previous.ComputedHash))
            {
                await transaction.RollbackAsync(cancellationToken);
                return new Result<CreateRectificativeBillingRecordResponse>.DomainError(
                    "BROKEN_CHAIN",
                    "No se puede determinar la huella del RF inmediatamente anterior.");
            }

            var timestamp = DateTimeOffset.Now.ToString(
                "yyyy-MM-ddTHH:mm:sszzz",
                CultureInfo.InvariantCulture);

            var record = BillingRecord.Create(
                identifier,
                source.IssuerName,
                source.RecipientNif,
                source.RecipientName,
                command.Description.Trim(),
                total,
                tax,
                previous.Id,
                previous.ComputedHash,
                timestamp,
                invoiceType);

            record.RectifiesBillingRecordId = source.Id;
            record.RectificationType = rectificationType;

            if (rectificationType == "S")
            {
                record.RectifiedBaseAmount =
                    source.TotalAmount - source.TotalTaxAmount;
                record.RectifiedTaxAmount = source.TotalTaxAmount;
                record.RectifiedSurchargeAmount = null;
            }

            record.SetComputedHash(
                _hashCalculator.CalculateChainHash(
                    new BillingRecordHashInput
                    {
                        PreviousHash = record.PreviousRecordHash ?? string.Empty,
                        IssuerNif = record.IssuerNif,
                        InvoiceSeries = record.InvoiceSeries,
                        InvoiceNumber = record.InvoiceNumber,
                        IssueDate = record.IssueDate.ToString(
                            "dd-MM-yyyy",
                            CultureInfo.InvariantCulture),
                        InvoiceType = record.InvoiceType,
                        TotalAmount = record.TotalAmount,
                        TotalTaxAmount = record.TotalTaxAmount,
                        RegisterTimestamp = record.RegisterTimestamp
                    }));

            await _repository.AddAsync(record, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Rectificativa {RecordId} ({InvoiceType}/{RectificationType}) creada para {SourceId}. Previous={PreviousId}",
                record.Id,
                record.InvoiceType,
                record.RectificationType,
                source.Id,
                previous.Id);

            return new Result<CreateRectificativeBillingRecordResponse>.SuccessWithValue(
                new CreateRectificativeBillingRecordResponse(
                    record.Id,
                    source.Id,
                    $"{record.IssuerNif}/{record.FiscalInvoiceNumber}",
                    record.InvoiceType,
                    record.RectificationType!,
                    record.Status,
                    record.ComputedHash!,
                    record.CreateDate ?? DateTime.UtcNow));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error creando rectificativa para {SourceId}",
                source.Id);

            return new Result<CreateRectificativeBillingRecordResponse>.UnexpectedError(
                $"Error al crear la rectificativa: {ex.Message}");
        }
    }

    private static bool CanBeRectified(string status)
        => status is
            "Aceptado" or
            "AceptadoConErrores" or
            "AceptadoPorDuplicadoAEAT" or
            "AceptadoConErroresPorDuplicadoAEAT";

    private static Result<CreateRectificativeBillingRecordResponse>.ValidationError Validation(
        string property,
        string message)
        => new(property, message);
}
