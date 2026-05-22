namespace CareerVault.Api.Models;

public sealed class SemanticSearchCandidate
{
    public required SemanticSearchResultItem Result { get; init; }
    public double? TextScore { get; init; }
}
