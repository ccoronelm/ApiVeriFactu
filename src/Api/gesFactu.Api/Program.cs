using System.Net;
using System.Text.Json;
using System.Threading.RateLimiting;
using gesFactu.Api.Configuration;
using gesFactu.Api.Health;
using gesFactu.Api.Infrastructure;
using gesFactu.Api.Middleware;
using gesFactu.Application;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Infrastructure;
using gesFactu.Infrastructure.Integrations.VeriFactu;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfig) =>
{
    loggerConfig
        .MinimumLevel.Is(
            context.HostingEnvironment.IsProduction()
                ? Serilog.Events.LogEventLevel.Information
                : Serilog.Events.LogEventLevel.Debug)
        .MinimumLevel.Override(
            "Microsoft.AspNetCore",
            Serilog.Events.LogEventLevel.Warning)
        .MinimumLevel.Override(
            "Microsoft.EntityFrameworkCore.Database.Command",
            Serilog.Events.LogEventLevel.Warning)
        .WriteTo.Console(
            outputTemplate:
                "[{Timestamp:yyyy-MM-dd HH:mm:ss}] {Level:u3} " +
                "{SourceContext} [{CorrelationId}] {Message:lj}" +
                "{NewLine}{Exception}")
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "gesFactu.Api");
});

var maxRequestBodyBytes = Math.Max(
    64 * 1024,
    builder.Configuration.GetValue<long?>(
        "RequestLimits:MaxRequestBodyBytes") ?? 1_048_576);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = maxRequestBodyBytes;
});

builder.Services.Configure<SecurityOptions>(
    builder.Configuration.GetSection(SecurityOptions.SectionName));
builder.Services.Configure<OperationsOptions>(
    builder.Configuration.GetSection(OperationsOptions.SectionName));
builder.Services.Configure<IdempotencyOptions>(
    builder.Configuration.GetSection(IdempotencyOptions.SectionName));
builder.Services.Configure<RateLimitOptions>(
    builder.Configuration.GetSection(RateLimitOptions.SectionName));
builder.Services.Configure<RequestLimitsOptions>(
    builder.Configuration.GetSection(RequestLimitsOptions.SectionName));
builder.Services.Configure<ReverseProxyOptions>(
    builder.Configuration.GetSection(ReverseProxyOptions.SectionName));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditContext, HttpAuditContext>();

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services
    .AddHealthChecks()
    .AddCheck<PostgreSqlHealthCheck>(
        "postgresql",
        tags: ["ready"])
    .AddCheck<VeriFactuReadinessHealthCheck>(
        "verifactu",
        tags: ["ready"]);

var allowedOrigins =
    builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>()
    ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        if (allowedOrigins.Length > 0)
            policy.WithOrigins(allowedOrigins);

        policy.AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var rateLimit = new RateLimitOptions();
builder.Configuration
    .GetSection(RateLimitOptions.SectionName)
    .Bind(rateLimit);

var permitLimit = Math.Clamp(rateLimit.PermitLimit, 10, 10000);
var rateWindow = TimeSpan.FromSeconds(
    Math.Clamp(rateLimit.WindowSeconds, 1, 3600));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter =
        PartitionedRateLimiter.Create<HttpContext, string>(context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = rateWindow,
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
});

var reverseProxy = new ReverseProxyOptions();
builder.Configuration
    .GetSection(ReverseProxyOptions.SectionName)
    .Bind(reverseProxy);

var trustedProxyAddresses = reverseProxy.TrustedProxies
    .Select(x => IPAddress.TryParse(x, out var ip) ? ip : null)
    .Where(x => x is not null)
    .Cast<IPAddress>()
    .ToArray();

if (trustedProxyAddresses.Length > 0)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders =
            ForwardedHeaders.XForwardedFor |
            ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();

        foreach (var proxy in trustedProxyAddresses)
            options.KnownProxies.Add(proxy);
    });
}

var app = builder.Build();

