using CareerVault.Api.Models;

namespace CareerVault.Api.Services;

public interface IAiContentService
{
    Task<GeminiStructuredPayloadResult> GenerateStructuredPayloadAsync(
        string? userContext,
        IReadOnlyCollection<GeminiPart> fileParts,
        IngestionTemporalContext temporalContext,
        CancellationToken cancellationToken);

    Task<JobAnalysisDto> AnalyzeJobDescriptionAsync(
        string jobDescription,
        string targetLanguage,
        CancellationToken cancellationToken);

    Task<TailoredResumeDraftDto> GenerateTailoredResumeDraftAsync(
        ResumeGenerationContext context,
        CancellationToken cancellationToken);
}
