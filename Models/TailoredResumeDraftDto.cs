using System.Text.Json.Serialization;

namespace CareerVault.Api.Models;

public sealed class TailoredResumeDraftDto
{
    [JsonPropertyName("headline")]
    public string Headline { get; set; } = string.Empty;

    [JsonPropertyName("professionalSummary")]
    public string ProfessionalSummary { get; set; } = string.Empty;

    [JsonPropertyName("coreSkills")]
    public string[] CoreSkills { get; set; } = [];

    [JsonPropertyName("experienceItems")]
    public ResumeExperienceItemDto[] ExperienceItems { get; set; } = [];

    [JsonPropertyName("educationItems")]
    public ResumeEducationItemDto[] EducationItems { get; set; } = [];

    [JsonPropertyName("certificationItems")]
    public ResumeSimpleItemDto[] CertificationItems { get; set; } = [];

    [JsonPropertyName("projectItems")]
    public ResumeSimpleItemDto[] ProjectItems { get; set; } = [];

    [JsonPropertyName("keywordCoverage")]
    public string[] KeywordCoverage { get; set; } = [];
}
