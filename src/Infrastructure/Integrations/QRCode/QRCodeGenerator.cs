using System.Globalization;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Infrastructure.Integrations.VeriFactu;
using Microsoft.Extensions.Options;
using QRCoder;

namespace gesFactu.Infrastructure.Integrations.QRCode;

/// <summary>
/// Generador oficial de QR tributario VERI*FACTU.
/// </summary>
public sealed class QRCodeGenerator : IQRCodeGenerator
{
    private const string TestBaseUrl =
        "https://prewww2.aeat.es/wlpl/TIKE-CONT/ValidarQR";

    private const string ProductionBaseUrl =
        "https://www2.agenciatributaria.gob.es/wlpl/TIKE-CONT/ValidarQR";

    private readonly VeriFactuOptions _options;

    public QRCodeGenerator(IOptions<VeriFactuOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public Task<byte[]> GeneratePngAsync(
        VeriFactuQrData data,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var content = BuildVerificationUrl(data);

        using var generator = new QRCoder.QRCodeGenerator();
        using var qrData = generator.CreateQrCode(
            content,
            QRCoder.QRCodeGenerator.ECCLevel.M);

        var png = new PngByteQRCode(qrData);
        var bytes = png.GetGraphic(pixelsPerModule: 20);

        return Task.FromResult(bytes);
    }

    public string BuildVerificationUrl(VeriFactuQrData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var nif = data.IssuerNif?.Trim().ToUpperInvariant();
        var series = data.InvoiceSeries?.Trim() ?? string.Empty;
        var number = data.InvoiceNumber?.Trim() ?? string.Empty;
        var fullInvoiceNumber = series + number;

        if (string.IsNullOrWhiteSpace(nif) || nif.Length != 9)
            throw new ArgumentException(
                "IssuerNif debe tener 9 caracteres.",
                nameof(data));

        if (string.IsNullOrWhiteSpace(fullInvoiceNumber) ||
            fullInvoiceNumber.Length > 60)
        {
            throw new ArgumentException(
                "Número de serie + número de factura debe tener entre 1 y 60 caracteres.",
                nameof(data));
        }

        if (fullInvoiceNumber.Any(ch => ch < 32 || ch > 126))
            throw new ArgumentException(
                "Número de factura solo puede contener caracteres ASCII imprimibles.",
                nameof(data));

        var integerDigits = decimal.Truncate(decimal.Abs(data.TotalAmount))
            .ToString("0", CultureInfo.InvariantCulture)
            .Length;

        if (integerDigits > 12)
            throw new ArgumentException(
                "El importe total no puede superar 12 dígitos en la parte entera.",
                nameof(data));

        if (decimal.Round(data.TotalAmount, 2) != data.TotalAmount)
            throw new ArgumentException(
                "El importe total admite como máximo 2 decimales.",
                nameof(data));

        var baseUrl = _options.Environment switch
        {
            VeriFactuEntorno.Test => TestBaseUrl,
            VeriFactuEntorno.Production when _options.AllowProduction =>
                ProductionBaseUrl,
            VeriFactuEntorno.Production =>
                throw new InvalidOperationException(
                    "Producción está bloqueada: VeriFactu:AllowProduction debe ser true."),
            _ => throw new InvalidOperationException(
                $"Entorno VERI*FACTU no soportado: {_options.Environment}.")
        };

        var amount = data.TotalAmount.ToString(
            "0.##",
            CultureInfo.InvariantCulture);

        return $"{baseUrl}?nif={Uri.EscapeDataString(nif)}" +
               $"&numserie={Uri.EscapeDataString(fullInvoiceNumber)}" +
               $"&fecha={data.IssueDate:dd-MM-yyyy}" +
               $"&importe={Uri.EscapeDataString(amount)}";
    }
}
