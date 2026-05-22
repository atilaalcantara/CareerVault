using System.Text.Json.Serialization;

namespace CareerVault.Api.Models;

public sealed class ResumeGenerationPreviewResponseDto
{
    [JsonPropertyName("jobAnalysis")]
    public required JobAnalysisDto JobAnalysis { get; init; }

    [JsonPropertyName("queriesUsed")]
    public string[] QueriesUsed { get; init; } = [];

    [JsonPropertyName("evidence")]
    public ResumeEvidenceDto[] Evidence { get; init; } = [];

    [JsonPropertyName("draft")]
    public required TailoredResumeDraftDto Draft { get; init; }
}
