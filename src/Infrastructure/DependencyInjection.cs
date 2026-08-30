using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Infrastructure.Persistence;
using gesFactu.Infrastructure.Persistence.Repositories;
using gesFactu.Infrastructure.VeriFactu;
using gesFactu.Infrastructure.Integrations.VeriFactu;
using gesFactu.Infrastructure.Integrations.VeriFactu.Certificate;
using gesFactu.Infrastructure.Integrations.VeriFactu.Validation;
using gesFactu.Infrastructure.Integrations.VeriFactu.XmlGeneration;
using gesFactu.Infrastructure.Integrations.QRCode;
using gesFactu.Infrastructure.Outbox;
using gesFactu.Infrastructure.Idempotency;

namespace gesFactu.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Persistencia - EF Core con PostgreSQL
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

            options.UseNpgsql(connectionString);
        });

        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<ApplicationDbContext>());

        services.AddScoped<IBillingRecordRepository, BillingRecordRepository>();
        services.AddScoped<IOutboxStore, OutboxStore>();
        services.AddScoped<IDeadLetterStore, DeadLetterStore>();
        services.AddScoped<ISubmissionAttemptStore, SubmissionAttemptStore>();
        services.AddScoped<IAuditLogReader, AuditLogReader>();
        services.AddScoped<IOperationalMetricsStore, OperationalMetricsStore>();
        services.AddSingleton<IHashCalculator, Sha256HashCalculator>();
        services.AddScoped<IQRCodeGenerator, QRCodeGenerator>();
        services.AddVeriFactuClient(configuration);
        services.AddScoped<IXmlSchemaValidator, XmlSchemaValidator>();
        services.AddScoped<IRegistroAltaXmlBuilder, RegistroAltaXmlBuilderAdapter>();
        services.AddScoped<IRegistroAnulacionXmlBuilder, RegistroAnulacionXmlBuilderAdapter>();
        services.AddHostedService<OutboxProcessorService>();
        services.AddHostedService<IdempotencyCleanupService>();

        return services;
    }
}
