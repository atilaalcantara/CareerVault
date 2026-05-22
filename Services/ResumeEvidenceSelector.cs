using CareerVault.Api.Models;

namespace CareerVault.Api.Services;

public sealed class ResumeEvidenceSelector
{
    private const int MaxSelectedEvidence = 18;

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

        var merged = new Dictionary<Guid, AggregatedEvidence>(capacity: 64);

        foreach (var (query, results) in resultsByQuery)
        {
            foreach (var result in results)
            {
                if (!merged.TryGetValue(result.Id, out var existing))
                {
                    merged[result.Id] = new AggregatedEvidence(result);
                    existing = merged[result.Id];
                }

                existing.Queries.Add(query);
                existing.Score += ComputeEvidenceScore(result, mustHave, query);
            }
        }

        return merged
            .Values
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
        string query)
    {
        var score = 1d - Math.Min(result.Distance, 1d);
        score += CountMatches(result, mustHave) * 0.12d;
        score += CountMatches(result, [query]) * 0.08d;
        score += result.Tags.Any(tag => tag.Contains("study", StringComparison.OrdinalIgnoreCase) || tag.Contains("estudo", StringComparison.OrdinalIgnoreCase))
            ? 0d
            : 0.08d;

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

    private sealed class AggregatedEvidence(SemanticSearchResultItem result)
    {
        public SemanticSearchResultItem Result { get; } = result;
        public HashSet<string> Queries { get; } = new(StringComparer.OrdinalIgnoreCase);
        public double Score { get; set; }
    }
}
