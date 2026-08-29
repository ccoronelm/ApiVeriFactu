using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using gesFactu.Domain.Entities;

namespace gesFactu.Infrastructure.Persistence.Configuration;

/// <summary>
/// Configuración EF Core para OutboxMessage.
/// 
/// El outbox es una tabla auxiliar para garantizar entrega confiable.
/// Índices estratégicos en IsProcessed y ProcessingAttempts para queries eficientes.
/// </summary>
public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        // CorrelationId: Único para detectar duplicados
        builder.Property(e => e.CorrelationId)
            .IsRequired()
            .HasMaxLength(36);

        builder.HasIndex(e => e.CorrelationId)
            .HasDatabaseName("IX_OutboxMessages_CorrelationId")
            .IsUnique();

        // AggregateId: Relación con BillingRecord (sin FK para flexibilidad)
        builder.Property(e => e.AggregateId).IsRequired();

        builder.HasIndex(e => e.AggregateId)
            .HasDatabaseName("IX_OutboxMessages_AggregateId");

        // AggregateType: Tipo de entidad
        builder.Property(e => e.AggregateType)
            .IsRequired()
            .HasMaxLength(255);

        // EventType: Tipo de evento
        builder.Property(e => e.EventType)
            .IsRequired()
            .HasMaxLength(255);

        // Payload: JSON serializado
        builder.Property(e => e.Payload)
            .IsRequired()
            .HasColumnType("NVARCHAR(MAX)");

        // Timestamps
        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(e => e.ProcessedAt);

        // Reintentos y errores
        builder.Property(e => e.ProcessingAttempts)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(e => e.LastProcessingError)
            .HasColumnType("NVARCHAR(MAX)");

        // Bandera de procesamiento
        builder.Property(e => e.IsProcessed)
            .IsRequired()
            .HasDefaultValue(false);

        // Índices para queries frecuentes del procesador
        builder.HasIndex(e => e.IsProcessed)
            .HasDatabaseName("IX_OutboxMessages_IsProcessed");

        builder.HasIndex(e => new { e.IsProcessed, e.ProcessingAttempts })
            .HasDatabaseName("IX_OutboxMessages_IsProcessed_Attempts");

        builder.ToTable("OutboxMessages");
    }
}
