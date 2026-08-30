
using Serilog;
using gesFactu.Application;
using gesFactu.Infrastructure;
using gesFactu.Infrastructure.Integrations.VeriFactu;
using gesFactu.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Logging estructurado
builder.Host.UseSerilog((context, loggerConfig) =>
{
    loggerConfig
        .MinimumLevel.Debug()
        .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss}] {Level:u3} {SourceContext}: {Message:lj}{NewLine}{Exception}")
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "gesFactu.Api");

    if (!context.HostingEnvironment.IsProduction())
    {
        loggerConfig.MinimumLevel.Debug();
    }
});

// Servicios
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS (ajustar según necesidades)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// ?? Validación fail-fast: Development no puede usar Production endpoint ??????
// Ref: /VERIFACTU/SistemaFacturacion.wsdl.xml — endpoints por entorno
if (app.Environment.IsDevelopment())
{
    var veriFactuOptions = new VeriFactuOptions();
    app.Configuration.GetSection(VeriFactuOptions.SectionName).Bind(veriFactuOptions);

    if (veriFactuOptions.Environment == VeriFactuEntorno.Production)
    {
        throw new InvalidOperationException(
            "CONFIGURACIÓN INVÁLIDA: El entorno ASP.NET Core es 'Development' pero " +
            "VeriFactu:Environment está configurado como 'Production'. " +
            "En Development solo se permite VeriFactu:Environment=Test. " +
            "Endpoint TEST oficial: " + VeriFactuOptions.EndpointTest +
            " | Ref: /VERIFACTU/SistemaFacturacion.wsdl.xml");
    }
}

// Middleware
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();

app.MapControllers();

Log.Information("Iniciando aplicación gesFactu");
app.Run();

