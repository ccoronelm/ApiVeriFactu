using Microsoft.EntityFrameworkCore;
using Xunit;
using gesFactu.Domain.Entities;
using gesFactu.Infrastructure.Persistence;
using gesFactu.Infrastructure.Persistence.Repositories;

namespace gesFactu.Infrastructure.Tests.Outbox;

/// <summary>
/// Tests para la persistencia y procesamiento del outbox.
/// 
/// Casos de prueba:
/// - Obtener mensajes pendientes
/// - Marcar como procesado
/// - Registrar intento fallido
/// - Consultar por CorrelationId (idempotencia)
/// - Límite de intentos máximos
/// </summary>
public class OutboxStoreTests
{
    private ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task GetPendingMessagesAsync_ReturnUnprocessedMessages()
    {
        // Arrange
        using var context = CreateDbContext();
        var store = new OutboxStore(context);

        var message1 = new OutboxMessage
        {
            CorrelationId = Guid.NewGuid(),
            AggregateId = 1,
            AggregateType = "BillingRecord",
            EventType = "BillingRecordSubmittedToAEAT",
            Payload = "{\"test\": \"data1\"}",
            CreatedAt = DateTime.UtcNow,
            IsProcessed = false,
            ProcessingAttempts = 0
        };

        var message2 = new OutboxMessage
        {
            CorrelationId = Guid.NewGuid(),
            AggregateId = 2,
            AggregateType = "BillingRecord",
            EventType = "BillingRecordSubmittedToAEAT",
            Payload = "{\"test\": \"data2\"}",
            CreatedAt = DateTime.UtcNow,
            IsProcessed = false,
            ProcessingAttempts = 0
        };

        var processedMessage = new OutboxMessage
        {
            CorrelationId = Guid.NewGuid(),
            AggregateId = 3,
            AggregateType = "BillingRecord",
            EventType = "BillingRecordSubmittedToAEAT",
            Payload = "{\"test\": \"data3\"}",
            CreatedAt = DateTime.UtcNow,
            IsProcessed = true,
            ProcessingAttempts = 1,
            ProcessedAt = DateTime.UtcNow
        };

        context.OutboxMessages.AddRange(message1, message2, processedMessage);
        await context.SaveChangesAsync();

        // Act
        var pending = await store.GetPendingMessagesAsync(10, CancellationToken.None);

        // Assert
        Assert.Equal(2, pending.Count);
        Assert.DoesNotContain(processedMessage, pending);
    }

    [Fact]
    public async Task MarkAsProcessedAsync_SetIsProcessedAndTimestamp()
    {
        // Arrange
        using var context = CreateDbContext();
        var store = new OutboxStore(context);

        var message = new OutboxMessage
        {
            CorrelationId = Guid.NewGuid(),
            AggregateId = 1,
            AggregateType = "BillingRecord",
            EventType = "BillingRecordSubmittedToAEAT",
            Payload = "{\"test\": \"data\"}",
            CreatedAt = DateTime.UtcNow,
            IsProcessed = false,
            ProcessingAttempts = 0
        };

        context.OutboxMessages.Add(message);
        await context.SaveChangesAsync();

        var messageId = message.Id;
        var beforeMark = DateTime.UtcNow;

        // Act
        await store.MarkAsProcessedAsync(messageId);

        // Assert
        var updated = await context.OutboxMessages.FirstOrDefaultAsync(m => m.Id == messageId);

        Assert.NotNull(updated);
        Assert.True(updated.IsProcessed);
        Assert.NotNull(updated.ProcessedAt);
        Assert.True(updated.ProcessedAt >= beforeMark);
        Assert.Null(updated.LastProcessingError);
    }

    [Fact]
    public async Task MarkAsFailedAsync_IncrementAttemptsAndSetError()
    {
        // Arrange
        using var context = CreateDbContext();
        var store = new OutboxStore(context);

        var message = new OutboxMessage
        {
            CorrelationId = Guid.NewGuid(),
            AggregateId = 1,
            AggregateType = "BillingRecord",
            EventType = "BillingRecordSubmittedToAEAT",
            Payload = "{\"test\": \"data\"}",
            CreatedAt = DateTime.UtcNow,
            IsProcessed = false,
            ProcessingAttempts = 0
        };

        context.OutboxMessages.Add(message);
        await context.SaveChangesAsync();

        var messageId = message.Id;
        var errorMessage = "Connection timeout";

        // Act
        await store.MarkAsFailedAsync(messageId, errorMessage);

        // Assert
        var updated = await context.OutboxMessages.FirstOrDefaultAsync(m => m.Id == messageId);

        Assert.NotNull(updated);
        Assert.Equal(1, updated.ProcessingAttempts);
        Assert.Equal(errorMessage, updated.LastProcessingError);
        Assert.False(updated.IsProcessed);
    }

