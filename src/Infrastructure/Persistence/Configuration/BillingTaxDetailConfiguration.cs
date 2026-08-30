using gesFactu.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace gesFactu.Infrastructure.Persistence.Configuration;

public sealed class BillingTaxDetailConfiguration
    : IEntityTypeConfiguration<BillingTaxDetail>
{
    public void Configure(EntityTypeBuilder<BillingTaxDetail> builder)
    {
        builder.ToTable("BillingTaxDetails");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.CreateDate)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.LastModifiedDate);
        builder.Property(x => x.LastModifiedBy).HasMaxLength(256);
        builder.Property(x => x.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);
        builder.Property(x => x.DeletedAt);
        builder.Property(x => x.DeletedBy).HasMaxLength(256);

        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.Property(x => x.TaxCode).HasMaxLength(2);
        builder.Property(x => x.RegimeCode).HasMaxLength(2);
        builder.Property(x => x.OperationQualification).HasMaxLength(2);
        builder.Property(x => x.ExemptionCause).HasMaxLength(2);

        builder.Property(x => x.TaxRate).HasPrecision(5, 2);
        builder.Property(x => x.TaxBase).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.TaxAmount).HasPrecision(18, 2);
        builder.Property(x => x.EquivalenceSurchargeRate).HasPrecision(5, 2);
        builder.Property(x => x.EquivalenceSurchargeAmount).HasPrecision(18, 2);

        builder.HasOne(x => x.BillingRecord)
            .WithMany(x => x.TaxDetails)
            .HasForeignKey(x => x.BillingRecordId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.BillingRecordId, x.Id })
            .HasDatabaseName("IX_BillingTaxDetails_Record_Order");
    }
}
