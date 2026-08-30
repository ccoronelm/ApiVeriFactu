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

        // Identidad fiscal del registro de alta que se remite a AEAT.
        builder.HasIndex(e => new
            {
                e.IssuerNif,
                e.FiscalInvoiceNumber,
                e.IssueDate
            })
            .IsUnique()
            .HasDatabaseName("UX_BillingRecords_FiscalIdentity");

        // Soporta la lectura del último RF generado por obligado tributario
        // dentro de una transacción SERIALIZABLE.
        builder.HasIndex(e => new { e.IssuerNif, e.Id })
            .HasDatabaseName("IX_BillingRecords_Issuer_GenerationOrder");

        builder.HasIndex(e => e.PreviousBillingRecordId)
            .HasDatabaseName("IX_BillingRecords_PreviousBillingRecordId");

        builder.ToTable("BillingRecords");
    }
}
