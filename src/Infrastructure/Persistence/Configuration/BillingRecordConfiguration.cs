using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using gesFactu.Domain.Entities;

namespace gesFactu.Infrastructure.Persistence.Configuration;

/// <summary>
/// Configuración EF Core para BillingRecord.
/// 
/// Estrategia: Por simplicidad en el MVP, permitimos que EF Core cree la estructura automáticamente
/// para las propiedades escalares, e ignoramos los value objects complejos (InvoiceIdentifier, Money)
/// que requieren conversiones personalizadas sofisticadas.
/// 
/// Estos value objects se reconstruirán en el repositorio durante la lectura.
/// </summary>
public sealed class BillingRecordConfiguration : IEntityTypeConfiguration<BillingRecord>
{
    public void Configure(EntityTypeBuilder<BillingRecord> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        // Auditoría
        builder.Property(e => e.CreateDate)
            .HasDefaultValueSql("GETUTCDATE()")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.CreatedBy)
            .HasMaxLength(256);

        builder.Property(e => e.LastModifiedDate);
        builder.Property(e => e.LastModifiedBy).HasMaxLength(256);

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
            .IsRequired()
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

        // Ignoramos InvoiceIdentifier y Money para esta migración inicial.
        // EF Core no puede mapearlos automáticamente sin conversiones personalizadas complejas.
        builder.Ignore(e => e.InvoiceIdentifier);
        builder.Ignore(e => e.TotalAmount);
        builder.Ignore(e => e.TotalTaxAmount);

        // Mapeamos las propiedades desnormalizadas de InvoiceIdentifier
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

        // Mapeamos los importes desnormalizados
        builder.Property(e => e.TotalAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(e => e.TotalTaxAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.ToTable("BillingRecords");
    }
}
