namespace gesFactu.Api.Configuration;

public sealed class IdempotencyOptions
{
    public const string SectionName = "Idempotency";

    public bool RequireForUnsafeMethods { get; set; } = true;
    public int RetentionHours { get; set; } = 48;
    public int MaxKeyLength { get; set; } = 200;
}
