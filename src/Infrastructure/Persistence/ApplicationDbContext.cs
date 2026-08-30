using System.Data;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Domain.Entities;
using gesFactu.Infrastructure.Persistence.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace gesFactu.Infrastructure.Persistence;

/// <summary>
/// DbContext principal de la aplicación.
/// </summary>
public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<BillingRecord> BillingRecords { get; set; } = null!;
    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;
    public DbSet<DeadLetterMessage> DeadLetterMessages { get; set; } = null!;
    public DbSet<SubmissionAttempt> SubmissionAttempts { get; set; } = null!;

    public void AddOutboxMessage(object message)
    {
        if (message is OutboxMessage outboxMessage)
        {
            OutboxMessages.Add(outboxMessage);
            return;
        }

        throw new ArgumentException(
            $"Tipo de mensaje no soportado: {message.GetType().Name}",
            nameof(message));
    }

    public async Task<IApplicationTransaction> BeginSerializableTransactionAsync(
        CancellationToken cancellationToken = default)
    {
        var transaction = await Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        return new EfApplicationTransaction(transaction);
    }

    public async Task AcquireFiscalChainLockAsync(
        string chainKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chainKey);

        if (Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "El bloqueo de cadena fiscal requiere una transacción activa.");
        }

        var resource = $"gesFactu:VeriFactuChain:{chainKey.Trim().ToUpperInvariant()}";

        await Database.ExecuteSqlInterpolatedAsync(
            $"""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock
                @Resource = {resource},
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = 15000;
            IF @result < 0
                THROW 51000, 'No se pudo obtener el bloqueo de la cadena fiscal.', 1;
            """,
            cancellationToken);
    }

    public override Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
        => base.SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new BillingRecordConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new DeadLetterMessageConfiguration());
        modelBuilder.ApplyConfiguration(new SubmissionAttemptConfiguration());
    }

    private sealed class EfApplicationTransaction : IApplicationTransaction
    {
        private readonly IDbContextTransaction _transaction;

        public EfApplicationTransaction(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
            => _transaction.CommitAsync(cancellationToken);

        public Task RollbackAsync(CancellationToken cancellationToken = default)
            => _transaction.RollbackAsync(cancellationToken);

        public ValueTask DisposeAsync()
            => _transaction.DisposeAsync();
    }
}
