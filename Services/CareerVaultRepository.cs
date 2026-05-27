using System.Data;
using System.Text.Json;
using CareerVault.Api.Data;
using CareerVault.Api.Data.Entities;
using CareerVault.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Pgvector;

namespace CareerVault.Api.Services;

public sealed class CareerVaultRepository(IDbContextFactory<CareerVaultDbContext> dbContextFactory)
{
    public async Task<ProfessionalEntryRecord> CreateAsync(
        ProfessionalEntryCreateRequest request,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = new ProfessionalEntry
        {
            SourceType = request.Source.SourceType,
            SourceExternalId = request.Source.SourceExternalId,
            Title = request.StructuredEntry.Title,
            Content = request.StructuredEntry.Content,
            Summary = request.StructuredEntry.Summary,
            Company = request.StructuredEntry.Company,
            Project = request.StructuredEntry.Project,
            Role = request.StructuredEntry.Role,
            OccurredAt = request.StructuredEntry.OccurredAt,
            Technologies = request.StructuredEntry.Technologies,
            Tags = request.StructuredEntry.Tags,
            RawPayload = JsonDocument.Parse(request.RawPayload.GetRawText()),
            ContentHash = request.ContentHash,
            EmbeddingStatus = "pending",
            EmbeddingModel = request.EmbeddingModel,
            EmbeddingDimensions = request.EmbeddingDimensions,
            NotionSyncStatus = request.NotionSyncStatus,
            NotionPageId = request.NotionPageId,
            NotionLastError = request.NotionLastError,
            NotionSyncedAt = request.NotionSyncedAt
        };

        dbContext.ProfessionalEntries.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapEntry(entity);
    }

    public async Task<bool> ExistsByContentHashAsync(string contentHash, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.ProfessionalEntries
            .AsNoTracking()
            .AnyAsync(entry => entry.ContentHash == contentHash, cancellationToken);
    }

