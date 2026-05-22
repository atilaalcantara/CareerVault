using System.Text.Json.Serialization;

namespace CareerVault.Api.Models;

public sealed class ResumeGenerateRequest
{
    [JsonPropertyName("jobDescription")]
    public string JobDescription { get; set; } = string.Empty;

    [JsonPropertyName("templateId")]
    public string TemplateId { get; set; } = "default-ats";

    [JsonPropertyName("targetLanguage")]
    public string TargetLanguage { get; set; } = "pt-BR";
}
