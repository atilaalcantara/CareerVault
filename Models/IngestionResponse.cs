using System.Text.Json;
using System.Text.Json.Serialization;

namespace CareerVault.Api.Models;

public sealed class IngestionResponse
{
    [JsonPropertyName("success")]
    public required bool Success { get; init; }

    [JsonPropertyName("professionalEntryId")]
    public Guid? ProfessionalEntryId { get; init; }

    [JsonPropertyName("geminiModelUsed")]
    public string? GeminiModelUsed { get; init; }

    [JsonPropertyName("structuredEntry")]
    public ProfessionalEntryStructuredDto? StructuredEntry { get; init; }

    [JsonPropertyName("notionPageId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NotionPageId { get; init; }

    [JsonPropertyName("notionUrl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NotionUrl { get; init; }

    [JsonPropertyName("generatedNotionPayload")]
    public JsonElement? GeneratedNotionPayload { get; init; }

    [JsonPropertyName("embeddingStatus")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EmbeddingStatus { get; init; }

    [JsonPropertyName("notionSyncStatus")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NotionSyncStatus { get; init; }

    [JsonPropertyName("notionError")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NotionError { get; init; }
}
