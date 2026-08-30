using System.Globalization;
using gesFactu.Domain.Common;
using gesFactu.Domain.ValueObjects;

namespace gesFactu.Domain.Entities;

/// <summary>
/// Registro de facturación (alta) en VERI*FACTU.
/// </summary>
public class BillingRecord : BaseDomainModel
{
    public const string AltaRecordType = "Alta";
    public const string CancellationRecordType = "Anulacion";

    public required InvoiceIdentifier InvoiceIdentifier { get; set; }

    /// <summary>
    /// Tipo técnico del registro de facturación en la cadena del SIF:
    /// Alta o Anulacion.
    /// </summary>
    public string RecordType { get; set; } = AltaRecordType;

    /// <summary>
    /// Tipo fiscal AEAT del RegistroAlta: F1, F2, R1...R5, F3.
    /// Para RegistroAnulacion conserva el tipo de la factura anulada.
    /// </summary>
    public string InvoiceType { get; set; } = "F1";

    public string IssuerNif { get; set; } = string.Empty;
    public string InvoiceSeries { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>
    /// NumSerieFactura exactamente como se identifica fiscalmente ante AEAT:
    /// serie + número, sin separador añadido por gesFactu.
    /// </summary>
    public string FiscalInvoiceNumber { get; set; } = string.Empty;

    public DateOnly IssueDate { get; set; }

    public required string IssuerName { get; set; }

    /// <summary>
    /// Destinatario obligatorio para facturas F1.
    /// </summary>
    public string RecipientNif { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;

    public required string Description { get; set; }

    public decimal TotalAmount { get; set; }
    public decimal TotalTaxAmount { get; set; }

    /// <summary>
    /// Valor exacto usado en FechaHoraHusoGenRegistro y en la huella.
    /// </summary>
    public string RegisterTimestamp { get; set; } = string.Empty;

    /// <summary>
    /// Si este RegistroAlta es una subsanación, identifica el registro local
    /// cuyos datos se están subsanando. Null para altas iniciales.
    /// </summary>
    public int? SubsanatesBillingRecordId { get; set; }

    /// <summary>
    /// Si este RF es RegistroAnulacion, identifica el registro local cuya
    /// identidad fiscal se anula. El registro de anulación participa en la
    /// misma cadena de huellas que las altas.
    /// </summary>
    public int? CancelsBillingRecordId { get; set; }

    /// <summary>
    /// Registro local cuya factura rectifica este RegistroAlta R1-R5.
    /// </summary>
    public int? RectifiesBillingRecordId { get; set; }

    /// <summary>
    /// Tipo de rectificación AEAT: I (incremental) o S (sustitutiva).
    /// </summary>
    public string? RectificationType { get; set; }

    /// <summary>
    /// Importes sustituidos, solo aplicables a rectificativas sustitutivas.
    /// </summary>
    public decimal? RectifiedBaseAmount { get; set; }
    public decimal? RectifiedTaxAmount { get; set; }
    public decimal? RectifiedSurchargeAmount { get; set; }

    /// <summary>
    /// Identificador interno del RF inmediatamente anterior de la cadena.
    /// Null solo para el primer RF del obligado tributario en este SIF.
    /// </summary>
    public int? PreviousBillingRecordId { get; set; }

    /// <summary>
    /// Huella del RF inmediatamente anterior.
    /// </summary>
    public string? PreviousRecordHash { get; set; }

    public string? ComputedHash { get; set; }

    public bool IsSubmitted { get; set; }

    /// <summary>
    /// Correlación local de la remisión asíncrona. No es un identificador AEAT.
    /// </summary>
    public Guid? SubmissionCorrelationId { get; set; }

    /// <summary>
    /// CSV real devuelto por AEAT cuando el envío no es rechazado.
    /// </summary>
    public string? AeatSubmissionId { get; set; }

    public string Status { get; set; } = "Pendiente";

    private BillingRecord() { }

    public static BillingRecord Create(
        InvoiceIdentifier invoiceIdentifier,
        string issuerName,
        string recipientNif,
        string recipientName,
        string description,
        Money totalAmount,
        Money totalTaxAmount,
        int? previousBillingRecordId = null,
        string? previousRecordHash = null,
        string? registerTimestamp = null,
        string invoiceType = "F1")
    {
        ArgumentNullException.ThrowIfNull(invoiceIdentifier);

        if (string.IsNullOrWhiteSpace(issuerName))
            throw new InvalidOperationException("El nombre del emisor es requerido");

        if (issuerName.Trim().Length > 120)
            throw new InvalidOperationException("El nombre del emisor no puede superar 120 caracteres");

        var normalizedInvoiceType = invoiceType?.Trim().ToUpperInvariant() ?? string.Empty;

        if (normalizedInvoiceType is not ("F1" or "F2" or "R1" or "R2" or "R3" or "R4" or "R5"))
        {
            throw new InvalidOperationException(
                "TipoFactura no soportado. Valores admitidos: F1, F2, R1, R2, R3, R4 y R5.");
        }

        var hasRecipientNif = !string.IsNullOrWhiteSpace(recipientNif);
        var hasRecipientName = !string.IsNullOrWhiteSpace(recipientName);

        if (normalizedInvoiceType == "F1" && (!hasRecipientNif || !hasRecipientName))
        {
            throw new InvalidOperationException(
                "F1 requiere NIF y nombre o razón social del destinatario.");
        }

        if (hasRecipientNif != hasRecipientName)
        {
            throw new InvalidOperationException(
                "NIF y nombre del destinatario deben informarse juntos.");
        }

        if (hasRecipientNif)
        {
            if (recipientNif.Trim().Length != 9)
                throw new InvalidOperationException(
                    "El NIF del destinatario debe tener exactamente 9 caracteres");

            if (string.Equals(
                    invoiceIdentifier.IssuerNif.Value,
                    recipientNif.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "El NIF del destinatario debe ser distinto del NIF del obligado emisor");
            }

            if (recipientName.Trim().Length > 120)
                throw new InvalidOperationException(
                    "El nombre o razón social del destinatario no puede superar 120 caracteres");
        }

        if (string.IsNullOrWhiteSpace(description))
            throw new InvalidOperationException("La descripción de la operación es requerida");

        if (description.Trim().Length > 500)
            throw new InvalidOperationException("La descripción de la operación no puede superar 500 caracteres");

        if (normalizedInvoiceType is "F1" or "F2" &&
            totalTaxAmount.Amount > totalAmount.Amount)
        {
            throw new InvalidOperationException(
                "La cuota de impuesto no puede ser mayor que el importe total");
        }

        if (previousBillingRecordId.HasValue != !string.IsNullOrWhiteSpace(previousRecordHash))
        {
            throw new InvalidOperationException(
                "PreviousBillingRecordId y PreviousRecordHash deben informarse juntos.");
        }

        var fiscalInvoiceNumber =
            invoiceIdentifier.Series.Value.Trim() +
            invoiceIdentifier.Number.Value.Trim();

        if (fiscalInvoiceNumber.Length > 60)
        {
            throw new InvalidOperationException(
                "NumSerieFactura no puede superar 60 caracteres.");
        }

        var timestamp = string.IsNullOrWhiteSpace(registerTimestamp)
            ? DateTimeOffset.Now.ToString(
                "yyyy-MM-ddTHH:mm:sszzz",
                CultureInfo.InvariantCulture)
            : registerTimestamp.Trim();

        return new BillingRecord
        {
            InvoiceIdentifier = invoiceIdentifier,
            IssuerNif = invoiceIdentifier.IssuerNif.Value,
            InvoiceSeries = invoiceIdentifier.Series.Value,
            InvoiceNumber = invoiceIdentifier.Number.Value,
            FiscalInvoiceNumber = fiscalInvoiceNumber,
            IssueDate = invoiceIdentifier.IssueDate,
            IssuerName = issuerName.Trim(),
            RecipientNif = hasRecipientNif
                ? recipientNif.Trim().ToUpperInvariant()
                : string.Empty,
            RecipientName = hasRecipientName
                ? recipientName.Trim()
                : string.Empty,
            Description = description.Trim(),
            TotalAmount = totalAmount.Amount,
            TotalTaxAmount = totalTaxAmount.Amount,
            RegisterTimestamp = timestamp,
            RecordType = AltaRecordType,
            InvoiceType = normalizedInvoiceType,
            PreviousBillingRecordId = previousBillingRecordId,
            PreviousRecordHash = previousRecordHash,
            Status = "Pendiente",
            IsSubmitted = false
        };
    }

    public void MarkAsQueued(Guid correlationId)
    {
        if (correlationId == Guid.Empty)
            throw new InvalidOperationException("CorrelationId no puede estar vacío");

        IsSubmitted = true;
        SubmissionCorrelationId = correlationId;
        Status = "PendienteEnvio";
    }

    public void MarkAsSubmitted(string aeatSubmissionId)
    {
        if (string.IsNullOrWhiteSpace(aeatSubmissionId))
            throw new InvalidOperationException("El CSV/ID de envío de AEAT es requerido");

        IsSubmitted = true;
        AeatSubmissionId = aeatSubmissionId.Trim();
        Status = "Enviado";
    }

    public void MarkAsAccepted() => Status = "Aceptado";

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

    public virtual ICollection<BillingTaxDetail> TaxDetails { get; set; } =
        new HashSet<BillingTaxDetail>();

    public virtual ICollection<SubmissionAttempt> SubmissionAttempts { get; set; } =
        new HashSet<SubmissionAttempt>();

    public void SetTaxDetails(IEnumerable<BillingTaxDetail> details)
    {
        ArgumentNullException.ThrowIfNull(details);

        var normalized = details.ToList();
        if (normalized.Count is < 1 or > 12)
            throw new InvalidOperationException(
                "VERI*FACTU requiere entre 1 y 12 detalles de desglose.");

        TaxDetails.Clear();
        foreach (var detail in normalized)
            TaxDetails.Add(detail);
    }
}
