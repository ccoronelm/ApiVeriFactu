using gesFactu.Domain.Entities;

namespace gesFactu.Application.Common.Abstractions;

/// <summary>
/// Repositorio específico para BillingRecord.
/// </summary>
public interface IBillingRecordRepository
{
    Task AddAsync(
        BillingRecord billingRecord,
        CancellationToken cancellationToken = default);

    Task<BillingRecord?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene el último registro de facturación generado para el obligado tributario
    /// en este SIF, independientemente de la serie y de si ya fue remitido a AEAT.
    /// La llamada debe realizarse dentro de la transacción SERIALIZABLE usada para
    /// crear el siguiente registro.
    /// </summary>
    Task<BillingRecord?> GetLastGeneratedRecordAsync(
        string issuerNif,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca una factura por su identidad fiscal AEAT:
    /// emisor + NumSerieFactura + fecha de expedición.
    /// </summary>
    Task<BillingRecord?> GetByFiscalIdentityAsync(
        string issuerNif,
        string fiscalInvoiceNumber,
        DateOnly issueDate,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<BillingRecord>> ListByIssuerAsync(
        string issuerNif,
        int pageSize = 50,
        int pageNumber = 1,
        CancellationToken cancellationToken = default);

    Task UpdateSubmissionStatusAsync(
        int id,
        string aeatSubmissionId,
        CancellationToken cancellationToken = default);

    Task UpdateAeatStatusAsync(
        int id,
        string status,
        CancellationToken cancellationToken = default);
}