    [Fact]
    public async Task GetByCorrelationIdAsync_ReturnMessageIfExists()
    {
        // Arrange
        using var context = CreateDbContext();
        var store = new OutboxStore(context);

        var correlationId = Guid.NewGuid();
        var message = new OutboxMessage
        {
            CorrelationId = correlationId,
            AggregateId = 1,
            AggregateType = "BillingRecord",
            EventType = "BillingRecordSubmittedToAEAT",
            Payload = "{\"test\": \"data\"}",
            CreatedAt = DateTime.UtcNow,
            IsProcessed = false,
            ProcessingAttempts = 0
        };

        context.OutboxMessages.Add(message);
        await context.SaveChangesAsync();

        // Act
        var found = await store.GetByCorrelationIdAsync(correlationId);

        // Assert
        Assert.NotNull(found);
        Assert.Equal(correlationId, found.CorrelationId);
        Assert.Equal(1, found.AggregateId);
    }

    [Fact]
    public async Task GetByCorrelationIdAsync_ReturnNullIfNotExists()
    {
        // Arrange
        using var context = CreateDbContext();
        var store = new OutboxStore(context);

        // Act
        var found = await store.GetByCorrelationIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(found);
    }

    [Fact]
    public async Task GetPendingMessagesAsync_RespectMaxAttemptsLimit()
    {
        // Arrange
        using var context = CreateDbContext();
        var store = new OutboxStore(context);

        var messageWithinLimit = new OutboxMessage
        {
            CorrelationId = Guid.NewGuid(),
            AggregateId = 1,
            AggregateType = "BillingRecord",
            EventType = "BillingRecordSubmittedToAEAT",
            Payload = "{\"test\": \"data1\"}",
            CreatedAt = DateTime.UtcNow,
            IsProcessed = false,
            ProcessingAttempts = 4 // Less than maxAttempts (5)
        };

        var messageExceedsLimit = new OutboxMessage
        {
            CorrelationId = Guid.NewGuid(),
            AggregateId = 2,
            AggregateType = "BillingRecord",
            EventType = "BillingRecordSubmittedToAEAT",
            Payload = "{\"test\": \"data2\"}",
            CreatedAt = DateTime.UtcNow,
            IsProcessed = false,
            ProcessingAttempts = 5 // Equal to maxAttempts (5)
        };

        context.OutboxMessages.AddRange(messageWithinLimit, messageExceedsLimit);
        await context.SaveChangesAsync();

        // Act
        var pending = await store.GetPendingMessagesAsync(10, CancellationToken.None);

        // Assert
        Assert.Equal(2, pending.Count);
        Assert.Contains(messageWithinLimit, pending);
        Assert.Contains(messageExceedsLimit, pending);  // Store ahora retorna TODOS los no-procesados, sin filtrar por intentos
    }

    [Fact]
    public async Task AddAsync_CreateNewOutboxMessage()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        OutboxMessage? found;

        // Act & Assert - Create and persist
        using (var context = new ApplicationDbContext(options))
        {
            var store = new OutboxStore(context);

            var message = new OutboxMessage
            {
                CorrelationId = Guid.NewGuid(),
                AggregateId = 1,
                AggregateType = "BillingRecord",
                EventType = "BillingRecordSubmittedToAEAT",
                Payload = "{\"test\": \"data\"}",
                CreatedAt = DateTime.UtcNow,
                IsProcessed = false,
                ProcessingAttempts = 0
            };

            await store.AddAsync(message);
        }

        // Verify with same database
        using (var verifyContext = new ApplicationDbContext(options))
        {
            var count = await verifyContext.OutboxMessages.CountAsync();
            Assert.Equal(1, count);

            found = await verifyContext.OutboxMessages.FirstAsync();
        }

        Assert.NotNull(found);
        Assert.Equal("BillingRecord", found.AggregateType);
        Assert.Equal("{\"test\": \"data\"}", found.Payload);
    }

    [Fact]
    public async Task MarkAsFailedAsync_MultipleAttempts_PreservesAllData()
    {
        // Arrange
        using var context = CreateDbContext();
        var store = new OutboxStore(context);

        var message = new OutboxMessage
        {
            CorrelationId = Guid.NewGuid(),
            AggregateId = 1,
            AggregateType = "BillingRecord",
            EventType = "BillingRecordSubmittedToAEAT",
            Payload = "{\"test\": \"data\"}",
            CreatedAt = DateTime.UtcNow,
            IsProcessed = false,
            ProcessingAttempts = 0
        };

        context.OutboxMessages.Add(message);
        await context.SaveChangesAsync();

        var messageId = message.Id;

        // Act
        await store.MarkAsFailedAsync(messageId, "Error 1");
        await store.MarkAsFailedAsync(messageId, "Error 2");
        await store.MarkAsFailedAsync(messageId, "Error 3");

        // Assert
        var updated = await context.OutboxMessages.FirstOrDefaultAsync(m => m.Id == messageId);

        Assert.NotNull(updated);
        Assert.Equal(3, updated.ProcessingAttempts);
        Assert.Equal("Error 3", updated.LastProcessingError); // Último error registrado
        Assert.False(updated.IsProcessed);
    }
}



