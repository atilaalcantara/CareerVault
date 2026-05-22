using System.Text.Json;
using System.Text.Json.Serialization;

namespace CareerVault.Api.Models;

public sealed class IngestionResponse
{
    [JsonPropertyName("success")]
    public required bool Success { get; init; }

    [JsonPropertyName("geminiModelUsed")]
    public string? GeminiModelUsed { get; init; }

    [JsonPropertyName("notionPageId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NotionPageId { get; init; }

    [JsonPropertyName("notionUrl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NotionUrl { get; init; }

    [JsonPropertyName("generatedNotionPayload")]
    public JsonElement? GeneratedNotionPayload { get; init; }

    [JsonPropertyName("notionError")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NotionError { get; init; }
}
