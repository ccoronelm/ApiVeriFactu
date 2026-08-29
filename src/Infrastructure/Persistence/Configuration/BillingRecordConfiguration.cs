using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using gesFactu.Domain.Entities;

namespace gesFactu.Infrastructure.Persistence.Configuration;

/// <summary>
/// Configuración EF Core para la entidad BillingRecord.
/// Mapea los Value Objects a columnas de base de datos.
/// </summary>
public sealed class BillingRecordConfiguration : IEntityTypeConfiguration<BillingRecord>
{
    public void Configure(EntityTypeBuilder<BillingRecord> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();

        // Auditoría
        builder.Property(e => e.CreateDate)
            .HasDefaultValueSql("GETUTCDATE()")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.CreatedBy)
            .HasMaxLength(256);

        builder.Property(e => e.LastModifiedDate);

        builder.Property(e => e.LastModifiedBy)
            .HasMaxLength(256);

        // InvoiceIdentifier (Value Object compuesto)
        // Descomponemos en columnas individuales
        builder.Property("InvoiceIdentifier.IssuerNif.Value")
            .HasColumnName("IssuerNif")
            .IsRequired()
            .HasMaxLength(9);

        builder.Property("InvoiceIdentifier.Series.Value")
            .HasColumnName("InvoiceSeries")
            .IsRequired()
            .HasMaxLength(60);

        builder.Property("InvoiceIdentifier.Number.Value")
            .HasColumnName("InvoiceNumber")
            .IsRequired()
            .HasMaxLength(60);

        builder.Property("InvoiceIdentifier.IssueDate")
            .HasColumnName("IssueDate")
            .IsRequired();

        // Money Value Objects
        builder.Property("TotalAmount.Amount")
            .HasColumnName("TotalAmount")
            .IsRequired()
            .HasPrecision(18, 2); // Money: 18 dígitos, 2 decimales

        builder.Property("TotalTaxAmount.Amount")
            .HasColumnName("TotalTaxAmount")
            .IsRequired()
            .HasPrecision(18, 2);

        // Datos básicos
        builder.Property(e => e.IssuerName)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(e => e.Description)
            .IsRequired()
            .HasMaxLength(500);

        // Hash y encadenamiento
        builder.Property(e => e.PreviousRecordHash)
            .HasMaxLength(64);

        builder.Property(e => e.ComputedHash)
            .HasMaxLength(64);

        // Envío a AEAT
        builder.Property(e => e.IsSubmitted)
            .HasDefaultValue(false);

        builder.Property(e => e.AeatSubmissionId)
            .HasMaxLength(50);

        builder.Property(e => e.Status)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("Pendiente");

        // Índices para queries frecuentes
        builder.HasIndex("IssuerNif")
            .HasDatabaseName("IX_BillingRecords_IssuerNif");

        builder.HasIndex("IssuerNif", "InvoiceSeries")
            .HasDatabaseName("IX_BillingRecords_IssuerNif_Series");

        builder.HasIndex("AeatSubmissionId")
            .HasDatabaseName("IX_BillingRecords_AeatSubmissionId")
            .IsUnique(true);

        builder.HasIndex("ComputedHash")
            .HasDatabaseName("IX_BillingRecords_Hash")
            .IsUnique(true);

        builder.HasIndex("Status")
            .HasDatabaseName("IX_BillingRecords_Status");

        builder.ToTable("BillingRecords");
    }
}
