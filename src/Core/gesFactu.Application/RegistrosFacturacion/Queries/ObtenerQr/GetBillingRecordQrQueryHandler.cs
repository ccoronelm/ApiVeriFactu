using gesFactu.Application.Common;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Domain.Entities;
using MediatR;

namespace gesFactu.Application.RegistrosFacturacion.Queries.ObtenerQr;

public sealed class GetBillingRecordQrQueryHandler
    : IRequestHandler<GetBillingRecordQrQuery, Result<BillingRecordQrDto>>
{
    private readonly IBillingRecordRepository _repository;
    private readonly IQRCodeGenerator _qr;

    public GetBillingRecordQrQueryHandler(
        IBillingRecordRepository repository,
        IQRCodeGenerator qr)
    {
        _repository = repository;
        _qr = qr;
    }

    public async Task<Result<BillingRecordQrDto>> Handle(
        GetBillingRecordQrQuery query,
        CancellationToken cancellationToken)
    {
        var record = await _repository.GetByIdAsync(
            query.BillingRecordId,
            cancellationToken);

        if (record is null)
        {
            return new Result<BillingRecordQrDto>.NotFoundError(
                "BillingRecord",
                query.BillingRecordId.ToString());
        }

        if (record.RecordType == BillingRecord.CancellationRecordType)
        {
            return new Result<BillingRecordQrDto>.DomainError(
                "QR_NOT_APPLICABLE",
                "Un RegistroAnulacion no representa una factura emitida y no genera QR tributario.");
        }

        var data = new VeriFactuQrData
        {
            IssuerNif = record.IssuerNif,
            InvoiceSeries = record.InvoiceSeries,
            InvoiceNumber = record.InvoiceNumber,
            IssueDate = record.IssueDate,
            TotalAmount = record.TotalAmount
        };

        try
        {
            var url = _qr.BuildVerificationUrl(data);
            var png = await _qr.GeneratePngAsync(data, cancellationToken);

            return new Result<BillingRecordQrDto>.SuccessWithValue(
                new BillingRecordQrDto(url, png));
        }
        catch (ArgumentException ex)
        {
            return new Result<BillingRecordQrDto>.DomainError(
                "INVALID_QR_DATA",
                ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return new Result<BillingRecordQrDto>.DomainError(
                "QR_ENVIRONMENT_BLOCKED",
                ex.Message);
        }
    }
}
