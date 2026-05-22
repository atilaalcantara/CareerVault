using System.Text.Json.Serialization;

namespace CareerVault.Api.Models;

public sealed class ProfessionalEntryStructuredDto
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("company")]
    public string? Company { get; set; }

    [JsonPropertyName("project")]
    public string? Project { get; set; }

    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("occurredAt")]
    public DateTimeOffset? OccurredAt { get; set; }

    [JsonPropertyName("technologies")]
    public string[] Technologies { get; set; } = [];

    [JsonPropertyName("tags")]
    public string[] Tags { get; set; } = [];
}
