using System.Text.Json.Serialization;

namespace CareerVault.Api.Models;

public sealed class GeminiGenerateContentResponse
{
    [JsonPropertyName("candidates")]
    public List<GeminiCandidate> Candidates { get; init; } = [];
}

public sealed class GeminiCandidate
{
    [JsonPropertyName("content")]
    public GeminiResponseContent? Content { get; init; }
}

public sealed class GeminiResponseContent
{
    [JsonPropertyName("parts")]
    public List<GeminiResponsePart> Parts { get; init; } = [];
}

public sealed class GeminiResponsePart
{
    [JsonPropertyName("text")]
    public string? Text { get; init; }
}
