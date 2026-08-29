using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using gesFactu.Application.Common.Abstractions;

namespace gesFactu.Infrastructure.VeriFactu;

/// <summary>
/// Implementación del cálculo de hash SHA256 para registros VERI*FACTU.
/// 
/// El hash es determinista y culture-independent.
/// Sigue la especificación oficial AEAT para TipoHuella=01 (SHA256).
/// 
/// Ref: /VERIFACTU/Veri-Factu_especificaciones_huella_hash_registros.pdf
/// </summary>
public sealed class Sha256HashCalculator : IHashCalculator
{
    /// <summary>
    /// Calcula el hash SHA256 de una cadena de texto.
    /// Usa UTF-8 sin BOM para consistencia.
    /// </summary>
    public string CalculateSha256(string data)
    {
        var bytes = Encoding.UTF8.GetBytes(data);
        return CalculateSha256(bytes);
    }

    /// <summary>
    /// Calcula el hash SHA256 de datos binarios.
    /// </summary>
    public string CalculateSha256(byte[] data)
    {
        using (var sha256 = SHA256.Create())
        {
            var hashBytes = sha256.ComputeHash(data);

            // Convertir a hexadecimal en mayúsculas (formato AEAT)
            return BitConverter.ToString(hashBytes).Replace("-", "").ToUpperInvariant();
        }
    }

    /// <summary>
    /// Calcula el hash de encadenamiento según la especificación VERI*FACTU.
    /// 
    /// El input se construye concatenando los campos en el orden exacto requerido,
    /// con separadores específicos y formatos garantizados (sin cultura local).
    /// 
    /// Orden de campos (según especificación oficial):
    /// 1. Hash anterior
    /// 2. NIF emisor
    /// 3. Serie factura
    /// 4. Número factura
    /// 5. Fecha expedición (dd-MM-yyyy)
    /// 6. Tipo factura
    /// 7. Importe total
    /// 8. Cuota total
    /// 9. Descripción
    /// 10. Timestamp
    /// 11. Id sistema
    /// 
    /// Separador entre campos: pipe (|)
    /// Importes: siempre con punto decimal, máximo 2 decimales
    /// </summary>
    public string CalculateChainHash(BillingRecordHashInput input)
    {
        // Validaciones
        if (input == null)
            throw new ArgumentNullException(nameof(input));

        if (string.IsNullOrEmpty(input.IssuerNif))
            throw new ArgumentException("NIF del emisor es requerido", nameof(input));

        if (string.IsNullOrEmpty(input.InvoiceSeries))
            throw new ArgumentException("Serie de factura es requerida", nameof(input));

        if (string.IsNullOrEmpty(input.InvoiceNumber))
            throw new ArgumentException("Número de factura es requerido", nameof(input));

        if (string.IsNullOrEmpty(input.IssueDate))
            throw new ArgumentException("Fecha de expedición es requerida", nameof(input));

        if (string.IsNullOrEmpty(input.RegisterTimestamp))
            throw new ArgumentException("Timestamp del registro es requerido", nameof(input));

        // Normalizar y formatear datos según especificación
        var previousHash = input.PreviousHash ?? string.Empty;
        var issuerNif = input.IssuerNif.ToUpperInvariant();
        var invoiceSeries = input.InvoiceSeries.Trim();
        var invoiceNumber = input.InvoiceNumber.Trim();
        var issueDate = input.IssueDate.Trim();
        var invoiceType = (input.InvoiceType ?? string.Empty).Trim();
        var description = (input.Description ?? string.Empty).Trim();
        var softwareId = (input.SoftwareId ?? string.Empty).Trim();

        // Formatear importes: siempre con punto decimal, máximo 2 decimales, usando InvariantCulture
        var totalAmountStr = input.TotalAmount.ToString("0.00", CultureInfo.InvariantCulture);
        var totalTaxStr = input.TotalTaxAmount.ToString("0.00", CultureInfo.InvariantCulture);

        // Construir el string de entrada para el hash
        // Orden exacto según VERI*FACTU
        var hashInput = string.Join("|",
            previousHash,
            issuerNif,
            invoiceSeries,
            invoiceNumber,
            issueDate,
            invoiceType,
            totalAmountStr,
            totalTaxStr,
            description,
            input.RegisterTimestamp,
            softwareId
        );

        // Calcular y retornar hash
        return CalculateSha256(hashInput);
    }
}
