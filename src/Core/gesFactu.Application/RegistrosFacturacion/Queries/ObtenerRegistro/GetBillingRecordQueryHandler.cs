using MediatR;
using Microsoft.Extensions.Logging;
using gesFactu.Application.Common;
using gesFactu.Application.Common.Abstractions;

namespace gesFactu.Application.RegistrosFacturacion.Queries.ObtenerRegistro;

/// <summary>
/// Handler para obtener un registro de facturación.
/// </summary>
public sealed class GetBillingRecordQueryHandler
    : IRequestHandler<GetBillingRecordQuery, Result<BillingRecordDto>>
{
    private readonly IBillingRecordRepository _repository;
    private readonly ILogger<GetBillingRecordQueryHandler> _logger;

    public GetBillingRecordQueryHandler(
        IBillingRecordRepository repository,
        ILogger<GetBillingRecordQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Result<BillingRecordDto>> Handle(
        GetBillingRecordQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Obteniendo registro de facturación: {Id}", query.BillingRecordId);

        try
        {
            var record = await _repository.GetByIdAsync(query.BillingRecordId, cancellationToken);

            if (record == null)
            {
                _logger.LogWarning("Registro no encontrado: {Id}", query.BillingRecordId);
                return new Result<BillingRecordDto>.NotFoundError("BillingRecord", query.BillingRecordId.ToString());
            }

            var invoiceId = $"{record.IssuerNif}/{record.FiscalInvoiceNumber}";

            var dto = new BillingRecordDto(
                record.Id,
                invoiceId,
                record.IssuerName,
                record.RecipientNif,
                record.RecipientName,
                record.Description,
                record.TotalAmount,
                record.TotalTaxAmount,
                record.Status,
                record.ComputedHash,
                record.PreviousRecordHash,
                record.AeatSubmissionId,
                record.SubmissionCorrelationId?.ToString(),
                record.IsSubmitted,
                record.CreateDate,
                record.CreatedBy,
                record.SubsanatesBillingRecordId,
                record.SubsanatesBillingRecordId.HasValue
            );

            return new Result<BillingRecordDto>.SuccessWithValue(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener registro: {Id}", query.BillingRecordId);
            return new Result<BillingRecordDto>.UnexpectedError($"Error: {ex.Message}");
        }
    }
}