var veriFactuOptions = new VeriFactuOptions();
app.Configuration
    .GetSection(VeriFactuOptions.SectionName)
    .Bind(veriFactuOptions);

var operationsOptions = new OperationsOptions();
app.Configuration
    .GetSection(OperationsOptions.SectionName)
    .Bind(operationsOptions);

var securityOptions = new SecurityOptions();
app.Configuration
    .GetSection(SecurityOptions.SectionName)
    .Bind(securityOptions);

if (app.Environment.IsDevelopment() &&
    veriFactuOptions.Environment == VeriFactuEntorno.Production)
{
    throw new InvalidOperationException(
        "CONFIGURACIÓN INVÁLIDA: Development no puede usar AEAT Production.");
}

if (app.Environment.IsProduction())
{
    if (veriFactuOptions.Environment != VeriFactuEntorno.Production)
    {
        throw new InvalidOperationException(
            "En ASP.NET Core Production, VeriFactu:Environment debe ser Production.");
    }

    if (!veriFactuOptions.AllowProduction)
    {
        throw new InvalidOperationException(
            "Producción requiere VeriFactu:AllowProduction=true de forma explícita.");
    }

    if (!string.Equals(
            veriFactuOptions.ClientMode,
            "SoapClient",
            StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            "Producción requiere VeriFactu:ClientMode=SoapClient.");
    }

    var apiKey = securityOptions.ResolveApiKey();
    if (apiKey.Length < 32)
    {
        throw new InvalidOperationException(
            "Producción requiere Security:ApiKey/ApiKeyFile con al menos 32 caracteres.");
    }

    var adminKey = operationsOptions.ResolveAdminApiKey();
    if (adminKey.Length < 32)
    {
        throw new InvalidOperationException(
            "Producción requiere Operations:AdminApiKey/AdminApiKeyFile con al menos 32 caracteres.");
    }

    var allowedHosts = app.Configuration["AllowedHosts"]?.Trim();
    if (string.IsNullOrWhiteSpace(allowedHosts) || allowedHosts == "*")
    {
        throw new InvalidOperationException(
            "Producción requiere AllowedHosts explícito; no se permite '*'.");
    }
}

if (trustedProxyAddresses.Length > 0)
    app.UseForwardedHeaders();

if (app.Environment.IsProduction())
    app.UseHsts();

app.UseHttpsRedirection();

app.UseMiddleware<CorrelationIdMiddleware>();

app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate =
        "HTTP {RequestMethod} {RequestPath} -> {StatusCode} en {Elapsed:0.0000} ms";
});

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseRateLimiter();
app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
app.UseMiddleware<IdempotencyMiddleware>();

var openApiEnabled =
    app.Environment.IsDevelopment() ||
    app.Configuration.GetValue<bool>("OpenApi:Enabled");

if (openApiEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Frontend");
app.UseAuthorization();

app.MapHealthChecks(
    "/health/live",
    new HealthCheckOptions
    {
        Predicate = _ => false,
        ResponseWriter = WriteHealthResponseAsync
    })
    .DisableRateLimiting();

app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions
    {
        Predicate = registration =>
            registration.Tags.Contains("ready"),
        ResponseWriter = WriteHealthResponseAsync
    })
    .DisableRateLimiting();

app.MapControllers();

Log.Information(
    "Iniciando gesFactu. ASPNET={AspNetEnvironment}; AEAT={AeatEnvironment}; ClientMode={ClientMode}",
    app.Environment.EnvironmentName,
    veriFactuOptions.Environment,
    veriFactuOptions.ClientMode);

app.Run();

static Task WriteHealthResponseAsync(
    HttpContext context,
    HealthReport report)
{
    context.Response.ContentType = "application/json; charset=utf-8";

    return context.Response.WriteAsync(
        JsonSerializer.Serialize(
            new
            {
                status = report.Status.ToString(),
                checks = report.Entries.Select(x => new
                {
                    name = x.Key,
                    status = x.Value.Status.ToString(),
                    description = x.Value.Description
                })
            }));
}

public partial class Program;
