namespace gesFactu.Application.Common.Abstractions;

/// <summary>
/// Puerto para el cálculo de huellas VERI*FACTU.
/// Ref: /VERIFACTU/Veri-Factu_especificaciones_huella_hash_registros.pdf.
/// Algoritmo vigente: SHA-256 (TipoHuella=01), UTF-8, hexadecimal mayúsculas.
/// </summary>
public interface IHashCalculator
{
    string CalculateSha256(string data);

    string CalculateSha256(byte[] data);

    /// <summary>
    /// Calcula la huella oficial de un RegistroAlta.
    /// </summary>
    string CalculateChainHash(BillingRecordHashInput input);

    /// <summary>
    /// Calcula la huella oficial de un RegistroAnulacion.
    /// </summary>
    string CalculateCancellationHash(CancellationRecordHashInput input);
}

/// <summary>
/// Datos oficiales usados en la huella de RegistroAlta.
/// </summary>
public record BillingRecordHashInput
{
    public string PreviousHash { get; init; } = string.Empty;
    public required string IssuerNif { get; init; }
    public required string InvoiceSeries { get; init; }
    public required string InvoiceNumber { get; init; }
    public required string IssueDate { get; init; }
    public string InvoiceType { get; init; } = string.Empty;
    public required decimal TotalAmount { get; init; }
    public required decimal TotalTaxAmount { get; init; }
    public string Description { get; init; } = string.Empty;
    public required string RegisterTimestamp { get; init; }
    public string SoftwareId { get; init; } = string.Empty;
}

/// <summary>
/// Datos oficiales usados en la huella de RegistroAnulacion.
///
/// Cadena:
/// IDEmisorFacturaAnulada=...&NumSerieFacturaAnulada=...&
/// FechaExpedicionFacturaAnulada=...&Huella=...&
/// FechaHoraHusoGenRegistro=...
/// </summary>
public record CancellationRecordHashInput
{
    public string PreviousHash { get; init; } = string.Empty;
    public required string IssuerNif { get; init; }
    public required string InvoiceSeries { get; init; }
    public required string InvoiceNumber { get; init; }
    public required string IssueDate { get; init; }
    public required string RegisterTimestamp { get; init; }
}
