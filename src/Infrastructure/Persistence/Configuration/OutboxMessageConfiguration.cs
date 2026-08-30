using gesFactu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace gesFactu.Infrastructure.Persistence.Configuration;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        builder.Property(e => e.CorrelationId)
            .IsRequired();

        builder.HasIndex(e => e.CorrelationId)
            .HasDatabaseName("IX_OutboxMessages_CorrelationId")
            .IsUnique();

        builder.Property(e => e.AggregateId).IsRequired();

        builder.HasIndex(e => e.AggregateId)
            .HasDatabaseName("IX_OutboxMessages_AggregateId");

        builder.Property(e => e.AggregateType)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(e => e.EventType)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(e => e.Payload)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(e => e.ProcessedAt);

        builder.Property(e => e.ProcessingAttempts)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(e => e.LastProcessingError)
            .HasColumnType("text");

        builder.Property(e => e.IsProcessed)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.NextAttemptAt);

        builder.Property(e => e.LockedBy)
            .HasMaxLength(100);

        builder.Property(e => e.LockedUntil);

        builder.HasIndex(e => e.IsProcessed)
            .HasDatabaseName("IX_OutboxMessages_IsProcessed");

        builder.HasIndex(e => new
            {
                e.IsProcessed,
                e.NextAttemptAt,
                e.LockedUntil,
                e.CreatedAt
            })
            .HasDatabaseName("IX_OutboxMessages_Claim");

        // Un RegistroAlta solo puede generar una remisión de este tipo.
        builder.HasIndex(e => new { e.AggregateType, e.AggregateId, e.EventType })
            .IsUnique()
            .HasDatabaseName("UX_OutboxMessages_AggregateEvent");

        builder.ToTable("OutboxMessages");
    }
}
