using System.Text.Json.Serialization;

namespace CareerVault.Api.Models;

public sealed class ResumeEducationItemDto
{
    [JsonPropertyName("institution")]
    public string Institution { get; set; } = string.Empty;

    [JsonPropertyName("degree")]
    public string Degree { get; set; } = string.Empty;

    [JsonPropertyName("details")]
    public string Details { get; set; } = string.Empty;
}
