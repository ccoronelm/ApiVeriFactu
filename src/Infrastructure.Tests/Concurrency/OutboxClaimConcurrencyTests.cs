using gesFactu.Domain.Entities;
using gesFactu.Infrastructure.Persistence;
using gesFactu.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace gesFactu.Infrastructure.Tests.Concurrency;

public sealed class OutboxClaimConcurrencyTests
{
    [SqlServerFact]
    public async Task TwoWorkers_CannotClaimSameMessage()
    {
        var connectionString = await CreateDatabaseAsync();

        try
        {
            long messageId;

            await using (var setup = CreateContext(connectionString))
            {
                var message = new OutboxMessage
                {
                    CorrelationId = Guid.NewGuid(),
                    AggregateId = 100,
                    AggregateType = "BillingRecord",
                    EventType = "BillingRecordSubmittedToAEAT",
                    Payload = "{}",
                    CreatedAt = DateTime.UtcNow,
                    IsProcessed = false
                };

                setup.OutboxMessages.Add(message);
                await setup.SaveChangesAsync();
                messageId = message.Id;
            }

            await using var context1 = CreateContext(connectionString);
            await using var context2 = CreateContext(connectionString);

            var store1 = new OutboxStore(context1);
            var store2 = new OutboxStore(context2);

            var claims = await Task.WhenAll(
                store1.ClaimPendingMessagesAsync(
                    "worker-1",
                    1,
                    TimeSpan.FromMinutes(5)),
                store2.ClaimPendingMessagesAsync(
                    "worker-2",
                    1,
                    TimeSpan.FromMinutes(5)));

            Assert.Equal(1, claims.Sum(x => x.Count));

            var claimed = claims.SelectMany(x => x).Single();
            Assert.Equal(messageId, claimed.Id);
            Assert.NotNull(claimed.LockedBy);
            Assert.NotNull(claimed.LockedUntil);
        }
        finally
        {
            await DeleteDatabaseAsync(connectionString);
        }
    }

    [SqlServerFact]
    public async Task ExpiredLease_CanBeClaimedByAnotherWorker()
    {
        var connectionString = await CreateDatabaseAsync();

        try
        {
            long messageId;

            await using (var setup = CreateContext(connectionString))
            {
                var message = new OutboxMessage
                {
                    CorrelationId = Guid.NewGuid(),
                    AggregateId = 101,
                    AggregateType = "BillingRecord",
                    EventType = "BillingRecordSubmittedToAEAT",
                    Payload = "{}",
                    CreatedAt = DateTime.UtcNow,
                    IsProcessed = false,
                    LockedBy = "dead-worker",
                    LockedUntil = DateTime.UtcNow.AddMinutes(-1)
                };

                setup.OutboxMessages.Add(message);
                await setup.SaveChangesAsync();
                messageId = message.Id;
            }

            await using var context = CreateContext(connectionString);
            var store = new OutboxStore(context);

            var claimed = await store.ClaimPendingMessagesAsync(
                "new-worker",
                1,
                TimeSpan.FromMinutes(5));

            Assert.Single(claimed);
            Assert.Equal(messageId, claimed[0].Id);
            Assert.Equal("new-worker", claimed[0].LockedBy);
        }
        finally
        {
            await DeleteDatabaseAsync(connectionString);
        }
    }

    [SqlServerFact]
    public async Task ScheduledRetry_IsNotClaimedBeforeDueTime()
    {
        var connectionString = await CreateDatabaseAsync();

        try
        {
            await using (var setup = CreateContext(connectionString))
            {
                setup.OutboxMessages.Add(new OutboxMessage
                {
                    CorrelationId = Guid.NewGuid(),
                    AggregateId = 102,
                    AggregateType = "BillingRecord",
                    EventType = "BillingRecordSubmittedToAEAT",
                    Payload = "{}",
                    CreatedAt = DateTime.UtcNow,
                    IsProcessed = false,
                    NextAttemptAt = DateTime.UtcNow.AddMinutes(5)
                });

                await setup.SaveChangesAsync();
            }

            await using var context = CreateContext(connectionString);
            var store = new OutboxStore(context);

            var claimed = await store.ClaimPendingMessagesAsync(
                "worker",
                10,
                TimeSpan.FromMinutes(5));

            Assert.Empty(claimed);
        }
        finally
        {
            await DeleteDatabaseAsync(connectionString);
        }
    }

    private static ApplicationDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task<string> CreateDatabaseAsync()
    {
        var serverConnection = Environment.GetEnvironmentVariable(
            "GESFACTU_TEST_SQLSERVER");

        if (string.IsNullOrWhiteSpace(serverConnection))
            throw new InvalidOperationException(
                "GESFACTU_TEST_SQLSERVER no está configurada.");

        var databaseName = "gesFactuOutboxCi_" + Guid.NewGuid().ToString("N");
        var connectionString =
            serverConnection.TrimEnd(';') + $";Database={databaseName};";

        Exception? lastError = null;

        for (var attempt = 0; attempt < 30; attempt++)
        {
            try
            {
                await using var context = CreateContext(connectionString);
                await context.Database.EnsureCreatedAsync();
                return connectionString;
            }
            catch (Exception ex)
            {
                lastError = ex;
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }

        throw new InvalidOperationException(
            "SQL Server de pruebas no estuvo disponible a tiempo.",
            lastError);
    }

    private static async Task DeleteDatabaseAsync(string connectionString)
    {
        try
        {
            await using var context = CreateContext(connectionString);
            await context.Database.EnsureDeletedAsync();
        }
        catch
        {
        }
    }
}
