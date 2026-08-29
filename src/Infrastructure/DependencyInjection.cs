using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Infrastructure.Persistence;
using gesFactu.Infrastructure.Persistence.Repositories;
using gesFactu.Infrastructure.VeriFactu;
using gesFactu.Infrastructure.Integrations.VeriFactu;
using gesFactu.Infrastructure.Integrations.QRCode;
using gesFactu.Infrastructure.Outbox;

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

        // Repositorios
        services.AddScoped<IBillingRecordRepository, BillingRecordRepository>();
        services.AddScoped<SubmissionAttemptRepository>();

        // Outbox store para procesamiento confiable
        services.AddScoped<IOutboxStore, OutboxStore>();

        // Dead letter store para mensajes irrecuperables
        services.AddScoped<IDeadLetterStore, DeadLetterStore>();

        // Submission attempt store para auditoría
        services.AddScoped<ISubmissionAttemptStore, SubmissionAttemptStore>();

        // Hash calculation (SHA256 para VERI*FACTU)
        services.AddSingleton<IHashCalculator, Sha256HashCalculator>();

        // QR Code generator
        services.AddScoped<IQRCodeGenerator, QRCodeGenerator>();

        // Puerto de AEAT (stub para MVP - en producción usar VeriFactuGateway real)
        services.AddScoped<IVeriFactuGateway, VeriFactuGatewayStub>();

        // Servicio background para procesar outbox
        services.AddHostedService<OutboxProcessorService>();

        return services;
    }
}

