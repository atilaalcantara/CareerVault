namespace CareerVault.Api.Options;

public sealed class LocalEmbeddingsOptions
{
    public const string SectionName = "LocalEmbeddings";

    public string Model { get; init; } = "sentence-transformers/all-MiniLM-L6-v2";
    public int Dimensions { get; init; } = 384;
    public string? CacheDirectory { get; init; }
}
