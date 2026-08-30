using Serilog.Context;

namespace gesFactu.Api.Middleware;

public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-ID";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId =
            context.Request.Headers.TryGetValue(HeaderName, out var supplied) &&
            !string.IsNullOrWhiteSpace(supplied)
                ? supplied.ToString().Trim()
                : Guid.NewGuid().ToString("D");

        if (correlationId.Length > 128)
            correlationId = correlationId[..128];

        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
