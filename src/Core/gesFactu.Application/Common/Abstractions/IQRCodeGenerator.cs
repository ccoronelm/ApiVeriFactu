namespace gesFactu.Application.Common.Abstractions;

/// <summary>
/// Puerto para generación del QR tributario oficial VERI*FACTU.
/// El entorno TEST/PROD se obtiene de la configuración segura del servidor.
/// </summary>
public interface IQRCodeGenerator
{
    /// <summary>
    /// Genera un PNG QR real con nivel de corrección M.
    /// </summary>
    Task<byte[]> GeneratePngAsync(
        VeriFactuQrData data,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Genera la URL oficial que debe codificarse dentro del QR.
    /// </summary>
    string BuildVerificationUrl(VeriFactuQrData data);
}

/// <summary>
/// Datos fiscales que forman la URL oficial del QR.
/// </summary>
public sealed record VeriFactuQrData
{
    public required string IssuerNif { get; init; }
    public required string InvoiceSeries { get; init; }
    public required string InvoiceNumber { get; init; }
    public required DateOnly IssueDate { get; init; }
    public required decimal TotalAmount { get; init; }
}
