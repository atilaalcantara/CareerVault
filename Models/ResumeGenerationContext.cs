namespace CareerVault.Api.Models;

public sealed class ResumeGenerationContext
{
    public required ResumeProfileDto Profile { get; init; }
    public required string JobDescription { get; init; }
    public required string TemplateId { get; init; }
    public required string TargetLanguage { get; init; }
    public required JobAnalysisDto JobAnalysis { get; init; }
    public required ResumeEvidenceDto[] Evidence { get; init; }
}
