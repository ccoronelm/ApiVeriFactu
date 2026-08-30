using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using gesFactu.Application.Common.Abstractions;

namespace gesFactu.Infrastructure.VeriFactu;

/// <summary>
/// Implementación del cálculo de huella/hash conforme a la especificación oficial AEAT VERI*FACTU.
///
/// Ref: /VERIFACTU/Veri-Factu_especificaciones_huella_hash_registros.pdf
///
/// Para RegistroAlta la cadena de entrada debe ser exactamente:
/// IDEmisorFactura=...&NumSerieFactura=...&FechaExpedicionFactura=...&TipoFactura=...&
/// CuotaTotal=...&ImporteTotal=...&Huella=...&FechaHoraHusoGenRegistro=...
///
/// Los valores se toman tal como aparecen en el XML, eliminando únicamente espacios al inicio/final.
/// Los importes deben conservar la representación usada en el XML (dos decimales).
/// Para el primer registro, Huella se informa vacía en la cadena de cálculo.
/// Codificación: UTF-8. Algoritmo: SHA-256. Salida: hexadecimal en mayúsculas.
/// </summary>
public sealed class Sha256HashCalculator : IHashCalculator
{
    public string CalculateSha256(string data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return CalculateSha256(Encoding.UTF8.GetBytes(data));
    }

    public string CalculateSha256(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return Convert.ToHexString(SHA256.HashData(data));
    }

    /// <summary>
    /// Calcula la huella de un RegistroAlta.
    /// Ref: documento oficial, apartados 3 y 6.1/6.2.
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

        if (string.IsNullOrWhiteSpace(input.InvoiceType))
            throw new ArgumentException("InvoiceType es obligatorio para el cálculo de la huella de un RegistroAlta.", nameof(input));

        if (string.IsNullOrWhiteSpace(input.RegisterTimestamp))
            throw new ArgumentException("RegisterTimestamp es obligatorio para el cálculo de la huella.", nameof(input));

        var nif = input.IssuerNif.Trim().ToUpperInvariant();
        var numSerieFactura = string.IsNullOrWhiteSpace(input.InvoiceSeries)
            ? input.InvoiceNumber.Trim()
            : input.InvoiceSeries.Trim() + input.InvoiceNumber.Trim();

        var fechaExpedicion = input.IssueDate.Trim();
        var tipoFactura = input.InvoiceType.Trim();
        var cuotaTotal = FormatDecimalParaHash(input.TotalTaxAmount);
        var importeTotal = FormatDecimalParaHash(input.TotalAmount);
        var huellaAnterior = input.PreviousHash?.Trim() ?? string.Empty;
        var fechaHoraHuso = input.RegisterTimestamp.Trim();

        var cadena =
            $"IDEmisorFactura={nif}" +
            $"&NumSerieFactura={numSerieFactura}" +
            $"&FechaExpedicionFactura={fechaExpedicion}" +
            $"&TipoFactura={tipoFactura}" +
            $"&CuotaTotal={cuotaTotal}" +
            $"&ImporteTotal={importeTotal}" +
            $"&Huella={huellaAnterior}" +
            $"&FechaHoraHusoGenRegistro={fechaHoraHuso}";

        return CalculateSha256(cadena);
    }

    /// <summary>
    /// Formatea importes exactamente igual que RegistroAltaXmlBuilder.FormatImporte:
    /// siempre dos decimales con punto como separador. La huella se calcula sobre
    /// la representación textual enviada a AEAT, por lo que 21 debe ser "21.00".
    /// </summary>
    internal static string FormatDecimalParaHash(decimal value)
        => value.ToString("0.00", CultureInfo.InvariantCulture);
}
