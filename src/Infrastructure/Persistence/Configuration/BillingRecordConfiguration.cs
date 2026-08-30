using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using gesFactu.Domain.Entities;

namespace gesFactu.Infrastructure.Persistence.Configuration;

public sealed class BillingRecordConfiguration : IEntityTypeConfiguration<BillingRecord>
{
    public void Configure(EntityTypeBuilder<BillingRecord> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        builder.Property(e => e.CreateDate)
            .HasDefaultValueSql("GETUTCDATE()")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.CreatedBy)
            .HasMaxLength(256);

        builder.Property(e => e.LastModifiedDate);
        builder.Property(e => e.LastModifiedBy).HasMaxLength(256);

        builder.Property(e => e.IssuerName)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(e => e.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.RegisterTimestamp)
            .IsRequired()
            .HasMaxLength(25);

        builder.Property(e => e.PreviousRecordHash)
            .HasMaxLength(64);

        builder.Property(e => e.ComputedHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(e => e.IsSubmitted)
            .HasDefaultValue(false);

        builder.Property(e => e.AeatSubmissionId)
            .HasMaxLength(50);

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

        builder.Property(e => e.IssueDate)
            .IsRequired();

        builder.Property(e => e.TotalAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(e => e.TotalTaxAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.ToTable("BillingRecords");
    }
}
