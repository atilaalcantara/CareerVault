using Pgvector;

namespace CareerVault.Api.Data.Entities;

public sealed class ProfessionalEntryEmbedding
{
    public Guid Id { get; set; }
    public Guid EntryId { get; set; }
    public required string Model { get; set; }
    public int Dimensions { get; set; }
    public required Vector Embedding { get; set; }
    public required string ContentHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ProfessionalEntry Entry { get; set; } = null!;
}
