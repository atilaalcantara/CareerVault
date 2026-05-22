using System.Text.Json.Serialization;

namespace CareerVault.Api.Models;

public sealed class JobAnalysisDto
{
    [JsonPropertyName("targetRole")]
    public string TargetRole { get; set; } = string.Empty;

    [JsonPropertyName("seniority")]
    public string Seniority { get; set; } = string.Empty;

    [JsonPropertyName("mustHaveSkills")]
    public string[] MustHaveSkills { get; set; } = [];

    [JsonPropertyName("niceToHaveSkills")]
    public string[] NiceToHaveSkills { get; set; } = [];

    [JsonPropertyName("domainKeywords")]
    public string[] DomainKeywords { get; set; } = [];

    [JsonPropertyName("responsibilities")]
    public string[] Responsibilities { get; set; } = [];

    [JsonPropertyName("searchQueries")]
    public string[] SearchQueries { get; set; } = [];
}
