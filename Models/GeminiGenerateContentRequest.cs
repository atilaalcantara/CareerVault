using System.Text.Json.Serialization;

namespace CareerVault.Api.Models;

public sealed class GeminiGenerateContentRequest
{
    [JsonPropertyName("contents")]
    public required List<GeminiContent> Contents { get; init; }

    [JsonPropertyName("generationConfig")]
    public required GeminiGenerationConfig GenerationConfig { get; init; }
}

public sealed class GeminiContent
{
    [JsonPropertyName("parts")]
    public required List<GeminiPart> Parts { get; init; }
}

public sealed class GeminiPart
{
    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; init; }

    [JsonPropertyName("inline_data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GeminiInlineData? InlineData { get; init; }
}

public sealed class GeminiInlineData
{
    [JsonPropertyName("mime_type")]
    public required string MimeType { get; init; }

    [JsonPropertyName("data")]
    public required string Data { get; init; }
}

public sealed class GeminiGenerationConfig
{
    [JsonPropertyName("responseMimeType")]
    public string ResponseMimeType { get; init; } = "application/json";

    [JsonPropertyName("maxOutputTokens")]
    public required int MaxOutputTokens { get; init; }

    [JsonPropertyName("temperature")]
    public required double Temperature { get; init; }
}
