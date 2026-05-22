using CareerVault.Api.Models;
using CareerVault.Api.Options;
using Microsoft.Extensions.Options;

namespace CareerVault.Api.Services;

public sealed class SemanticSearchService(
    IEmbeddingProvider embeddingProvider,
    CareerVaultRepository repository,
    IOptions<LocalEmbeddingsOptions> options)
{
    private const int CandidateMultiplier = 5;

    public async Task<IReadOnlyList<SemanticSearchResultItem>> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new InvalidOperationException("A query da busca semantica nao pode ser vazia.");
        }

        var normalizedLimit = Math.Clamp(limit, 1, 50);
        var embedding = await embeddingProvider.GenerateAsync(query, cancellationToken);
        if (embedding.Length != options.Value.Dimensions)
        {
            throw new InvalidOperationException(
                $"Embedding da busca retornou {embedding.Length} dimensoes, esperado: {options.Value.Dimensions}.");
        }

        var candidateLimit = Math.Clamp(normalizedLimit * CandidateMultiplier, normalizedLimit, 100);
        var vectorCandidates = await repository.SearchVectorCandidatesAsync(embedding, candidateLimit, cancellationToken);
        var textCandidates = await repository.SearchTextCandidatesAsync(query, embedding, candidateLimit, cancellationToken);

        return MergeCandidates(vectorCandidates, textCandidates, normalizedLimit);
    }

    private static IReadOnlyList<SemanticSearchResultItem> MergeCandidates(
        IReadOnlyList<SemanticSearchCandidate> vectorCandidates,
        IReadOnlyList<SemanticSearchCandidate> textCandidates,
        int limit)
    {
        var maxDistance = vectorCandidates.Count == 0
            ? 1d
            : Math.Max(vectorCandidates.Max(candidate => candidate.Result.Distance), 0.000001d);
        var maxTextScore = textCandidates.Count == 0
            ? 1d
            : Math.Max(textCandidates.Max(candidate => candidate.TextScore ?? 0d), 0.000001d);

        var merged = new Dictionary<Guid, RankedCandidate>();

        foreach (var candidate in vectorCandidates)
        {
            var semanticScore = 1d - Math.Min(candidate.Result.Distance / maxDistance, 1d);
            merged[candidate.Result.Id] = new RankedCandidate(
                candidate.Result,
                semanticScore,
                0d);
        }

        foreach (var candidate in textCandidates)
        {
            var textScore = Math.Min((candidate.TextScore ?? 0d) / maxTextScore, 1d);
            if (merged.TryGetValue(candidate.Result.Id, out var existing))
            {
                merged[candidate.Result.Id] = existing with { TextScore = textScore };
                continue;
            }

            var semanticScore = 1d - Math.Min(candidate.Result.Distance / maxDistance, 1d);
            merged[candidate.Result.Id] = new RankedCandidate(
                candidate.Result,
                semanticScore,
                textScore);
        }

        return merged
            .Values
            .OrderByDescending(candidate => candidate.FinalScore)
            .ThenBy(candidate => candidate.Result.Distance)
            .Take(limit)
            .Select(candidate => candidate.Result)
            .ToArray();
    }

    private sealed record RankedCandidate(
        SemanticSearchResultItem Result,
        double SemanticScore,
        double TextScore)
    {
        public double FinalScore => (SemanticScore * 0.7d) + (TextScore * 0.3d);
    }
}
