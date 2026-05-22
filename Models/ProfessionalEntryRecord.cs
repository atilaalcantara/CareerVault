using System.Text.Json;

namespace CareerVault.Api.Models;

public sealed class ProfessionalEntryRecord
{
    public Guid Id { get; init; }
    public required string SourceType { get; init; }
    public string? SourceExternalId { get; init; }
    public required string Title { get; init; }
    public required string Content { get; init; }
    public string? Summary { get; init; }
    public string? Company { get; init; }
    public string? Project { get; init; }
    public string? Role { get; init; }
    public DateTimeOffset? OccurredAt { get; init; }
    public string[] Technologies { get; init; } = [];
    public string[] Tags { get; init; } = [];
    public required string ContentHash { get; init; }
    public required string EmbeddingStatus { get; init; }
    public string? EmbeddingModel { get; init; }
    public int? EmbeddingDimensions { get; init; }
    public DateTimeOffset? EmbeddingUpdatedAt { get; init; }
    public string? EmbeddingError { get; init; }
    public required string NotionSyncStatus { get; init; }
    public string? NotionPageId { get; init; }
    public string? NotionLastError { get; init; }
    public DateTimeOffset? NotionSyncedAt { get; init; }
    public JsonElement RawPayload { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
