using System.Security.Cryptography;
using System.Text;
using gesFactu.Api.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;

namespace gesFactu.Api.Middleware;

/// <summary>
/// Autenticación simple servidor-a-servidor para la API interna consumida por Python.
/// CORS no sustituye esta comprobación.
/// </summary>
public sealed class ApiKeyAuthenticationMiddleware
{
    public const string HeaderName = "X-GesFactu-Api-Key";

    private readonly RequestDelegate _next;
    private readonly IHostEnvironment _environment;

    public ApiKeyAuthenticationMiddleware(
        RequestDelegate next,
        IHostEnvironment environment)
    {
        _next = next;
        _environment = environment;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IOptions<SecurityOptions> options)
    {
        if (IsPublicEndpoint(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var expected = options.Value.ResolveApiKey();

        // Desarrollo local puede funcionar sin secreto. Producción nunca:
        // Program.cs valida fail-fast antes de aceptar tráfico.
        if (string.IsNullOrEmpty(expected) && _environment.IsDevelopment())
        {
            await _next(context);
            return;
        }

        if (string.IsNullOrEmpty(expected) ||
            !context.Request.Headers.TryGetValue(HeaderName, out var supplied) ||
            !FixedTimeEquals(expected, supplied.ToString()))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Title = "Unauthorized",
                Status = StatusCodes.Status401Unauthorized,
                Detail = $"Se requiere una cabecera {HeaderName} válida.",
                Instance = context.Request.Path
            });
            return;
        }

        await _next(context);
    }

    private static bool IsPublicEndpoint(PathString path)
        => path.StartsWithSegments("/health/live") ||
           path.StartsWithSegments("/health/ready");

    private static bool FixedTimeEquals(string expected, string supplied)
    {
        var left = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        var right = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        return CryptographicOperations.FixedTimeEquals(left, right);
    }
}
