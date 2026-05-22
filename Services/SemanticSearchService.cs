using CareerVault.Api.Models;
using CareerVault.Api.Options;
using Microsoft.Extensions.Options;

namespace CareerVault.Api.Services;

public sealed class SemanticSearchService(
    IEmbeddingProvider embeddingProvider,
    CareerVaultRepository repository,
    IOptions<LocalEmbeddingsOptions> options)
{
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

        return await repository.SearchSemanticAsync(embedding, normalizedLimit, cancellationToken);
    }
}
