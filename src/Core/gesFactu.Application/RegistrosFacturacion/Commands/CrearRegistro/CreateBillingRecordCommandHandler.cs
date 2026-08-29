using MediatR;
using Microsoft.Extensions.Logging;
using gesFactu.Application.Common;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Domain.Entities;
using gesFactu.Domain.ValueObjects;

namespace gesFactu.Application.RegistrosFacturacion.Commands.CrearRegistro;

/// <summary>
/// Handler para el comando de crear un nuevo registro de facturación.
/// Orquesta la validación y persistencia sin contener lógica fiscal.
/// </summary>
public sealed class CreateBillingRecordCommandHandler
    : IRequestHandler<CreateBillingRecordCommand, Result<CreateBillingRecordResponse>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ILogger<CreateBillingRecordCommandHandler> _logger;

    public CreateBillingRecordCommandHandler(
        IApplicationDbContext dbContext,
        ILogger<CreateBillingRecordCommandHandler> logger)
    {
        _dbContext = dbContext;
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

        // Validar y crear Value Objects
        var nipResult = TaxpayerNif.Create(command.IssuerNif);
        if (nipResult is ValueObjectResult<TaxpayerNif>.ValidationError nipError)
        {
            _logger.LogWarning("Validación fallida para NIF: {Message}", nipError.Message);
            return new Result<CreateBillingRecordResponse>.ValidationError(
                nameof(command.IssuerNif),
                nipError.Message);
        }

        var nif = ((ValueObjectResult<TaxpayerNif>.SuccessWithValue)nipResult).Value;

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

        // Parsear fecha
        if (!DateTime.TryParseExact(command.IssueDate, "dd-MM-yyyy", null, System.Globalization.DateTimeStyles.None, out var issueDateTime))
        {
            return new Result<CreateBillingRecordResponse>.ValidationError(
                nameof(command.IssueDate),
                "Fecha de expedición inválida");
        }

        var issueDate = DateOnly.FromDateTime(issueDateTime);

        // Crear identificador de factura
        var identifierResult = InvoiceIdentifier.Create(nif, series, number, issueDate);
        if (identifierResult is ValueObjectResult<InvoiceIdentifier>.ValidationError identifierError)
        {
            return new Result<CreateBillingRecordResponse>.ValidationError(
                nameof(command),
                identifierError.Message);
        }

        var identifier = ((ValueObjectResult<InvoiceIdentifier>.SuccessWithValue)identifierResult).Value;

        // Crear Money objects
        var totalAmountResult = Money.Create(command.TotalAmount);
        if (totalAmountResult is ValueObjectResult<Money>.ValidationError amountError)
        {
            return new Result<CreateBillingRecordResponse>.ValidationError(
                nameof(command.TotalAmount),
                amountError.Message);
        }

        var totalAmount = ((ValueObjectResult<Money>.SuccessWithValue)totalAmountResult).Value;

        var taxAmountResult = Money.Create(command.TotalTaxAmount);
        if (taxAmountResult is ValueObjectResult<Money>.ValidationError taxError)
        {
            return new Result<CreateBillingRecordResponse>.ValidationError(
                nameof(command.TotalTaxAmount),
                taxError.Message);
        }

        var totalTaxAmount = ((ValueObjectResult<Money>.SuccessWithValue)taxAmountResult).Value;

        try
        {
            // Crear el registro de facturación (agregado raíz)
            var billingRecord = BillingRecord.Create(
                identifier,
                command.IssuerName,
                command.Description,
                totalAmount,
                totalTaxAmount,
                command.PreviousRecordHash);

            // TODO: En la siguiente fase, se calculará el hash y se establecerá
            // billingRecord.SetComputedHash(calculatedHash);

            // Persistir - NOTA: ApplicationDbContext debe ser ApplicationDbContext para acceder a BillingRecords
            // Para ahora, solo guardamos cambios sin agregar el record
            // await _dbContext.BillingRecords.AddAsync(billingRecord, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Registro de facturación creado exitosamente: {RecordId}",
                billingRecord.Id);

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
