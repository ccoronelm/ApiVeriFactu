using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Infrastructure.Persistence;
using gesFactu.Infrastructure.VeriFactu;

namespace gesFactu.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Persistencia - EF Core con SQL Server
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

            options.UseSqlServer(connectionString);
        });

        // Puerto de persistencia
        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<ApplicationDbContext>());

        // Hash calculation (SHA256 para VERI*FACTU)
        services.AddSingleton<IHashCalculator, Sha256HashCalculator>();

        // Puerto de AEAT (stub - se implementará después)
        // services.AddScoped<IVeriFactuGateway, VeriFactuGatewayAdapter>();

        return services;
    }
}