    public async Task<int> MarkEmbeddingsStaleAsync(
        string? model,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.ProfessionalEntries
            .Where(entry => entry.EmbeddingStatus == "completed"
                && (model == null || entry.EmbeddingModel == model))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(entry => entry.EmbeddingStatus, "stale")
                .SetProperty(entry => entry.EmbeddingError, (string?)null)
                .SetProperty(entry => entry.UpdatedAt, DateTimeOffset.UtcNow),
                cancellationToken);
    }

    public async Task UpdateNotionSyncAsync(
        Guid entryId,
        bool success,
        string? pageId,
        string? error,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        await dbContext.ProfessionalEntries
            .Where(entry => entry.Id == entryId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(entry => entry.NotionSyncStatus, success ? "completed" : "failed")
                .SetProperty(entry => entry.NotionPageId, pageId)
                .SetProperty(entry => entry.NotionLastError, error)
                .SetProperty(entry => entry.NotionSyncedAt, success ? DateTimeOffset.UtcNow : (DateTimeOffset?)null)
                .SetProperty(entry => entry.UpdatedAt, DateTimeOffset.UtcNow),
                cancellationToken);
    }

    public async Task<IReadOnlyList<ProfessionalEntryEmbeddingJob>> ClaimPendingEmbeddingJobsAsync(
        int batchSize,
        TimeSpan failedRetryDelay,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH candidates AS (
                SELECT id
                FROM career_vault.professional_entries
                WHERE embedding_status IN ('pending', 'stale')
                   OR (
                        embedding_status = 'failed'
                        AND updated_at <= now() - @failed_retry_delay
                   )
                ORDER BY created_at
                LIMIT @batch_size
                FOR UPDATE SKIP LOCKED
            )
            UPDATE career_vault.professional_entries AS entries
            SET
                embedding_status = 'processing',
                embedding_error = NULL,
                updated_at = now()
            FROM candidates
            WHERE entries.id = candidates.id
            RETURNING
                entries.id,
                entries.title,
                entries.content,
                entries.summary,
                entries.company,
                entries.project,
                entries.role,
                entries.technologies,
                entries.tags,
                entries.content_hash,
                entries.embedding_status;
            """;

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
        await using var command = new NpgsqlCommand(sql, connection, (NpgsqlTransaction)transaction.GetDbTransaction());
        command.Parameters.AddWithValue("batch_size", batchSize);
        command.Parameters.AddWithValue("failed_retry_delay", failedRetryDelay);

        var jobs = new List<ProfessionalEntryEmbeddingJob>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            jobs.Add(new ProfessionalEntryEmbeddingJob
            {
                Id = reader.GetGuid(0),
                Title = reader.GetString(1),
                Content = reader.GetString(2),
                Summary = reader.IsDBNull(3) ? null : reader.GetString(3),
                Company = reader.IsDBNull(4) ? null : reader.GetString(4),
                Project = reader.IsDBNull(5) ? null : reader.GetString(5),
                Role = reader.IsDBNull(6) ? null : reader.GetString(6),
                Technologies = reader.IsDBNull(7) ? [] : reader.GetFieldValue<string[]>(7),
                Tags = reader.IsDBNull(8) ? [] : reader.GetFieldValue<string[]>(8),
                ContentHash = reader.GetString(9),
                EmbeddingStatus = reader.GetString(10)
            });
        }

        await reader.CloseAsync();
        await transaction.CommitAsync(cancellationToken);
        return jobs;
    }

    public async Task SaveEmbeddingAsync(
        Guid entryId,
        string model,
        int dimensions,
        string contentHash,
        float[] embedding,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var entry = await dbContext.ProfessionalEntries
            .SingleOrDefaultAsync(current => current.Id == entryId, cancellationToken)
            ?? throw new InvalidOperationException($"Entry {entryId} nao encontrada ao salvar embedding.");

        var existingEmbedding = await dbContext.ProfessionalEntryEmbeddings
            .SingleOrDefaultAsync(current => current.EntryId == entryId && current.Model == model, cancellationToken);

        if (existingEmbedding is null)
        {
            dbContext.ProfessionalEntryEmbeddings.Add(new ProfessionalEntryEmbedding
            {
                Id = Guid.NewGuid(),
                EntryId = entryId,
                Model = model,
                Dimensions = dimensions,
                Embedding = new Vector(embedding),
                ContentHash = contentHash
            });
        }
        else
        {
            existingEmbedding.Dimensions = dimensions;
            existingEmbedding.Embedding = new Vector(embedding);
            existingEmbedding.ContentHash = contentHash;
            existingEmbedding.CreatedAt = DateTimeOffset.UtcNow;
        }

        entry.ContentHash = contentHash;
        entry.EmbeddingStatus = "completed";
        entry.EmbeddingModel = model;
        entry.EmbeddingDimensions = dimensions;
        entry.EmbeddingUpdatedAt = DateTimeOffset.UtcNow;
        entry.EmbeddingError = null;

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task MarkEmbeddingFailedAsync(
        Guid entryId,
        string error,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        await dbContext.ProfessionalEntries
            .Where(entry => entry.Id == entryId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(entry => entry.EmbeddingStatus, "failed")
                .SetProperty(entry => entry.EmbeddingError, error)
                .SetProperty(entry => entry.UpdatedAt, DateTimeOffset.UtcNow),
                cancellationToken);
    }

    public async Task<IReadOnlyList<SemanticSearchResultItem>> SearchSemanticAsync(
        float[] queryEmbedding,
        int limit,
        CancellationToken cancellationToken)
    {
        var candidates = await SearchVectorCandidatesAsync(queryEmbedding, limit, cancellationToken);
        return candidates.Select(candidate => candidate.Result).ToArray();
    }

    public async Task<IReadOnlyList<SemanticSearchCandidate>> SearchVectorCandidatesAsync(
        float[] queryEmbedding,
        int limit,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                entries.id,
                entries.title,
                entries.summary,
                entries.content,
                entries.company,
                entries.project,
                entries.technologies,
                entries.tags,
                embeddings.embedding <=> @query_embedding AS distance
            FROM career_vault.professional_entries AS entries
            INNER JOIN career_vault.professional_entry_embeddings AS embeddings
                ON embeddings.entry_id = entries.id
            WHERE entries.embedding_status = 'completed'
            ORDER BY embeddings.embedding <=> @query_embedding
            LIMIT @limit;
            """;

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.Database.OpenConnectionAsync(cancellationToken);

        var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("query_embedding", new Vector(queryEmbedding));
        command.Parameters.AddWithValue("limit", limit);

        var results = new List<SemanticSearchCandidate>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new SemanticSearchCandidate
            {
                Result = ReadSemanticSearchResult(reader),
                TextScore = null
            });
        }

        return results;
    }

    public async Task<IReadOnlyList<SemanticSearchCandidate>> SearchTextCandidatesAsync(
        string query,
        float[] queryEmbedding,
        int limit,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH source AS (
                SELECT
                    entries.id,
                    entries.title,
                    entries.summary,
                    entries.content,
                    entries.company,
                    entries.project,
                    entries.technologies,
                    entries.tags,
                    embeddings.embedding <=> @query_embedding AS distance,
                    (
                        setweight(to_tsvector('simple', coalesce(entries.title, '')), 'A') ||
                        setweight(to_tsvector('simple', coalesce(entries.summary, '')), 'A') ||
                        setweight(to_tsvector('simple', coalesce(entries.project, '')), 'B') ||
                        setweight(to_tsvector('simple', coalesce(entries.company, '')), 'B') ||
                        setweight(to_tsvector('simple', array_to_string(coalesce(entries.technologies, ARRAY[]::text[]), ' ')), 'A') ||
                        setweight(to_tsvector('simple', array_to_string(coalesce(entries.tags, ARRAY[]::text[]), ' ')), 'B') ||
                        setweight(to_tsvector('simple', coalesce(entries.content, '')), 'C')
                    ) AS search_document
                FROM career_vault.professional_entries AS entries
                INNER JOIN career_vault.professional_entry_embeddings AS embeddings
                    ON embeddings.entry_id = entries.id
                WHERE entries.embedding_status = 'completed'
            )
            SELECT
                id,
                title,
                summary,
                content,
                company,
                project,
                technologies,
                tags,
                distance,
                (
                    ts_rank_cd(search_document, websearch_to_tsquery('simple', @query)) * 0.45
                    + GREATEST(
                        similarity(coalesce(title, ''), @query),
                        similarity(coalesce(summary, ''), @query),
                        similarity(coalesce(project, ''), @query)
                    ) * 0.40
                    + GREATEST(
                        similarity(array_to_string(coalesce(technologies, ARRAY[]::text[]), ' '), @query),
                        similarity(array_to_string(coalesce(tags, ARRAY[]::text[]), ' '), @query),
                        similarity(coalesce(company, ''), @query)
                    ) * 0.10
                    + similarity(coalesce(content, ''), @query) * 0.05
                    + CASE
                        WHEN coalesce(title, '') ILIKE '%' || @query || '%' THEN 0.20
                        WHEN coalesce(summary, '') ILIKE '%' || @query || '%' THEN 0.12
                        WHEN coalesce(project, '') ILIKE '%' || @query || '%' THEN 0.10
                        ELSE 0
                      END
                ) AS text_score
            FROM source
            WHERE
                search_document @@ websearch_to_tsquery('simple', @query)
                OR coalesce(title, '') ILIKE '%' || @query || '%'
                OR coalesce(summary, '') ILIKE '%' || @query || '%'
                OR coalesce(project, '') ILIKE '%' || @query || '%'
                OR similarity(coalesce(title, ''), @query) >= 0.16
                OR similarity(coalesce(summary, ''), @query) >= 0.14
                OR similarity(coalesce(project, ''), @query) >= 0.14
                OR similarity(coalesce(company, ''), @query) >= 0.16
                OR similarity(array_to_string(coalesce(technologies, ARRAY[]::text[]), ' '), @query) >= 0.16
                OR similarity(array_to_string(coalesce(tags, ARRAY[]::text[]), ' '), @query) >= 0.16
                OR similarity(coalesce(content, ''), @query) >= 0.14
            ORDER BY text_score DESC, distance ASC
            LIMIT @limit;
            """;

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.Database.OpenConnectionAsync(cancellationToken);

        var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("query", query);
        command.Parameters.AddWithValue("query_embedding", new Vector(queryEmbedding));
        command.Parameters.AddWithValue("limit", limit);

        var results = new List<SemanticSearchCandidate>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new SemanticSearchCandidate
            {
                Result = ReadSemanticSearchResult(reader),
                TextScore = reader.IsDBNull(9) ? null : reader.GetDouble(9)
            });
        }

        return results;
    }

    private static SemanticSearchResultItem ReadSemanticSearchResult(NpgsqlDataReader reader) =>
        new()
        {
            Id = reader.GetGuid(0),
            Title = reader.GetString(1),
            Summary = reader.IsDBNull(2) ? null : reader.GetString(2),
            Content = reader.GetString(3),
            Company = reader.IsDBNull(4) ? null : reader.GetString(4),
            Project = reader.IsDBNull(5) ? null : reader.GetString(5),
            Technologies = reader.IsDBNull(6) ? [] : reader.GetFieldValue<string[]>(6),
            Tags = reader.IsDBNull(7) ? [] : reader.GetFieldValue<string[]>(7),
            Distance = reader.GetDouble(8)
        };

    private static ProfessionalEntryRecord MapEntry(ProfessionalEntry entity) =>
        new()
        {
            Id = entity.Id,
            SourceType = entity.SourceType,
            SourceExternalId = entity.SourceExternalId,
            Title = entity.Title,
            Content = entity.Content,
            Summary = entity.Summary,
            Company = entity.Company,
            Project = entity.Project,
            Role = entity.Role,
            OccurredAt = entity.OccurredAt,
            Technologies = entity.Technologies,
            Tags = entity.Tags,
            RawPayload = entity.RawPayload.RootElement.Clone(),
            ContentHash = entity.ContentHash,
            EmbeddingStatus = entity.EmbeddingStatus,
            EmbeddingModel = entity.EmbeddingModel,
            EmbeddingDimensions = entity.EmbeddingDimensions,
            EmbeddingUpdatedAt = entity.EmbeddingUpdatedAt,
            EmbeddingError = entity.EmbeddingError,
            NotionSyncStatus = entity.NotionSyncStatus,
            NotionPageId = entity.NotionPageId,
            NotionLastError = entity.NotionLastError,
            NotionSyncedAt = entity.NotionSyncedAt,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
}
