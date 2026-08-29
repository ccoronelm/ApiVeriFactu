using Microsoft.EntityFrameworkCore;
using gesFactu.Application.Common.Abstractions;

namespace gesFactu.Infrastructure.Persistence;

/// <summary>
/// DbContext principal de la aplicación.
/// Implementa el puerto IApplicationDbContext definido en Application.
/// </summary>
public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // DbSets se agregarán aquí según los agregados del dominio
    // public DbSet<BillingRecord> BillingRecords { get; set; } = null!;

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configuraciones de entidades irán aquí
        // modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
