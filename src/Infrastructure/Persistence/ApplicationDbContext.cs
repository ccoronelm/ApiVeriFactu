using System.Data;
using System.Text.Json;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Domain.Common;
using gesFactu.Domain.Entities;
using gesFactu.Infrastructure.Persistence.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;

namespace gesFactu.Infrastructure.Persistence;

/// <summary>
/// DbContext principal. Implementa auditoría automática, protección de
/// inmutabilidad fiscal y borrado lógico de entidades no fiscales.
/// </summary>
public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    private static readonly HashSet<string> MutableBillingRecordProperties =
        new(StringComparer.Ordinal)
        {
            nameof(BillingRecord.IsSubmitted),
            nameof(BillingRecord.SubmissionCorrelationId),
            nameof(BillingRecord.AeatSubmissionId),
            nameof(BillingRecord.Status),
            nameof(BaseDomainModel.LastModifiedDate),
            nameof(BaseDomainModel.LastModifiedBy)
        };

    private readonly IAuditContext _auditContext;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        IAuditContext? auditContext = null)
        : base(options)
    {
        _auditContext = auditContext ?? new SystemAuditContext();
    }

    public DbSet<BillingRecord> BillingRecords { get; set; } = null!;
    public DbSet<BillingTaxDetail> BillingTaxDetails { get; set; } = null!;
    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;
    public DbSet<DeadLetterMessage> DeadLetterMessages { get; set; } = null!;
    public DbSet<SubmissionAttempt> SubmissionAttempts { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;
    public DbSet<IdempotencyRecord> IdempotencyRecords { get; set; } = null!;

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

    public async Task<IApplicationTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default)
    {
        var transaction = await Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        return new EfApplicationTransaction(transaction);
    }

    public async Task AcquireExclusiveLockAsync(
        string resourceKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceKey);

        if (Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "El bloqueo de cadena fiscal requiere una transacción activa.");
        }

        var resource = $"gesFactu:{resourceKey.Trim().ToUpperInvariant()}";

        await Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({resource}, 0));",
            cancellationToken);
    }

    public override async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        ChangeTracker.DetectChanges();

        var now = DateTime.UtcNow;
        PrepareTrackedEntities(now);
        ValidateFiscalImmutability();
        ValidateAuditLogAppendOnly();

        var auditEntries = CaptureAuditEntries(now);

        var affected = await base.SaveChangesAsync(cancellationToken);

        if (auditEntries.Count == 0)
            return affected;

        foreach (var pending in auditEntries)
            AuditLogs.Add(pending.ToAuditLog());

        affected += await base.SaveChangesAsync(cancellationToken);
        return affected;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new BillingRecordConfiguration());
        modelBuilder.ApplyConfiguration(new BillingTaxDetailConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new DeadLetterMessageConfiguration());
        modelBuilder.ApplyConfiguration(new SubmissionAttemptConfiguration());
        modelBuilder.ApplyConfiguration(new AuditLogConfiguration());
        modelBuilder.ApplyConfiguration(new IdempotencyRecordConfiguration());
    }

    private void PrepareTrackedEntities(DateTime now)
    {
        foreach (var entry in ChangeTracker.Entries<BaseDomainModel>().ToArray())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreateDate ??= now;
                entry.Entity.CreatedBy =
                    NormalizeActor(entry.Entity.CreatedBy) ?? CurrentActor();
                entry.Entity.IsDeleted = false;
                continue;
            }

            if (entry.State == EntityState.Deleted)
            {
                if (entry.Entity is BillingRecord or BillingTaxDetail)
                {
                    throw new InvalidOperationException(
                        "Los registros fiscales y sus desgloses son inmutables y no pueden borrarse. " +
                        "Use subsanación, rectificación o RegistroAnulacion.");
                }

                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedAt = now;
                entry.Entity.DeletedBy = CurrentActor();
                entry.Entity.LastModifiedDate = now;
                entry.Entity.LastModifiedBy = CurrentActor();
                continue;
            }

            if (entry.State == EntityState.Modified &&
                HasActualChanges(entry))
            {
                entry.Entity.LastModifiedDate = now;
                entry.Entity.LastModifiedBy = CurrentActor();
            }
        }
    }

    private void ValidateFiscalImmutability()
    {
        foreach (var entry in ChangeTracker.Entries<BillingRecord>())
        {
            if (entry.State != EntityState.Modified)
                continue;

            var illegal = entry.Properties
                .Where(p =>
                    p.IsModified &&
                    !Equals(p.OriginalValue, p.CurrentValue) &&
                    !MutableBillingRecordProperties.Contains(p.Metadata.Name))
                .Select(p => p.Metadata.Name)
                .ToArray();

            if (illegal.Length > 0)
            {
                throw new InvalidOperationException(
                    "Un BillingRecord persistido es fiscalmente inmutable. " +
                    "Campos modificados no permitidos: " +
                    string.Join(", ", illegal) +
                    ". Use subsanación, rectificación o anulación.");
            }
        }

        foreach (var entry in ChangeTracker.Entries<BillingTaxDetail>())
        {
            if (entry.State != EntityState.Modified)
                continue;

            var illegal = entry.Properties
                .Where(p =>
                    p.IsModified &&
                    !Equals(p.OriginalValue, p.CurrentValue) &&
                    p.Metadata.Name is not nameof(BaseDomainModel.LastModifiedDate)
                        and not nameof(BaseDomainModel.LastModifiedBy))
                .Select(p => p.Metadata.Name)
                .ToArray();

            if (illegal.Length > 0)
            {
                throw new InvalidOperationException(
                    "El desglose fiscal persistido es inmutable. " +
                    "Cree una subsanación o rectificación en lugar de modificarlo.");
            }
        }
    }

    private void ValidateAuditLogAppendOnly()
    {
        if (ChangeTracker.Entries<AuditLog>().Any(x =>
                x.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException(
                "AuditLog es append-only y no admite modificación ni borrado.");
        }
    }

    private List<PendingAuditEntry> CaptureAuditEntries(DateTime now)
    {
        var pending = new List<PendingAuditEntry>();

        foreach (var entry in ChangeTracker.Entries<BaseDomainModel>())
        {
            if (entry.State == EntityState.Added)
            {
                pending.Add(new PendingAuditEntry(
                    entry,
                    "Created",
                    CurrentActor(),
                    _auditContext.CorrelationId,
                    now,
                    OldValues: null,
                    NewValues: null));
                continue;
            }

            if (entry.State != EntityState.Modified)
                continue;

            var changed = entry.Properties
                .Where(p =>
                    p.IsModified &&
                    !Equals(p.OriginalValue, p.CurrentValue))
                .ToArray();

            if (changed.Length == 0)
                continue;

            var oldValues = changed.ToDictionary(
                p => p.Metadata.Name,
                p => NormalizeValue(p.OriginalValue));

            var newValues = changed.ToDictionary(
                p => p.Metadata.Name,
                p => NormalizeValue(p.CurrentValue));

            var action =
                entry.Entity.IsDeleted &&
                oldValues.TryGetValue(
                    nameof(BaseDomainModel.IsDeleted),
                    out var wasDeleted) &&
                wasDeleted is false
                    ? "SoftDeleted"
                    : "Updated";

            pending.Add(new PendingAuditEntry(
                entry,
                action,
                CurrentActor(),
                _auditContext.CorrelationId,
                now,
                oldValues,
                newValues));
        }

        return pending;
    }

    private string CurrentActor()
        => NormalizeActor(_auditContext.Actor) ?? "system";

    private static string? NormalizeActor(string? actor)
    {
        if (string.IsNullOrWhiteSpace(actor))
            return null;

        var value = actor.Trim();
        return value.Length <= 256 ? value : value[..256];
    }

    private static bool HasActualChanges(EntityEntry<BaseDomainModel> entry)
        => entry.Properties.Any(p =>
            p.IsModified &&
            !Equals(p.OriginalValue, p.CurrentValue));

    private static object? NormalizeValue(object? value)
        => value switch
        {
            DateOnly date => date.ToString("yyyy-MM-dd"),
            DateTime dateTime => dateTime.ToUniversalTime().ToString("O"),
            DateTimeOffset offset => offset.ToUniversalTime().ToString("O"),
            _ => value
        };

    private sealed record PendingAuditEntry(
        EntityEntry Entry,
        string Action,
        string Actor,
        string? CorrelationId,
        DateTime OccurredAtUtc,
        IReadOnlyDictionary<string, object?>? OldValues,
        IReadOnlyDictionary<string, object?>? NewValues)
    {
        public AuditLog ToAuditLog()
        {
            var primaryKey = Entry.Metadata.FindPrimaryKey();
            var entityId = primaryKey is null
                ? "(sin clave)"
                : string.Join(
                    "|",
                    primaryKey.Properties.Select(p =>
                        Entry.Property(p.Name).CurrentValue?.ToString() ?? "(null)"));

            IReadOnlyDictionary<string, object?>? newValues = NewValues;

            if (Action == "Created")
            {
                newValues = Entry.Properties.ToDictionary(
                    p => p.Metadata.Name,
                    p => NormalizeValue(p.CurrentValue));
            }

            return new AuditLog
            {
                EntityName = Entry.Metadata.ClrType.Name,
                EntityId = entityId,
                Action = Action,
                Actor = Actor,
                CorrelationId = string.IsNullOrWhiteSpace(CorrelationId)
                    ? null
                    : CorrelationId.Length <= 128
                        ? CorrelationId
                        : CorrelationId[..128],
                OccurredAtUtc = OccurredAtUtc,
                OldValues = OldValues is null
                    ? null
                    : JsonSerializer.Serialize(OldValues),
                NewValues = newValues is null
                    ? null
                    : JsonSerializer.Serialize(newValues)
            };
        }
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
