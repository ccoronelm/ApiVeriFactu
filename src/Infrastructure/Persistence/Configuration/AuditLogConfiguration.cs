using gesFactu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace gesFactu.Infrastructure.Persistence.Configuration;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.EntityName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.EntityId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Action)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(x => x.Actor)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.CorrelationId)
            .HasMaxLength(128);

        builder.Property(x => x.OccurredAtUtc)
            .IsRequired();

        builder.Property(x => x.OldValues)
            .HasColumnType("jsonb");

        builder.Property(x => x.NewValues)
            .HasColumnType("jsonb");

        builder.HasIndex(x => new { x.EntityName, x.EntityId, x.OccurredAtUtc })
            .HasDatabaseName("IX_AuditLogs_Entity_Time");

        builder.HasIndex(x => x.CorrelationId)
            .HasDatabaseName("IX_AuditLogs_CorrelationId");
    }
}
