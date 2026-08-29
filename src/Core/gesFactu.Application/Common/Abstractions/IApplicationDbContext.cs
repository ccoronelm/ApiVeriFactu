namespace gesFactu.Application.Common.Abstractions;

/// <summary>
/// Puerto que define la interfaz de persistencia.
/// Esta abstracción permite que Application orqueste casos de uso sin depender de EF Core.
/// La implementación estará en Infrastructure.
/// </summary>
public interface IApplicationDbContext
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    // DbSets se agregarán aquí según sea necesario
    // public DbSet<BillingRecord> BillingRecords { get; }
}
