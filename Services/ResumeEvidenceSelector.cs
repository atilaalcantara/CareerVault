using CareerVault.Api.Models;

namespace CareerVault.Api.Services;

public sealed class ResumeEvidenceSelector
{
    private const int MaxSelectedEvidence = 18;
    private const double MaxEvidenceDistance = 0.90d;
    private const double MinEvidenceScore = 0.35d;

    public ResumeEvidenceDto[] Select(
        JobAnalysisDto jobAnalysis,
        IReadOnlyDictionary<string, IReadOnlyList<SemanticSearchResultItem>> resultsByQuery)
    {
        var mustHave = jobAnalysis.MustHaveSkills
            .Concat(jobAnalysis.DomainKeywords)
            .Concat(jobAnalysis.Responsibilities)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToArray();
        var practicalBias = ShouldFavorPracticalExperience(jobAnalysis);

        var merged = new Dictionary<Guid, AggregatedEvidence>(capacity: 64);

        foreach (var (query, results) in resultsByQuery)
        {
            foreach (var result in results)
            {
                if (result.Distance > MaxEvidenceDistance)
                {
                    continue;
                }

                if (!merged.TryGetValue(result.Id, out var existing))
                {
                    merged[result.Id] = new AggregatedEvidence(result);
                    existing = merged[result.Id];
                }

                existing.Queries.Add(query);
                existing.Score += ComputeEvidenceScore(result, mustHave, query, practicalBias);
            }
        }

        return merged
            .Values
            .Where(item => item.Score >= MinEvidenceScore)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Result.Distance)
            .Take(MaxSelectedEvidence)
            .Select(item => new ResumeEvidenceDto
            {
                EntryId = item.Result.Id,
                Title = item.Result.Title,
                Summary = item.Result.Summary,
                Content = item.Result.Content,
                Company = item.Result.Company,
                Project = item.Result.Project,
                Technologies = item.Result.Technologies,
                Tags = item.Result.Tags,
                Distance = item.Result.Distance,
                MatchedQueries = item.Queries.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
                RelevanceReason = BuildReason(item.Result, mustHave, item.Queries)
            })
            .ToArray();
    }

    private static double ComputeEvidenceScore(
        SemanticSearchResultItem result,
        IReadOnlyCollection<string> mustHave,
        string query,
        bool practicalBias)
    {
        var score = 1d - Math.Min(result.Distance, 1d);
        score += CountMatches(result, mustHave) * 0.12d;
        score += CountMatches(result, [query]) * 0.08d;

        if (LooksPractical(result))
        {
            score += 0.10d;
        }

        if (practicalBias && IsStudyLike(result))
        {
            score -= 0.12d;
        }

        if (IsGenericCareerEntry(result))
        {
            score -= 0.18d;
        }

        if (HasConcreteStackMatch(result, mustHave))
        {
            score += 0.12d;
        }

        return score;
    }

    private static int CountMatches(SemanticSearchResultItem result, IReadOnlyCollection<string> terms)
    {
        var haystack = string.Join(
            ' ',
            [
                result.Title,
                result.Summary ?? string.Empty,
                result.Project ?? string.Empty,
                string.Join(' ', result.Technologies),
                string.Join(' ', result.Tags)
            ]);

        return terms.Count(term =>
            !string.IsNullOrWhiteSpace(term)
            && haystack.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildReason(
        SemanticSearchResultItem result,
        IReadOnlyCollection<string> mustHave,
        IReadOnlyCollection<string> queries)
    {
        var matchedKeywords = mustHave
            .Where(term =>
                !string.IsNullOrWhiteSpace(term)
                && (result.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || (result.Summary?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (result.Project?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                    || result.Technologies.Any(technology => technology.Contains(term, StringComparison.OrdinalIgnoreCase))
                    || result.Tags.Any(tag => tag.Contains(term, StringComparison.OrdinalIgnoreCase))))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToArray();

        if (matchedKeywords.Length == 0)
        {
            return $"Recuperada pelas queries: {string.Join(", ", queries.Take(3))}.";
        }

        return $"Aderencia observada em: {string.Join(", ", matchedKeywords)}.";
    }

    private static bool ShouldFavorPracticalExperience(JobAnalysisDto jobAnalysis)
    {
        var responsibilities = string.Join(' ', jobAnalysis.Responsibilities);
        return responsibilities.Contains("Entregar", StringComparison.OrdinalIgnoreCase)
            || responsibilities.Contains("Participar de code reviews", StringComparison.OrdinalIgnoreCase)
            || responsibilities.Contains("Escrever código", StringComparison.OrdinalIgnoreCase)
            || jobAnalysis.MustHaveSkills.Length >= 4;
    }

    private static bool LooksPractical(SemanticSearchResultItem result)
    {
        var signals = string.Join(
            ' ',
            [
                result.Title,
                result.Summary ?? string.Empty,
                result.Project ?? string.Empty,
                string.Join(' ', result.Tags)
            ]);

        return signals.Contains("backend", StringComparison.OrdinalIgnoreCase)
            || signals.Contains("bugfix", StringComparison.OrdinalIgnoreCase)
            || signals.Contains("infra", StringComparison.OrdinalIgnoreCase)
            || signals.Contains("api", StringComparison.OrdinalIgnoreCase)
            || signals.Contains("projeto", StringComparison.OrdinalIgnoreCase)
            || signals.Contains("integra", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStudyLike(SemanticSearchResultItem result)
    {
        var signals = string.Join(
            ' ',
            [
                result.Title,
                result.Summary ?? string.Empty,
                result.Project ?? string.Empty,
                string.Join(' ', result.Tags)
            ]);

        return signals.Contains("estudo", StringComparison.OrdinalIgnoreCase)
            || signals.Contains("study", StringComparison.OrdinalIgnoreCase)
            || signals.Contains("certifica", StringComparison.OrdinalIgnoreCase)
            || signals.Contains("formação acadêmica", StringComparison.OrdinalIgnoreCase)
            || signals.Contains("bacharelado", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGenericCareerEntry(SemanticSearchResultItem result)
    {
        var signals = string.Join(
            ' ',
            [
                result.Title,
                result.Summary ?? string.Empty,
                result.Project ?? string.Empty
            ]);

        return signals.Contains("memória profissional", StringComparison.OrdinalIgnoreCase)
            || signals.Contains("inicialização do sistema", StringComparison.OrdinalIgnoreCase)
            || signals.Contains("exploração de memória profissional", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasConcreteStackMatch(SemanticSearchResultItem result, IReadOnlyCollection<string> mustHave)
    {
        var concreteTerms = mustHave
            .Where(term =>
                term.Contains(".NET", StringComparison.OrdinalIgnoreCase)
                || term.Contains("C#", StringComparison.OrdinalIgnoreCase)
                || term.Contains("PostgreSQL", StringComparison.OrdinalIgnoreCase)
                || term.Contains("SQL", StringComparison.OrdinalIgnoreCase)
                || term.Contains("Docker", StringComparison.OrdinalIgnoreCase)
                || term.Contains("Git", StringComparison.OrdinalIgnoreCase)
                || term.Contains("React", StringComparison.OrdinalIgnoreCase)
                || term.Contains("Angular", StringComparison.OrdinalIgnoreCase)
                || term.Contains("Kafka", StringComparison.OrdinalIgnoreCase)
                || term.Contains("RabbitMQ", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (concreteTerms.Length == 0)
        {
            return false;
        }

        return CountMatches(result, concreteTerms) > 0;
    }

    private sealed class AggregatedEvidence(SemanticSearchResultItem result)
    {
        public SemanticSearchResultItem Result { get; } = result;
        public HashSet<string> Queries { get; } = new(StringComparer.OrdinalIgnoreCase);
        public double Score { get; set; }
    }
}
