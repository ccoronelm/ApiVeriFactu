using System.Text.Json;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Domain.Entities;
using gesFactu.Domain.ValueObjects;
using gesFactu.Infrastructure.Outbox;
using gesFactu.Infrastructure.Persistence;
using gesFactu.Infrastructure.Persistence.Repositories;
using gesFactu.Infrastructure.Tests.Concurrency;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace gesFactu.Infrastructure.Tests.Outbox;

public sealed class OutboxProcessorIntegrationTests
{
    [SqlServerFact]
    public async Task AcceptedGatewayResponse_IsAuditedAndClosesOutbox()
    {
        var connectionString = await CreateDatabaseAsync();

        try
        {
            var recordId = await SeedQueuedRecordAsync(connectionString);

            var services = new ServiceCollection();

            services.AddDbContext<ApplicationDbContext>(
                options => options.UseSqlServer(connectionString));

            services.AddScoped<IOutboxStore, OutboxStore>();
            services.AddScoped<IDeadLetterStore, DeadLetterStore>();
            services.AddScoped<ISubmissionAttemptStore, SubmissionAttemptStore>();
            services.AddScoped<IBillingRecordRepository, BillingRecordRepository>();
            services.AddScoped<IVeriFactuGateway, AcceptedGateway>();

            await using var provider = services.BuildServiceProvider();

            var worker = new OutboxProcessorService(
                provider,
                NullLogger<OutboxProcessorService>.Instance,
                new OutboxProcessorOptions
                {
                    BatchSize = 1,
                    LeaseDurationSeconds = 30,
                    IdleDelayMilliseconds = 50,
                    ErrorDelayMilliseconds = 50,
                    RetryPolicy = new RetryPolicy
                    {
                        MaxAttempts = 3,
                        BaseDelayMilliseconds = 20,
                        MaxDelayMilliseconds = 100,
                        MaxJitterMilliseconds = 0
                    }
                });

            await worker.StartAsync(CancellationToken.None);

            try
            {
                await WaitUntilProcessedAsync(
                    connectionString,
                    recordId,
                    TimeSpan.FromSeconds(10));
            }
            finally
            {
                using var stopCts = new CancellationTokenSource(
                    TimeSpan.FromSeconds(5));

                await worker.StopAsync(stopCts.Token);
                worker.Dispose();
            }

            await using var verify = CreateContext(connectionString);

            var record = await verify.BillingRecords
                .SingleAsync(x => x.Id == recordId);

            var outbox = await verify.OutboxMessages
                .SingleAsync(x => x.AggregateId == recordId);

            var attempts = await verify.SubmissionAttempts
                .Where(x => x.BillingRecordId == recordId)
                .OrderBy(x => x.AttemptNumber)
                .ToListAsync();

            Assert.True(outbox.IsProcessed);
            Assert.Null(outbox.LockedBy);
            Assert.Null(outbox.LockedUntil);

            Assert.Equal("CSV-INTEGRATION-001", record.AeatSubmissionId);
            Assert.Equal("Aceptado", record.Status);

            var attempt = Assert.Single(attempts);
            Assert.Equal(1, attempt.AttemptNumber);
            Assert.Equal(SubmissionAttemptStatus.Success, attempt.Status);
            Assert.Equal("CSV-INTEGRATION-001", attempt.AeatSubmissionId);
            Assert.Equal("<soap>accepted</soap>", attempt.ResponsePayload);
        }
        finally
        {
            await DeleteDatabaseAsync(connectionString);
        }
    }

    [SqlServerFact]
    public async Task ConfirmedDuplicate_IsReconciledAsSuccessAndClosesOutbox()
    {
        var connectionString = await CreateDatabaseAsync();

        try
        {
            var recordId = await SeedQueuedRecordAsync(connectionString);

            var services = new ServiceCollection();

            services.AddDbContext<ApplicationDbContext>(
                options => options.UseSqlServer(connectionString));

            services.AddScoped<IOutboxStore, OutboxStore>();
            services.AddScoped<IDeadLetterStore, DeadLetterStore>();
            services.AddScoped<ISubmissionAttemptStore, SubmissionAttemptStore>();
            services.AddScoped<IBillingRecordRepository, BillingRecordRepository>();
            services.AddScoped<IVeriFactuGateway, DuplicateGateway>();

            await using var provider = services.BuildServiceProvider();

            var worker = new OutboxProcessorService(
                provider,
                NullLogger<OutboxProcessorService>.Instance,
                new OutboxProcessorOptions
                {
                    BatchSize = 1,
                    LeaseDurationSeconds = 30,
                    IdleDelayMilliseconds = 50,
                    ErrorDelayMilliseconds = 50,
                    RetryPolicy = new RetryPolicy
                    {
                        MaxAttempts = 3,
                        BaseDelayMilliseconds = 20,
                        MaxDelayMilliseconds = 100,
                        MaxJitterMilliseconds = 0
                    }
                });

            await worker.StartAsync(CancellationToken.None);

            try
            {
                await WaitUntilProcessedAsync(
                    connectionString,
                    recordId,
                    TimeSpan.FromSeconds(10));
            }
            finally
            {
                using var stopCts = new CancellationTokenSource(
                    TimeSpan.FromSeconds(5));

                await worker.StopAsync(stopCts.Token);
                worker.Dispose();
            }

            await using var verify = CreateContext(connectionString);

            var record = await verify.BillingRecords
                .SingleAsync(x => x.Id == recordId);

            var outbox = await verify.OutboxMessages
                .SingleAsync(x => x.AggregateId == recordId);

            var attempt = await verify.SubmissionAttempts
                .SingleAsync(x => x.BillingRecordId == recordId);

            Assert.True(outbox.IsProcessed);
            Assert.Null(record.AeatSubmissionId);
            Assert.Equal("AceptadoPorDuplicadoAEAT", record.Status);

            Assert.Equal(SubmissionAttemptStatus.Success, attempt.Status);
            Assert.Equal("3000", attempt.ResponseCode);
            Assert.Null(attempt.AeatSubmissionId);
            Assert.Contains(
                "IdPeticionRegistroDuplicado=PETICION-RECUPERADA",
                attempt.ResponseDescription);
            Assert.Equal("<soap>duplicate</soap>", attempt.ResponsePayload);

            Assert.Empty(await verify.DeadLetterMessages.ToListAsync());
        }
        finally
        {
            await DeleteDatabaseAsync(connectionString);
        }
    }

