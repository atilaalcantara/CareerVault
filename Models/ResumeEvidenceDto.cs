using System.Text.Json.Serialization;

namespace CareerVault.Api.Models;

public sealed class ResumeEvidenceDto
{
    [JsonPropertyName("entryId")]
    public Guid EntryId { get; init; }

    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("summary")]
    public string? Summary { get; init; }

    [JsonPropertyName("content")]
    public required string Content { get; init; }

    [JsonPropertyName("company")]
    public string? Company { get; init; }

    [JsonPropertyName("project")]
    public string? Project { get; init; }

    [JsonPropertyName("technologies")]
    public string[] Technologies { get; init; } = [];

    [JsonPropertyName("tags")]
    public string[] Tags { get; init; } = [];

    [JsonPropertyName("distance")]
    public double Distance { get; init; }

    [JsonPropertyName("matchedQueries")]
    public string[] MatchedQueries { get; init; } = [];

    [JsonPropertyName("relevanceReason")]
    public string RelevanceReason { get; init; } = string.Empty;
}
