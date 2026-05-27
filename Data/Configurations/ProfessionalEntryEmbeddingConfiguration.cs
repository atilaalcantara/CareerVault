using CareerVault.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareerVault.Api.Data.Configurations;

public sealed class ProfessionalEntryEmbeddingConfiguration : IEntityTypeConfiguration<ProfessionalEntryEmbedding>
{
    public void Configure(EntityTypeBuilder<ProfessionalEntryEmbedding> builder)
    {
        builder.ToTable("professional_entry_embeddings", CareerVaultDbContext.Schema);

        builder.HasKey(embedding => embedding.Id);

        builder.Property(embedding => embedding.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(embedding => embedding.EntryId)
            .HasColumnName("entry_id");

        builder.Property(embedding => embedding.Model)
            .HasColumnName("model")
            .HasColumnType("text");

        builder.Property(embedding => embedding.Dimensions)
            .HasColumnName("dimensions");

        builder.Property(embedding => embedding.Embedding)
            .HasColumnName("embedding")
            .HasColumnType("vector(384)");

        builder.Property(embedding => embedding.ContentHash)
            .HasColumnName("content_hash")
            .HasColumnType("text");

        builder.Property(embedding => embedding.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        builder.HasIndex(embedding => new { embedding.EntryId, embedding.Model })
            .IsUnique()
            .HasDatabaseName("uq_professional_entry_embedding_entry_model");

        builder.HasIndex(embedding => embedding.Embedding)
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops")
            .HasDatabaseName("ix_professional_entry_embeddings_embedding_hnsw");
    }
}
