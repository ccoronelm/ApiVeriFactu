namespace gesFactu.Application.Common.Abstractions;
/// <summary>
/// Puerto que define la interfaz de persistencia.
/// Esta abstracción permite que Application orqueste casos de uso sin depender de EF Core.
/// La implementación estará en Infrastructure.
/// 
/// Nota: Se proporciona un método para agregar mensajes de outbox de forma atómica.
/// </summary>
public interface IApplicationDbContext
{
    /// <summary>
    /// Agrega un mensaje de outbox a la transacción actual.
    /// Se persiste junto con otros cambios en SaveChangesAsync.
    /// </summary>
    void AddOutboxMessage(object message);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    // DbSets se agregarán aquí según sea necesario
    // public DbSet<BillingRecord> BillingRecords { get; }
}
