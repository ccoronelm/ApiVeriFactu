namespace gesFactu.Api.Configuration;

public sealed class ReverseProxyOptions
{
    public const string SectionName = "ReverseProxy";

    /// <summary>
    /// IPs exactas de proxies de confianza que pueden aportar X-Forwarded-*.
    /// </summary>
    public string[] TrustedProxies { get; set; } = Array.Empty<string>();
}
