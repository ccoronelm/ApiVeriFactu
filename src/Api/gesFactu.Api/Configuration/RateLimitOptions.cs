namespace gesFactu.Api.Configuration;

public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimit";

    public int PermitLimit { get; set; } = 300;
    public int WindowSeconds { get; set; } = 60;
}
