namespace gesFactu.Api.Configuration;

public sealed class RequestLimitsOptions
{
    public const string SectionName = "RequestLimits";

    public long MaxRequestBodyBytes { get; set; } = 1_048_576;
}
