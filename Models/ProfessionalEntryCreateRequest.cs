using System.Text.Json;

namespace CareerVault.Api.Models;

public sealed class ProfessionalEntryCreateRequest
{
    public required IngestionSourceMetadata Source { get; init; }
    public required ProfessionalEntryStructuredDto StructuredEntry { get; init; }
    public required JsonElement RawPayload { get; init; }
    public required string ContentHash { get; init; }
    public required string EmbeddingModel { get; init; }
    public required int EmbeddingDimensions { get; init; }
}