    private static async Task<int> SeedQueuedRecordAsync(string connectionString)
    {
        await using var context = CreateContext(connectionString);

        var nif = ((ValueObjectResult<TaxpayerNif>.SuccessWithValue)
            TaxpayerNif.Create("89890001K")).Value;

        var series = ((ValueObjectResult<InvoiceSeries>.SuccessWithValue)
            InvoiceSeries.Create("A/")).Value;

        var number = ((ValueObjectResult<InvoiceNumber>.SuccessWithValue)
            InvoiceNumber.Create("0009")).Value;

        var invoiceIdentifier =
            ((ValueObjectResult<InvoiceIdentifier>.SuccessWithValue)
                InvoiceIdentifier.Create(
                    nif,
                    series,
                    number,
                    new DateOnly(2026, 8, 30))).Value;

        var total = ((ValueObjectResult<Money>.SuccessWithValue)
            Money.Create(121m)).Value;

        var tax = ((ValueObjectResult<Money>.SuccessWithValue)
            Money.Create(21m)).Value;

        var record = BillingRecord.Create(
            invoiceIdentifier,
            "EMISOR PRUEBAS",
            "87654321B",
            "DESTINATARIO PRUEBAS",
            "Servicio de pruebas",
            total,
            tax,
            registerTimestamp: "2026-08-30T08:00:00+02:00");

        record.SetComputedHash(
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");

        var correlationId = Guid.NewGuid();
        record.MarkAsQueued(correlationId);

        context.BillingRecords.Add(record);
        await context.SaveChangesAsync();

        var request = new VeriFactuSubmissionRequest
        {
            TaxpayerNif = "89890001K",
            SignedXmlContent = "<test />"
        };

        context.OutboxMessages.Add(new OutboxMessage
        {
            CorrelationId = correlationId,
            AggregateId = record.Id,
            AggregateType = "BillingRecord",
            EventType = "BillingRecordSubmittedToAEAT",
            Payload = JsonSerializer.Serialize(request),
            CreatedAt = DateTime.UtcNow,
            IsProcessed = false
        });

        await context.SaveChangesAsync();

        return record.Id;
    }

    private static async Task WaitUntilProcessedAsync(
        string connectionString,
        int recordId,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow.Add(timeout);

        while (DateTime.UtcNow < deadline)
        {
            await using var context = CreateContext(connectionString);

            var processed = await context.OutboxMessages
                .Where(x => x.AggregateId == recordId)
                .Select(x => x.IsProcessed)
                .SingleAsync();

            if (processed)
                return;

            await Task.Delay(50);
        }

        throw new TimeoutException(
            "El OutboxProcessor no completó el mensaje dentro del tiempo esperado.");
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

        var databaseName = "gesFactuWorkerCi_" + Guid.NewGuid().ToString("N");
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

    private sealed class DuplicateGateway : IVeriFactuGateway
    {
        public Task<VeriFactuSubmissionResult> SubmitBillingRecordAsync(
            VeriFactuSubmissionRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new VeriFactuSubmissionResult
            {
                SubmissionId = null,
                IsAccepted = false,
                ResponseCode = AeatResponseCode.DuplicateError,
                StatusCode = "Incorrecto",
                RecordStatus = "Incorrecto",
                StatusDescription = "Registro de facturación duplicado",
                ErrorCode = "3000",
                IsDuplicate = true,
                DuplicateRecordStatus = "Correcta",
                DuplicateRequestId = "PETICION-RECUPERADA",
                RawResponsePayload = "<soap>duplicate</soap>"
            });

        public Task<VeriFactuQueryResult> QueryBillingRecordAsync(
            VeriFactuQueryRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<VeriFactuCancellationResult> CancelBillingRecordAsync(
            VeriFactuCancellationRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class AcceptedGateway : IVeriFactuGateway
    {
        public Task<VeriFactuSubmissionResult> SubmitBillingRecordAsync(
            VeriFactuSubmissionRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new VeriFactuSubmissionResult
            {
                SubmissionId = "CSV-INTEGRATION-001",
                IsAccepted = true,
                ResponseCode = AeatResponseCode.Success,
                StatusCode = "Correcto",
                RecordStatus = "Correcto",
                StatusDescription = "Registro aceptado",
                RawResponsePayload = "<soap>accepted</soap>"
            });

        public Task<VeriFactuQueryResult> QueryBillingRecordAsync(
            VeriFactuQueryRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<VeriFactuCancellationResult> CancelBillingRecordAsync(
            VeriFactuCancellationRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
