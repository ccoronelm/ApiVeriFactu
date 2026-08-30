namespace gesFactu.Domain.Entities;

public sealed class IdempotencyRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public int? ResponseStatusCode { get; set; }
    public string? ResponseContentType { get; set; }
    public string? ResponseBody { get; set; }
    public string? ResponseLocation { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}
