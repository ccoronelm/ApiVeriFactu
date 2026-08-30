using gesFactu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace gesFactu.Infrastructure.Persistence.Configuration;

public sealed class IdempotencyRecordConfiguration
    : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyRecords");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Key)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Method)
            .IsRequired()
            .HasMaxLength(16);

        builder.Property(x => x.Path)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.RequestHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(16);

        builder.Property(x => x.ResponseContentType)
            .HasMaxLength(200);

        builder.Property(x => x.ResponseBody)
            .HasColumnType("text");

        builder.Property(x => x.ResponseLocation)
            .HasMaxLength(1000);

        builder.HasIndex(x => new { x.Key, x.Method, x.Path })
            .IsUnique()
            .HasDatabaseName("UX_IdempotencyRecords_Key_Method_Path");

        builder.HasIndex(x => x.ExpiresAtUtc)
            .HasDatabaseName("IX_IdempotencyRecords_ExpiresAt");
    }
}
