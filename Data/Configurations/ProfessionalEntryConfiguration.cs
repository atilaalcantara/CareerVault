using CareerVault.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareerVault.Api.Data.Configurations;

public sealed class ProfessionalEntryConfiguration : IEntityTypeConfiguration<ProfessionalEntry>
{
    public void Configure(EntityTypeBuilder<ProfessionalEntry> builder)
    {
        builder.ToTable("professional_entries", CareerVaultDbContext.Schema);

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(entry => entry.SourceType)
            .HasColumnName("source_type")
            .HasColumnType("text");

        builder.Property(entry => entry.SourceExternalId)
            .HasColumnName("source_external_id")
            .HasColumnType("text");

        builder.Property(entry => entry.Title)
            .HasColumnName("title")
            .HasColumnType("text");

        builder.Property(entry => entry.Content)
            .HasColumnName("content")
            .HasColumnType("text");

        builder.Property(entry => entry.Summary)
            .HasColumnName("summary")
            .HasColumnType("text");

        builder.Property(entry => entry.Company)
            .HasColumnName("company")
            .HasColumnType("text");

        builder.Property(entry => entry.Project)
            .HasColumnName("project")
            .HasColumnType("text");

        builder.Property(entry => entry.Role)
            .HasColumnName("role")
            .HasColumnType("text");

        builder.Property(entry => entry.OccurredAt)
            .HasColumnName("occurred_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(entry => entry.Technologies)
            .HasColumnName("technologies")
            .HasColumnType("text[]");

        builder.Property(entry => entry.Tags)
            .HasColumnName("tags")
            .HasColumnType("text[]");

        builder.Property(entry => entry.RawPayload)
            .HasColumnName("raw_payload")
            .HasColumnType("jsonb");

        builder.Property(entry => entry.ContentHash)
            .HasColumnName("content_hash")
            .HasColumnType("text");

        builder.Property(entry => entry.EmbeddingStatus)
            .HasColumnName("embedding_status")
            .HasColumnType("text");

        builder.Property(entry => entry.EmbeddingModel)
            .HasColumnName("embedding_model")
            .HasColumnType("text");

        builder.Property(entry => entry.EmbeddingDimensions)
            .HasColumnName("embedding_dimensions");

        builder.Property(entry => entry.EmbeddingUpdatedAt)
            .HasColumnName("embedding_updated_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(entry => entry.EmbeddingError)
            .HasColumnName("embedding_error")
            .HasColumnType("text");

        builder.Property(entry => entry.NotionSyncStatus)
            .HasColumnName("notion_sync_status")
            .HasColumnType("text");

        builder.Property(entry => entry.NotionPageId)
            .HasColumnName("notion_page_id")
            .HasColumnType("text");

        builder.Property(entry => entry.NotionLastError)
            .HasColumnName("notion_last_error")
            .HasColumnType("text");

        builder.Property(entry => entry.NotionSyncedAt)
            .HasColumnName("notion_synced_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(entry => entry.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        builder.Property(entry => entry.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        builder.HasIndex(entry => entry.ContentHash)
            .HasDatabaseName("ix_professional_entries_content_hash");

        builder.HasIndex(entry => new { entry.EmbeddingStatus, entry.CreatedAt })
            .HasDatabaseName("ix_professional_entries_embedding_status_created_at");

        builder.HasMany(entry => entry.Embeddings)
            .WithOne(embedding => embedding.Entry)
            .HasForeignKey(embedding => embedding.EntryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
