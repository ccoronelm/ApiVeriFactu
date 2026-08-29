using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Domain.Entities;
using gesFactu.Infrastructure.Persistence.Configuration;

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

    public DbSet<BillingRecord> BillingRecords { get; set; } = null!;
    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;

    public void AddOutboxMessage(object message)
    {
        if (message is OutboxMessage outboxMessage)
        {
            OutboxMessages.Add(outboxMessage);
        }
        else
        {
            throw new ArgumentException($"Tipo de mensaje no soportado: {message.GetType().Name}", nameof(message));
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Aplicar configuraciones de entidades desde este assembly
        modelBuilder.ApplyConfiguration(new BillingRecordConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
    }
}