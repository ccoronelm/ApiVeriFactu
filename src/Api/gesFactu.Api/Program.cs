using System.Text.Json;
using gesFactu.Api.Configuration;
using gesFactu.Api.Health;
using gesFactu.Api.Middleware;
using gesFactu.Application;
using gesFactu.Infrastructure;
using gesFactu.Infrastructure.Integrations.VeriFactu;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
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

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

builder.Services.Configure<OperationsOptions>(
    builder.Configuration.GetSection(OperationsOptions.SectionName));

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

var app = builder.Build();

var veriFactuOptions = new VeriFactuOptions();
app.Configuration
    .GetSection(VeriFactuOptions.SectionName)
    .Bind(veriFactuOptions);

var operationsOptions = new OperationsOptions();
app.Configuration
    .GetSection(OperationsOptions.SectionName)
    .Bind(operationsOptions);

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

    if (veriFactuOptions.ClientMode != "SoapClient")
    {
        throw new InvalidOperationException(
            "Producción requiere VeriFactu:ClientMode=Soap.");
    }

    if (string.IsNullOrWhiteSpace(operationsOptions.AdminApiKey) ||
        operationsOptions.AdminApiKey.Length < 32)
    {
        throw new InvalidOperationException(
            "Producción requiere Operations:AdminApiKey con al menos 32 caracteres, suministrada como secreto.");
    }
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate =
        "HTTP {RequestMethod} {RequestPath} -> {StatusCode} en {Elapsed:0.0000} ms";
});

var openApiEnabled =
    app.Environment.IsDevelopment() ||
    app.Configuration.GetValue<bool>("OpenApi:Enabled");

if (openApiEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthorization();

app.MapHealthChecks(
    "/health/live",
    new HealthCheckOptions
    {
        Predicate = _ => false,
        ResponseWriter = WriteHealthResponseAsync
    });

app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions
    {
        Predicate = registration =>
            registration.Tags.Contains("ready"),
        ResponseWriter = WriteHealthResponseAsync
    });

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
