using gesFactu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace gesFactu.Infrastructure.Persistence.Configuration;

public sealed class BillingRecordConfiguration : IEntityTypeConfiguration<BillingRecord>
{
    public void Configure(EntityTypeBuilder<BillingRecord> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        builder.Property(e => e.CreateDate)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.CreatedBy).HasMaxLength(256);
        builder.Property(e => e.LastModifiedDate);
        builder.Property(e => e.LastModifiedBy).HasMaxLength(256);
        builder.Property(e => e.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);
        builder.Property(e => e.DeletedAt);
        builder.Property(e => e.DeletedBy).HasMaxLength(256);

        builder.HasQueryFilter(e => !e.IsDeleted);

        builder.Property(e => e.IssuerName)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(e => e.RecipientNif)
            .IsRequired()
            .HasMaxLength(9);

        builder.Property(e => e.RecipientName)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(e => e.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.RegisterTimestamp)
            .IsRequired()
            .HasMaxLength(25);

        builder.Property(e => e.RecordType)
            .IsRequired()
            .HasMaxLength(16)
            .HasDefaultValue(BillingRecord.AltaRecordType);

        builder.Property(e => e.InvoiceType)
            .IsRequired()
            .HasMaxLength(2)
            .HasDefaultValue("F1");

        builder.Property(e => e.SubsanatesBillingRecordId);
        builder.Property(e => e.CancelsBillingRecordId);
        builder.Property(e => e.RectifiesBillingRecordId);

        builder.Property(e => e.RectificationType)
            .HasMaxLength(1);

        builder.Property(e => e.RectifiedBaseAmount)
            .HasPrecision(18, 2);

        builder.Property(e => e.RectifiedTaxAmount)
            .HasPrecision(18, 2);

        builder.Property(e => e.RectifiedSurchargeAmount)
            .HasPrecision(18, 2);

        builder.Property(e => e.PreviousBillingRecordId);

        builder.Property(e => e.PreviousRecordHash)
            .HasMaxLength(64);

        builder.Property(e => e.ComputedHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(e => e.IsSubmitted)
            .HasDefaultValue(false);

        builder.Property(e => e.SubmissionCorrelationId);

        builder.Property(e => e.AeatSubmissionId)
            .HasMaxLength(100);

        builder.Property(e => e.Status)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("Pendiente");

        builder.Ignore(e => e.InvoiceIdentifier);

        builder.Property(e => e.IssuerNif)
            .IsRequired()
            .HasMaxLength(9);

        builder.Property(e => e.InvoiceSeries)
            .IsRequired()
            .HasMaxLength(60);

        builder.Property(e => e.InvoiceNumber)
            .IsRequired()
            .HasMaxLength(60);

        builder.Property(e => e.FiscalInvoiceNumber)
            .IsRequired()
            .HasMaxLength(60);

        builder.Property(e => e.IssueDate)
            .IsRequired();

        builder.Property(e => e.TotalAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(e => e.TotalTaxAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        // La identidad fiscal es única para el alta inicial. Las subsanaciones
        // reutilizan esa misma clave AEAT y quedan fuera del índice único.
        builder.HasIndex(e => new
            {
                e.IssuerNif,
                e.FiscalInvoiceNumber,
                e.IssueDate
            })
            .IsUnique()
            .HasFilter("\"RecordType\" = 'Alta' AND \"SubsanatesBillingRecordId\" IS NULL")
            .HasDatabaseName("UX_BillingRecords_FiscalIdentity");

        builder.HasIndex(e => e.SubsanatesBillingRecordId)
            .HasDatabaseName("IX_BillingRecords_SubsanatesBillingRecordId");

        builder.HasIndex(e => e.CancelsBillingRecordId)
            .HasDatabaseName("IX_BillingRecords_CancelsBillingRecordId");

        builder.HasIndex(e => e.RectifiesBillingRecordId)
            .HasDatabaseName("IX_BillingRecords_RectifiesBillingRecordId");

        // Soporta la lectura del último RF generado por obligado tributario
        // dentro de una transacción SERIALIZABLE.
        builder.HasIndex(e => new { e.IssuerNif, e.Id })
            .HasDatabaseName("IX_BillingRecords_Issuer_GenerationOrder");

        builder.HasIndex(e => e.PreviousBillingRecordId)
            .HasDatabaseName("IX_BillingRecords_PreviousBillingRecordId");

        builder.ToTable("BillingRecords");
    }
}
