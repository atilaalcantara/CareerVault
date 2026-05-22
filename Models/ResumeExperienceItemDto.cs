using System.Text.Json.Serialization;

namespace CareerVault.Api.Models;

public sealed class ResumeExperienceItemDto
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("company")]
    public string Company { get; set; } = string.Empty;

    [JsonPropertyName("project")]
    public string Project { get; set; } = string.Empty;

    [JsonPropertyName("period")]
    public string Period { get; set; } = string.Empty;

    [JsonPropertyName("bullets")]
    public string[] Bullets { get; set; } = [];
}
