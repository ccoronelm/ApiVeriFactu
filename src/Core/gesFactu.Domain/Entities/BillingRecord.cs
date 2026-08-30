using System.Globalization;
using gesFactu.Domain.Common;
using gesFactu.Domain.ValueObjects;

namespace gesFactu.Domain.Entities;

/// <summary>
/// Registro de facturación (alta) en VERI*FACTU.
/// Es el agregado raíz que encapsula el estado de una factura registrada.
///
/// Ref: /VERIFACTU/SuministroInformacion.xsd.xml - RegistroFacturacionAltaType
/// </summary>
public class BillingRecord : BaseDomainModel
{
    public required InvoiceIdentifier InvoiceIdentifier { get; set; }

    public string IssuerNif { get; set; } = string.Empty;
    public string InvoiceSeries { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateOnly IssueDate { get; set; }

    public required string IssuerName { get; set; }
    public required string Description { get; set; }

    public decimal TotalAmount { get; set; }
    public decimal TotalTaxAmount { get; set; }

    /// <summary>
    /// Valor exacto usado en FechaHoraHusoGenRegistro y en el cálculo de la huella.
    /// Se persiste para garantizar que hash y XML usan exactamente el mismo valor.
    /// Formato canónico generado por gesFactu: yyyy-MM-ddTHH:mm:sszzz.
    /// </summary>
    public string RegisterTimestamp { get; set; } = string.Empty;

    /// <summary>
    /// Huella del registro anterior. Null si es el primer registro.
    /// </summary>
    public string? PreviousRecordHash { get; set; }

    /// <summary>
    /// Huella calculada para este registro.
    /// </summary>
    public string? ComputedHash { get; set; }

    public bool IsSubmitted { get; set; }
    public string? AeatSubmissionId { get; set; }
    public string Status { get; set; } = "Pendiente";

    private BillingRecord() { }

    public static BillingRecord Create(
        InvoiceIdentifier invoiceIdentifier,
        string issuerName,
        string description,
        Money totalAmount,
        Money totalTaxAmount,
        string? previousRecordHash = null,
        string? registerTimestamp = null)
    {
        if (string.IsNullOrWhiteSpace(issuerName))
            throw new InvalidOperationException("El nombre del emisor es requerido");

        if (string.IsNullOrWhiteSpace(description))
            throw new InvalidOperationException("La descripción de la operación es requerida");

        if (totalTaxAmount.Amount > totalAmount.Amount)
            throw new InvalidOperationException("La cuota de impuesto no puede ser mayor que el importe total");

        var timestamp = string.IsNullOrWhiteSpace(registerTimestamp)
            ? DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture)
            : registerTimestamp.Trim();

        return new BillingRecord
        {
            InvoiceIdentifier = invoiceIdentifier,
            IssuerNif = invoiceIdentifier.IssuerNif.Value,
            InvoiceSeries = invoiceIdentifier.Series.Value,
            InvoiceNumber = invoiceIdentifier.Number.Value,
            IssueDate = invoiceIdentifier.IssueDate,
            IssuerName = issuerName,
            Description = description,
            TotalAmount = totalAmount.Amount,
            TotalTaxAmount = totalTaxAmount.Amount,
            RegisterTimestamp = timestamp,
            PreviousRecordHash = previousRecordHash,
            Status = "Pendiente",
            IsSubmitted = false
        };
    }

    public void MarkAsSubmitted(string aeatSubmissionId)
    {
        if (string.IsNullOrWhiteSpace(aeatSubmissionId))
            throw new InvalidOperationException("El ID de envío de AEAT es requerido");

        IsSubmitted = true;
        AeatSubmissionId = aeatSubmissionId;
        Status = "Enviado";
    }

    public void MarkAsAccepted()
    {
        Status = "Aceptado";
    }

    public void MarkAsRejected(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Debe proporcionar un motivo de rechazo");

        Status = $"Rechazado: {reason}";
    }

    public void SetComputedHash(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
            throw new InvalidOperationException("El hash no puede estar vacío");

        ComputedHash = hash;
    }

    public virtual ICollection<SubmissionAttempt> SubmissionAttempts { get; set; } =
        new HashSet<SubmissionAttempt>();
}
