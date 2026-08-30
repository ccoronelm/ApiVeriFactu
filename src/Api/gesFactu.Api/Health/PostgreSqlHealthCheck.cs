using gesFactu.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace gesFactu.Api.Health;

public sealed class PostgreSqlHealthCheck : IHealthCheck
{
    private readonly ApplicationDbContext _dbContext;

    public PostgreSqlHealthCheck(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbContext.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("PostgreSQL accesible.")
                : HealthCheckResult.Unhealthy("PostgreSQL no accesible.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Error comprobando PostgreSQL.",
                ex);
        }
    }
}
