using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using gesFactu.Domain.Entities;

namespace gesFactu.Infrastructure.Persistence.Configuration;

/// <summary>
/// Configuración de EF Core para DeadLetterMessage.
/// </summary>
public class DeadLetterMessageConfiguration : IEntityTypeConfiguration<DeadLetterMessage>
{
    public void Configure(EntityTypeBuilder<DeadLetterMessage> builder)
    {
        builder.ToTable("DeadLetterMessages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.OriginalMessageId)
            .IsRequired();

        builder.Property(x => x.CorrelationId)
            .IsRequired()
            .HasMaxLength(36);

        builder.Property(x => x.Payload)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.FailureReason)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.LastErrorResponse)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.ProcessingAttempts)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.MovedToDlqAt)
            .IsRequired();

        builder.Property(x => x.IsReviewed)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.ReviewNotes)
            .HasMaxLength(2000);

        // Índices para queries frecuentes
        builder.HasIndex(x => x.CorrelationId)
            .IsUnique()
            .HasDatabaseName("IX_DeadLetterMessages_CorrelationId");

        builder.HasIndex(x => new { x.IsReviewed, x.MovedToDlqAt })
            .HasDatabaseName("IX_DeadLetterMessages_UnreviewedByDate");
    }
}
