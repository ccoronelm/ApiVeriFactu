using gesFactu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace gesFactu.Infrastructure.Idempotency;

/// <summary>
/// Limpia respuestas idempotentes completadas una vez superada su retención.
/// Los registros Pending nunca se eliminan automáticamente: un resultado incierto
/// debe revisarse de forma explícita.
/// </summary>
public sealed class IdempotencyCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<IdempotencyCleanupService> _logger;

    public IdempotencyCleanupService(
        IServiceProvider serviceProvider,
        ILogger<IdempotencyCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

                var deleted = await db.IdempotencyRecords
                    .Where(x =>
                        x.Status == "Completed" &&
                        x.ExpiresAtUtc < DateTime.UtcNow)
                    .ExecuteDeleteAsync(stoppingToken);

                if (deleted > 0)
                {
                    _logger.LogInformation(
                        "Limpieza de idempotencia: {Count} registros expirados eliminados.",
                        deleted);
                }
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "No se pudo limpiar IdempotencyRecords expirados.");
            }

            await Task.Delay(
                TimeSpan.FromHours(1),
                stoppingToken);
        }
    }
}
