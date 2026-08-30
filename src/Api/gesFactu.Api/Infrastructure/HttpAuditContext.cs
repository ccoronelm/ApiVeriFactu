using gesFactu.Application.Common.Abstractions;

namespace gesFactu.Api.Infrastructure;

public sealed class HttpAuditContext : IAuditContext
{
    public const string ActorHeader = "X-GesFactu-Actor";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpAuditContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string Actor
    {
        get
        {
            var context = _httpContextAccessor.HttpContext;
            if (context is null)
                return "system";

            var supplied = context.Request.Headers[ActorHeader].ToString().Trim();
            if (string.IsNullOrWhiteSpace(supplied))
                return "api-key-client";

            return supplied.Length <= 256 ? supplied : supplied[..256];
        }
    }

    public string? CorrelationId
        => _httpContextAccessor.HttpContext?.TraceIdentifier;
}
