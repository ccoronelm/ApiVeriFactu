using gesFactu.Domain.Entities;

namespace gesFactu.Application.Common.Abstractions;

/// <summary>
/// Repositorio específico para BillingRecord.
/// Define las operaciones de persistencia sin exponer EF Core.
/// </summary>
public interface IBillingRecordRepository
{
    /// <summary>
    /// Agrega un nuevo registro de facturación.
    /// </summary>
    Task AddAsync(BillingRecord billingRecord, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene un registro por su ID.
    /// </summary>
    Task<BillingRecord?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene el registro anterior de una factura (para encadenamiento).
    /// </summary>
    Task<BillingRecord?> GetPreviousRecordAsync(
        string issuerNif,
        string invoiceSeries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista todos los registros de un contribuyente.
    /// </summary>
    Task<IEnumerable<BillingRecord>> ListByIssuerAsync(
        string issuerNif,
        int pageSize = 50,
        int pageNumber = 1,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Actualiza el estado de envío de un registro.
    /// </summary>
    Task UpdateSubmissionStatusAsync(
        int id,
        string aeatSubmissionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Actualiza el estado de un registro después de respuesta de AEAT.
    /// </summary>
    Task UpdateAeatStatusAsync(
        int id,
        string status,
        CancellationToken cancellationToken = default);
}
