using System.Text.RegularExpressions;
using CareerVault.Api.Models;
using Microsoft.Extensions.Logging;

namespace CareerVault.Api.Services;

public sealed partial class ResumeGenerationService(
    IAiContentService aiContentService,
    SemanticSearchService semanticSearchService,
    ResumeEvidenceSelector evidenceSelector,
    ResumeProfileProvider profileProvider,
    IResumePdfRenderer pdfRenderer,
    ILogger<ResumeGenerationService> logger)
{
    private const int ResultsPerQuery = 4;

    public async Task<ResumeGenerationPreviewResponseDto> GeneratePreviewAsync(
        ResumeGenerateRequest request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        logger.LogInformation("Gerando preview de curriculo para template {TemplateId}.", request.TemplateId);

        var profile = profileProvider.GetProfile();
        var jobAnalysis = await aiContentService.AnalyzeJobDescriptionAsync(
            request.JobDescription,
            request.TargetLanguage,
            cancellationToken);

        logger.LogInformation(
            "Analise da vaga concluida. Cargo alvo: {TargetRole}; senioridade: {Seniority}; queries: {QueryCount}",
            jobAnalysis.TargetRole,
            jobAnalysis.Seniority,
            jobAnalysis.SearchQueries.Length);

        var normalizedQueries = jobAnalysis.SearchQueries
            .Where(query => !string.IsNullOrWhiteSpace(query))
            .Select(query => query.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToArray();

        var resultsByQuery = new Dictionary<string, IReadOnlyList<SemanticSearchResultItem>>(StringComparer.OrdinalIgnoreCase);
        foreach (var query in normalizedQueries)
        {
            var results = await semanticSearchService.SearchAsync(query, ResultsPerQuery, cancellationToken);
            resultsByQuery[query] = results;
        }

        logger.LogInformation(
            "Recuperacao de evidencias concluida. Queries executadas: {QueryCount}; evidencias brutas: {RawEvidenceCount}",
            resultsByQuery.Count,
            resultsByQuery.Sum(item => item.Value.Count));

        var evidence = evidenceSelector.Select(jobAnalysis, resultsByQuery);

        logger.LogInformation("Selecao de evidencias concluida. Evidencias finais: {EvidenceCount}", evidence.Length);

        var context = new ResumeGenerationContext
        {
            Profile = profile,
            JobDescription = request.JobDescription,
            TemplateId = request.TemplateId,
            TargetLanguage = request.TargetLanguage,
            JobAnalysis = jobAnalysis,
            Evidence = evidence
        };

        var draft = await aiContentService.GenerateTailoredResumeDraftAsync(context, cancellationToken);

        logger.LogInformation(
            "Draft de curriculo gerado. Skills: {SkillCount}; experiencias: {ExperienceCount}; formacoes: {EducationCount}",
            draft.CoreSkills.Length,
            draft.ExperienceItems.Length,
            draft.EducationItems.Length);

        return new ResumeGenerationPreviewResponseDto
        {
            JobAnalysis = jobAnalysis,
            QueriesUsed = normalizedQueries,
            Evidence = evidence,
            Draft = draft
        };
    }

    public async Task<ResumeGenerationResult> GeneratePdfAsync(
        ResumeGenerateRequest request,
        CancellationToken cancellationToken)
    {
        var preview = await GeneratePreviewAsync(request, cancellationToken);
        var profile = profileProvider.GetProfile();
        var pdfBytes = pdfRenderer.Render(profile, preview.Draft);

        logger.LogInformation("PDF de curriculo gerado com sucesso. Bytes: {PdfLength}", pdfBytes.Length);

        return new ResumeGenerationResult
        {
            Preview = preview,
            PdfBytes = pdfBytes,
            FileName = BuildFileName(preview.JobAnalysis.TargetRole)
        };
    }

    private static void ValidateRequest(ResumeGenerateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.JobDescription))
        {
            throw new InvalidOperationException("A descricao da vaga e obrigatoria.");
        }
    }

    private static string BuildFileName(string targetRole)
    {
        var slug = WhitespaceRegex()
            .Replace(targetRole.ToLowerInvariant(), "-");

        slug = NonSlugRegex().Replace(slug, string.Empty).Trim('-');
        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = "resume";
        }

        return $"resume-{slug}.pdf";
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"[^a-z0-9\-]")]
    private static partial Regex NonSlugRegex();
}
