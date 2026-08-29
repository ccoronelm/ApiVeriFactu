using gesFactu.Domain.Common;
using gesFactu.Domain.ValueObjects;

namespace gesFactu.Domain.Entities;

/// <summary>
/// Registro de facturación (alta) en VERI*FACTU.
/// Es el agregado raíz que encapsula el estado de una factura registrada.
/// 
/// Ref: /VERIFACTU/SuministroInformacion.xsd.xml - RegistroFacturacionAltaType
/// 
/// Nota de persistencia: Para simplificar el mapeo EF Core en MVP,
/// se desnormalizan los Value Objects en propiedades escalares.
/// </summary>
public class BillingRecord : BaseDomainModel
{
    /// <summary>
    /// Identificador único de la factura (Value Object).
    /// Internamente compuesto por NIF del emisor, serie, número e fecha de emisión.
    /// </summary>
    public required InvoiceIdentifier InvoiceIdentifier { get; set; }

    /// <summary>
    /// NIF/CIF del emisor (desnormalizado para EF Core).
    /// </summary>
    public string IssuerNif { get; set; } = string.Empty;

    /// <summary>
    /// Serie de la factura (desnormalizado para EF Core).
    /// </summary>
    public string InvoiceSeries { get; set; } = string.Empty;

    /// <summary>
    /// Número de la factura (desnormalizado para EF Core).
    /// </summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>
    /// Fecha de emisión (desnormalizado para EF Core).
    /// </summary>
    public DateOnly IssueDate { get; set; }

    /// <summary>
    /// Nombre o razón social del emisor de la factura.
    /// </summary>
    public required string IssuerName { get; set; }

    /// <summary>
    /// Descripción de la operación / concepto.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Importe total de la factura (base + impuestos).
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Cuota total de impuesto.
    /// </summary>
    public decimal TotalTaxAmount { get; set; }

    /// <summary>
    /// Hash/huella del registro anterior en la cadena (para encadenamiento).
    /// Null si es el primer registro de la serie.
    /// </summary>
    public string? PreviousRecordHash { get; set; }

    /// <summary>
    /// Hash/huella calculada para este registro.
    /// Se calcula según la especificación de VERI*FACTU.
    /// </summary>
    public string? ComputedHash { get; set; }

    /// <summary>
    /// Indica si el registro ha sido enviado a AEAT.
    /// </summary>
    public bool IsSubmitted { get; set; }

    /// <summary>
    /// Identificador de envío asignado por AEAT (si fue enviado).
    /// </summary>
    public string? AeatSubmissionId { get; set; }

    /// <summary>
    /// Estado actual del registro en AEAT.
    /// Valores: "Pendiente", "Enviado", "Aceptado", "Rechazado", etc.
    /// </summary>
    public string Status { get; set; } = "Pendiente";

    /// <summary>
    /// Constructor privado para Entity Framework.
    /// </summary>
    private BillingRecord() { }

    /// <summary>
    /// Factory method para crear un nuevo registro de facturación.
    /// Valida que todos los datos sean consistentes.
    /// </summary>
    public static BillingRecord Create(
        InvoiceIdentifier invoiceIdentifier,
        string issuerName,
        string description,
        Money totalAmount,
        Money totalTaxAmount,
        string? previousRecordHash = null)
    {
        if (string.IsNullOrWhiteSpace(issuerName))
            throw new InvalidOperationException("El nombre del emisor es requerido");

        if (string.IsNullOrWhiteSpace(description))
            throw new InvalidOperationException("La descripción de la operación es requerida");

        if (totalTaxAmount.Amount > totalAmount.Amount)
            throw new InvalidOperationException("La cuota de impuesto no puede ser mayor que el importe total");

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
            PreviousRecordHash = previousRecordHash,
            Status = "Pendiente",
            IsSubmitted = false
        };
    }

    /// <summary>
    /// Marca el registro como enviado a AEAT.
    /// </summary>
    public void MarkAsSubmitted(string aeatSubmissionId)
    {
        if (string.IsNullOrWhiteSpace(aeatSubmissionId))
            throw new InvalidOperationException("El ID de envío de AEAT es requerido");

        IsSubmitted = true;
        AeatSubmissionId = aeatSubmissionId;
        Status = "Enviado";
    }

    /// <summary>
    /// Marca el registro como aceptado por AEAT.
    /// </summary>
    public void MarkAsAccepted()
    {
        Status = "Aceptado";
    }

    /// <summary>
    /// Marca el registro como rechazado por AEAT.
    /// </summary>
    public void MarkAsRejected(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Debe proporcionar un motivo de rechazo");

        Status = $"Rechazado: {reason}";
    }

    /// <summary>
    /// Establece el hash calculado para este registro.
    /// </summary>
    public void SetComputedHash(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
            throw new InvalidOperationException("El hash no puede estar vacío");

        ComputedHash = hash;
    }
}
