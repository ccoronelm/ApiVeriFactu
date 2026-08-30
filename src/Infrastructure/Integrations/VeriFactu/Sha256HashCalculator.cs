using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using gesFactu.Application.Common.Abstractions;

namespace gesFactu.Infrastructure.VeriFactu;

/// <summary>
/// Implementación del cálculo de huella/hash conforme a la especificación oficial AEAT VERI*FACTU.
///
/// Ref: /VERIFACTU/Veri-Factu_especificaciones_huella_hash_registros.pdf
/// Ref: /VERIFACTU/AnexosEjemplosFirmaRegFact/ejemploRegistro.xml  (vector de prueba)
///
/// Algoritmo:
///   Campos del RegistroAlta (en orden) separados por el carácter '&':
///     1. IDEmisorFactura
///     2. NumSerieFactura
///     3. FechaExpedicionFactura  (dd-MM-yyyy)
///     4. TipoFactura
///     5. CuotaTotal             (número decimal, separador punto, sin ceros innecesarios)
///     6. FechaHoraHusoGenRegistro (dateTime tal como aparece en el XML)
///     7. HuellaAnterior         (hex del registro anterior, o "S" si es el primero)
///   Codificación: UTF-8
///   Hash: SHA-256
///   Salida: hexadecimal en MAYÚSCULAS (64 caracteres)
/// </summary>
public sealed class Sha256HashCalculator : IHashCalculator
{
    /// <summary>
    /// Calcula SHA-256 de una cadena UTF-8. Resultado: hex mayúsculas.
    /// </summary>
    public string CalculateSha256(string data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var bytes = Encoding.UTF8.GetBytes(data);
        return CalculateSha256(bytes);
    }

    /// <summary>
    /// Calcula SHA-256 de bytes. Resultado: hex mayúsculas.
    /// </summary>
    public string CalculateSha256(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var hash = SHA256.HashData(data);
        return Convert.ToHexString(hash); // ya en mayúsculas en .NET 5+
    }

    /// <summary>
    /// Calcula la huella de encadenamiento de un RegistroAlta.
    ///
    /// Ref: /VERIFACTU/Veri-Factu_especificaciones_huella_hash_registros.pdf
    /// Vector verificado: /VERIFACTU/AnexosEjemplosFirmaRegFact/ejemploRegistro.xml
    /// </summary>
    public string CalculateChainHash(BillingRecordHashInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (string.IsNullOrWhiteSpace(input.IssuerNif))
            throw new ArgumentException("IssuerNif es obligatorio para el cálculo de la huella.", nameof(input));

        if (string.IsNullOrWhiteSpace(input.InvoiceNumber))
            throw new ArgumentException("InvoiceNumber es obligatorio para el cálculo de la huella.", nameof(input));

        if (string.IsNullOrWhiteSpace(input.IssueDate))
            throw new ArgumentException("IssueDate es obligatorio para el cálculo de la huella.", nameof(input));

        if (string.IsNullOrWhiteSpace(input.RegisterTimestamp))
            throw new ArgumentException("RegisterTimestamp es obligatorio para el cálculo de la huella.", nameof(input));

        // NIF siempre en mayúsculas (normalización defensiva)
        var nif = input.IssuerNif.ToUpperInvariant();

        // NumSerieFactura = serie + número combinados tal como van en el XML
        var numSerieFactura = string.IsNullOrWhiteSpace(input.InvoiceSeries)
            ? input.InvoiceNumber
            : input.InvoiceSeries + input.InvoiceNumber;

        // Fecha en formato dd-MM-yyyy (formato AEAT)
        var fechaExpedicion = input.IssueDate; // ya debe venir formateada correctamente

        // TipoFactura ("F1" para ordinaria, "R3", etc.)
        var tipoFactura = input.InvoiceType;

        // CuotaTotal: decimal con punto como separador, sin ceros innecesarios
        // Conforme a ImporteSgn12.2Type: (\+|-)?\d{1,12}(\.\d{0,2})?
        var cuotaTotal = FormatDecimalParaHash(input.TotalTaxAmount);

        // FechaHoraHusoGenRegistro: dateTime tal como se escribe en el XML
        // Ej: "2025-02-03T14:30:00+01:00"
        var fechaHoraHuso = input.RegisterTimestamp;

        // Huella anterior: hex del registro anterior, o "S" si es el primero
        var huellaAnterior = string.IsNullOrWhiteSpace(input.PreviousHash)
            ? "S"
            : input.PreviousHash;

        // Concatenar con '&' en el orden definido por la especificación
        var cadena = string.Join("&",
            nif,
            numSerieFactura,
            fechaExpedicion,
            tipoFactura,
            cuotaTotal,
            fechaHoraHuso,
            huellaAnterior);

        return CalculateSha256(cadena);
    }

    /// <summary>
    /// Formatea un decimal para incluirlo en la cadena de hash.
    /// Usa separador de punto, elimina ceros de relleno innecesarios al final
    /// pero mantiene al menos el entero. Ej: 41.4 ? "41.4", 100.00 ? "100", 21.00 ? "21".
    ///
    /// Nota: el ejemplo oficial muestra CuotaTotal="41.4" (no "41.40"),
    /// lo que indica que AEAT usa el valor sin zeros trailing.
    /// Ref: /VERIFACTU/AnexosEjemplosFirmaRegFact/ejemploRegistro.xml
    /// </summary>
    internal static string FormatDecimalParaHash(decimal value)
    {
        // G29 elimina ceros trailing; InvariantCulture garantiza punto como separador
        // Pero limitamos a 2 decimales según la especificación del tipo ImporteSgn12.2Type
        var con2decimales = Math.Round(value, 2, MidpointRounding.AwayFromZero);
        var str = con2decimales.ToString("G29", CultureInfo.InvariantCulture);

        // Eliminar ceros trailing después del punto decimal
        if (str.Contains('.'))
        {
            str = str.TrimEnd('0').TrimEnd('.');
        }

        return str;
    }
}
