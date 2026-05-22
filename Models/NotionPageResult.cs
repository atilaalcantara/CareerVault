namespace CareerVault.Api.Models;

public sealed class NotionPageResult
{
    public required bool Success { get; init; }
    public string? PageId { get; init; }
    public string? Url { get; init; }
    public string? ErrorBody { get; init; }
}
