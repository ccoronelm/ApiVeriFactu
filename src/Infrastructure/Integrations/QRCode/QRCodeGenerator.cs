using gesFactu.Application.Common.Abstractions;

namespace gesFactu.Infrastructure.Integrations.QRCode;

/// <summary>
/// Generador de códigos QR conforme a VERI*FACTU.
/// 
/// Esta implementación genera el contenido del QR (la URL).
/// El cliente puede usar cualquier librería QR (QRCoder, Zxing, etc) para renderizar como imagen.
/// 
/// Alternativa: Para integración completa con imagen PNG, usar una librería como QRCoder en la capa de presentación.
/// </summary>
public class QRCodeGenerator : IQRCodeGenerator
{
    /// <summary>
    /// URL base de AEAT para verificación de registros VERI*FACTU.
    /// </summary>
    private const string AeatVerifyBaseUrl = "https://www.aeat.es/verifactu";

    public async Task<byte[]> GenerateAsync(
        string submissionId,
        string recordHash,
        DateOnly issueDate,
        CancellationToken cancellationToken = default)
    {
        // Esta implementación genera solo el contenido del QR (la URL).
        // Para generar una imagen PNG, el cliente puede usar QRCoder o similar.
        // Para simplicidad en este MVP, retornamos la URL codificada en UTF8.

        var qrContent = GenerateQRContent(submissionId, recordHash, issueDate);
        return await Task.FromResult(System.Text.Encoding.UTF8.GetBytes(qrContent));
    }

    public string GenerateQRContent(
        string submissionId,
        string recordHash,
        DateOnly issueDate)
    {
        if (string.IsNullOrWhiteSpace(submissionId))
            throw new ArgumentException("SubmissionId no puede estar vacío", nameof(submissionId));

        if (string.IsNullOrWhiteSpace(recordHash))
            throw new ArgumentException("RecordHash no puede estar vacío", nameof(recordHash));

        // Formato conforme a VERI*FACTU
        // https://www.aeat.es/verifactu?ID={SubmissionId}&HASH={Hash}&FECHA={Fecha}
        var qrUrl = $"{AeatVerifyBaseUrl}?ID={Uri.EscapeDataString(submissionId)}&HASH={Uri.EscapeDataString(recordHash)}&FECHA={issueDate:yyyyMMdd}";

        return qrUrl;
    }
}
