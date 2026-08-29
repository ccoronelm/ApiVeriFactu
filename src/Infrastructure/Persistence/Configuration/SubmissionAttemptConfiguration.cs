using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using gesFactu.Domain.Entities;

namespace gesFactu.Infrastructure.Persistence.Configuration;

/// <summary>
/// Configuración de EF Core para SubmissionAttempt.
/// </summary>
public class SubmissionAttemptConfiguration : IEntityTypeConfiguration<SubmissionAttempt>
{
    public void Configure(EntityTypeBuilder<SubmissionAttempt> builder)
    {
        builder.ToTable("SubmissionAttempts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.BillingRecordId)
            .IsRequired();

        builder.Property(x => x.AttemptNumber)
            .IsRequired();

        builder.Property(x => x.RequestPayload)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.SubmittedAt)
            .IsRequired();

        builder.Property(x => x.ResponseCode)
            .HasMaxLength(50);

        builder.Property(x => x.ResponseDescription)
            .HasMaxLength(1000);

        builder.Property(x => x.ResponsePayload)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.AeatSubmissionId)
            .HasMaxLength(100);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(x => x.RespondedAt);

        builder.Property(x => x.DurationMilliseconds);

        builder.Property(x => x.Notes)
            .HasMaxLength(1000);

        // Relación con BillingRecord
        builder.HasOne(x => x.BillingRecord)
            .WithMany(br => br.SubmissionAttempts)
            .HasForeignKey(x => x.BillingRecordId)
            .OnDelete(DeleteBehavior.Cascade);

        // Índices para queries frecuentes
        builder.HasIndex(x => x.BillingRecordId)
            .HasDatabaseName("IX_SubmissionAttempts_BillingRecordId");

        builder.HasIndex(x => new { x.BillingRecordId, x.AttemptNumber })
            .HasDatabaseName("IX_SubmissionAttempts_BillingRecordAndAttempt");

        builder.HasIndex(x => new { x.Status, x.SubmittedAt })
            .HasDatabaseName("IX_SubmissionAttempts_StatusAndTime");
    }
}
