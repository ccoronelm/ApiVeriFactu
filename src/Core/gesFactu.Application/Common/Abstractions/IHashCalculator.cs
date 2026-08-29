namespace gesFactu.Application.Common.Abstractions;

/// <summary>
/// Puerto que define el cálculo de la huella/hash de un registro de facturación.
/// 
/// El hash es crítico para:
/// - Encadenamiento de registros
/// - Integridad de datos
/// - Compliance VERI*FACTU
/// 
/// La implementación en Infrastructure DEBE ser determinista y estar basada en
/// la especificación oficial: /VERIFACTU/Veri-Factu_especificaciones_huella_hash_registros.pdf
/// 
/// El algoritmo debe ser SHA256 (TipoHuella=01) según especificación AEAT.
/// </summary>
public interface IHashCalculator
{
    /// <summary>
    /// Calcula el hash SHA256 de los datos de entrada.
    /// Debe ser determinista: mismos datos siempre producen el mismo hash.
    /// </summary>
    /// <param name="data">Datos a hashear (UTF-8)</param>
    /// <returns>Hash en formato hexadecimal en mayúsculas (64 caracteres para SHA256)</returns>
    string CalculateSha256(string data);

    /// <summary>
    /// Calcula el hash SHA256 de datos binarios.
    /// </summary>
    string CalculateSha256(byte[] data);

    /// <summary>
    /// Calcula el hash de encadenamiento para un registro de facturación.
    /// 
    /// Según VERI*FACTU, el input para el hash incluye (en orden):
    /// - Hash del registro anterior
    /// - Identificador del emisor (NIF)
    /// - Serie de factura
    /// - Número de factura
    /// - Fecha de expedición
    /// - Tipo de factura
    /// - Importe total
    /// - Cuota total
    /// - Descripción (si aplica)
    /// - Timestamp
    /// - Información del sistema
    /// 
    /// Ref: VERI*FACTU specification section on "Huella de Encadenamiento"
    /// </summary>
    /// <param name="input">Datos estructurados del registro</param>
    /// <returns>Hash de encadenamiento (formato hexadecimal, 64 caracteres)</returns>
    string CalculateChainHash(BillingRecordHashInput input);
}

/// <summary>
/// Datos estructurados para calcular el hash de un registro.
/// </summary>
public record BillingRecordHashInput
{
    /// <summary>
    /// Hash del registro anterior en la cadena.
    /// Si es el primer registro, usar cadena vacía.
    /// </summary>
    public string PreviousHash { get; init; } = string.Empty;

    /// <summary>
    /// NIF/CIF del emisor (normalizado a mayúsculas).
    /// </summary>
    public required string IssuerNif { get; init; }

    /// <summary>
    /// Serie de la factura.
    /// </summary>
    public required string InvoiceSeries { get; init; }

    /// <summary>
    /// Número de la factura.
    /// </summary>
    public required string InvoiceNumber { get; init; }

    /// <summary>
    /// Fecha de expedición (formato: dd-MM-yyyy).
    /// </summary>
    public required string IssueDate { get; init; }

    /// <summary>
    /// Tipo de factura (ej: "F1", "R3", etc.).
    /// Si no aplica, usar cadena vacía.
    /// </summary>
    public string InvoiceType { get; init; } = string.Empty;

    /// <summary>
    /// Importe total (con máximo 2 decimales, separador punto).
    /// </summary>
    public required decimal TotalAmount { get; init; }

    /// <summary>
    /// Cuota total de impuesto (con máximo 2 decimales, separador punto).
    /// </summary>
    public required decimal TotalTaxAmount { get; init; }

    /// <summary>
    /// Descripción de la operación.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Timestamp del registro (ISO 8601 con zona horaria).
    /// Ej: "2025-02-03T14:30:00+01:00"
    /// </summary>
    public required string RegisterTimestamp { get; init; }

    /// <summary>
    /// Identificador del sistema informático.
    /// </summary>
    public string SoftwareId { get; init; } = string.Empty;
}
