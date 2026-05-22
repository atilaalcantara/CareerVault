namespace CareerVault.Api.Services;

public interface IEmbeddingProvider
{
    Task<float[]> GenerateAsync(string text, CancellationToken cancellationToken);
}
