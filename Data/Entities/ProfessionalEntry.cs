using System.Text.Json;

namespace CareerVault.Api.Data.Entities;

public sealed class ProfessionalEntry
{
    public Guid Id { get; set; }
    public required string SourceType { get; set; }
    public string? SourceExternalId { get; set; }
    public required string Title { get; set; }
    public required string Content { get; set; }
    public string? Summary { get; set; }
    public string? Company { get; set; }
    public string? Project { get; set; }
    public string? Role { get; set; }
    public DateTimeOffset? OccurredAt { get; set; }
    public string[] Technologies { get; set; } = [];
    public string[] Tags { get; set; } = [];
    public required JsonDocument RawPayload { get; set; }
    public required string ContentHash { get; set; }
    public required string EmbeddingStatus { get; set; }
    public string? EmbeddingModel { get; set; }
    public int? EmbeddingDimensions { get; set; }
    public DateTimeOffset? EmbeddingUpdatedAt { get; set; }
    public string? EmbeddingError { get; set; }
    public required string NotionSyncStatus { get; set; }
    public string? NotionPageId { get; set; }
    public string? NotionLastError { get; set; }
    public DateTimeOffset? NotionSyncedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<ProfessionalEntryEmbedding> Embeddings { get; set; } = [];
}
