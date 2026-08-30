using gesFactu.Application.Common;
using MediatR;

namespace gesFactu.Application.RegistrosFacturacion.Queries.ObtenerQr;

public sealed record GetBillingRecordQrQuery(int BillingRecordId)
    : IRequest<Result<BillingRecordQrDto>>;

public sealed record BillingRecordQrDto(
    string VerificationUrl,
    byte[] PngBytes);
