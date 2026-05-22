namespace CareerVault.Api.Models;

public sealed class ProfessionalEntryEmbeddingJob
{
    public Guid Id { get; init; }
    public required string Title { get; init; }
    public required string Content { get; init; }
    public string? Summary { get; init; }
    public string? Company { get; init; }
    public string? Project { get; init; }
    public string? Role { get; init; }
    public string[] Technologies { get; init; } = [];
    public string[] Tags { get; init; } = [];
    public required string ContentHash { get; init; }
    public required string EmbeddingStatus { get; init; }
}
