using System.Text.Json.Serialization;

namespace CareerVault.Api.Models;

public sealed class ResumeSimpleItemDto
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("details")]
    public string Details { get; set; } = string.Empty;
}
