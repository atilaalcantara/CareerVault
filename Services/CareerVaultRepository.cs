using System.Text.Json;
using CareerVault.Api.Models;
using Npgsql;
using Pgvector;

namespace CareerVault.Api.Services;

public sealed class CareerVaultRepository(NpgsqlDataSource dataSource)
{
    public async Task<ProfessionalEntryRecord> CreateAsync(
        ProfessionalEntryCreateRequest request,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO career_vault.professional_entries
            (
                source_type,
                source_external_id,
                title,
                content,
                summary,
                company,
                project,
                role,
                occurred_at,
                technologies,
                tags,
                raw_payload,
                content_hash,
                embedding_status,
                embedding_model,
                embedding_dimensions,
                notion_sync_status,
                notion_page_id,
                notion_last_error,
                notion_synced_at
            )
            VALUES
            (
                @source_type,
                @source_external_id,
                @title,
                @content,
                @summary,
                @company,
                @project,
                @role,
                @occurred_at,
                @technologies,
                @tags,
                CAST(@raw_payload AS jsonb),
                @content_hash,
                'pending',
                @embedding_model,
                @embedding_dimensions,
                @notion_sync_status,
                @notion_page_id,
                @notion_last_error,
                @notion_synced_at
            )
            RETURNING
                id,
                source_type,
                source_external_id,
                title,
                content,
                summary,
                company,
                project,
                role,
                occurred_at,
                technologies,
                tags,
                raw_payload,
                content_hash,
                embedding_status,
                embedding_model,
                embedding_dimensions,
                embedding_updated_at,
                embedding_error,
                notion_sync_status,
                notion_page_id,
                notion_last_error,
                notion_synced_at,
                created_at,
                updated_at;
            """;

        await using var command = dataSource.CreateCommand(sql);
        AddCommonEntryParameters(command, request);
        command.Parameters.AddWithValue("raw_payload", request.RawPayload.GetRawText());
        command.Parameters.AddWithValue("notion_sync_status", request.NotionSyncStatus);
        command.Parameters.AddWithValue("notion_page_id", (object?)request.NotionPageId ?? DBNull.Value);
        command.Parameters.AddWithValue("notion_last_error", (object?)request.NotionLastError ?? DBNull.Value);
        command.Parameters.AddWithValue("notion_synced_at", request.NotionSyncedAt?.UtcDateTime ?? (object)DBNull.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Nao foi possivel inserir a entrada profissional no PostgreSQL.");
        }

        return ReadEntry(reader);
    }

    public async Task<bool> ExistsByContentHashAsync(string contentHash, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM career_vault.professional_entries
                WHERE content_hash = @content_hash
            );
            """;

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("content_hash", contentHash);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is true || result is bool boolResult && boolResult;
    }

    public async Task<int> MarkEmbeddingsStaleAsync(
        string? model,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH updated AS (
                UPDATE career_vault.professional_entries
                SET
                    embedding_status = 'stale',
                    embedding_error = NULL
                WHERE embedding_status = 'completed'
                  AND (
                        @model IS NULL
                        OR embedding_model = @model
                      )
                RETURNING 1
            )
            SELECT COUNT(*)
            FROM updated;
            """;

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("model", (object?)model ?? DBNull.Value);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is int count
            ? count
            : Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task UpdateNotionSyncAsync(
        Guid entryId,
        bool success,
        string? pageId,
        string? error,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE career_vault.professional_entries
            SET
                notion_sync_status = @notion_sync_status,
                notion_page_id = @notion_page_id,
                notion_last_error = @notion_last_error,
                notion_synced_at = @notion_synced_at
            WHERE id = @id;
            """;

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("id", entryId);
        command.Parameters.AddWithValue("notion_sync_status", success ? "completed" : "failed");
        command.Parameters.AddWithValue("notion_page_id", (object?)pageId ?? DBNull.Value);
        command.Parameters.AddWithValue("notion_last_error", (object?)error ?? DBNull.Value);
        command.Parameters.AddWithValue("notion_synced_at", success ? DateTimeOffset.UtcNow : DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
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
                embedding_error = NULL
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

        await using var command = dataSource.CreateCommand(sql);
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
        const string sql = """
            INSERT INTO career_vault.professional_entry_embeddings
            (
                entry_id,
                model,
                dimensions,
                embedding,
                content_hash
            )
            VALUES
            (
                @entry_id,
                @model,
                @dimensions,
                @embedding,
                @content_hash
            )
            ON CONFLICT (entry_id, model) DO UPDATE
            SET
                dimensions = EXCLUDED.dimensions,
                embedding = EXCLUDED.embedding,
                content_hash = EXCLUDED.content_hash,
                created_at = now();

            UPDATE career_vault.professional_entries
            SET
                content_hash = @content_hash,
                embedding_status = 'completed',
                embedding_model = @model,
                embedding_dimensions = @dimensions,
                embedding_updated_at = now(),
                embedding_error = NULL
            WHERE id = @entry_id;
            """;

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("entry_id", entryId);
        command.Parameters.AddWithValue("model", model);
        command.Parameters.AddWithValue("dimensions", dimensions);
        command.Parameters.AddWithValue("content_hash", contentHash);
        command.Parameters.AddWithValue("embedding", new Vector(embedding));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task MarkEmbeddingFailedAsync(
        Guid entryId,
        string error,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE career_vault.professional_entries
            SET
                embedding_status = 'failed',
                embedding_error = @embedding_error
            WHERE id = @id;
            """;

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("id", entryId);
        command.Parameters.AddWithValue("embedding_error", error);
        await command.ExecuteNonQueryAsync(cancellationToken);
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

        await using var command = dataSource.CreateCommand(sql);
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
                    ts_rank_cd(search_document, websearch_to_tsquery('simple', @query)) * 0.75
                    + GREATEST(
                        similarity(coalesce(title, ''), @query),
                        similarity(coalesce(summary, ''), @query),
                        similarity(coalesce(project, ''), @query),
                        similarity(coalesce(company, ''), @query),
                        similarity(array_to_string(coalesce(technologies, ARRAY[]::text[]), ' '), @query),
                        similarity(array_to_string(coalesce(tags, ARRAY[]::text[]), ' '), @query),
                        similarity(coalesce(content, ''), @query)
                    ) * 0.25
                ) AS text_score
            FROM source
            WHERE
                search_document @@ websearch_to_tsquery('simple', @query)
                OR similarity(coalesce(title, ''), @query) >= 0.12
                OR similarity(coalesce(summary, ''), @query) >= 0.12
                OR similarity(coalesce(project, ''), @query) >= 0.12
                OR similarity(coalesce(company, ''), @query) >= 0.12
                OR similarity(array_to_string(coalesce(technologies, ARRAY[]::text[]), ' '), @query) >= 0.12
                OR similarity(array_to_string(coalesce(tags, ARRAY[]::text[]), ' '), @query) >= 0.12
                OR similarity(coalesce(content, ''), @query) >= 0.10
            ORDER BY text_score DESC, distance ASC
            LIMIT @limit;
            """;

        await using var command = dataSource.CreateCommand(sql);
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

    private static void AddCommonEntryParameters(
        NpgsqlCommand command,
        ProfessionalEntryCreateRequest request)
    {
        command.Parameters.AddWithValue("source_type", request.Source.SourceType);
        command.Parameters.AddWithValue("source_external_id", (object?)request.Source.SourceExternalId ?? DBNull.Value);
        command.Parameters.AddWithValue("title", request.StructuredEntry.Title);
        command.Parameters.AddWithValue("content", request.StructuredEntry.Content);
        command.Parameters.AddWithValue("summary", (object?)request.StructuredEntry.Summary ?? DBNull.Value);
        command.Parameters.AddWithValue("company", (object?)request.StructuredEntry.Company ?? DBNull.Value);
        command.Parameters.AddWithValue("project", (object?)request.StructuredEntry.Project ?? DBNull.Value);
        command.Parameters.AddWithValue("role", (object?)request.StructuredEntry.Role ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "occurred_at",
            request.StructuredEntry.OccurredAt is { } occurredAt
                ? occurredAt.UtcDateTime
                : DBNull.Value);
        command.Parameters.AddWithValue("technologies", request.StructuredEntry.Technologies);
        command.Parameters.AddWithValue("tags", request.StructuredEntry.Tags);
        command.Parameters.AddWithValue("content_hash", request.ContentHash);
        command.Parameters.AddWithValue("embedding_model", request.EmbeddingModel);
        command.Parameters.AddWithValue("embedding_dimensions", request.EmbeddingDimensions);
    }

    private static ProfessionalEntryRecord ReadEntry(NpgsqlDataReader record)
    {
        var rawPayload = JsonDocument.Parse(record.GetString(12)).RootElement.Clone();

        return new ProfessionalEntryRecord
        {
            Id = record.GetGuid(0),
            SourceType = record.GetString(1),
            SourceExternalId = record.IsDBNull(2) ? null : record.GetString(2),
            Title = record.GetString(3),
            Content = record.GetString(4),
            Summary = record.IsDBNull(5) ? null : record.GetString(5),
            Company = record.IsDBNull(6) ? null : record.GetString(6),
            Project = record.IsDBNull(7) ? null : record.GetString(7),
            Role = record.IsDBNull(8) ? null : record.GetString(8),
            OccurredAt = record.IsDBNull(9) ? null : record.GetFieldValue<DateTimeOffset>(9),
            Technologies = record.IsDBNull(10) ? [] : record.GetFieldValue<string[]>(10),
            Tags = record.IsDBNull(11) ? [] : record.GetFieldValue<string[]>(11),
            RawPayload = rawPayload,
            ContentHash = record.GetString(13),
            EmbeddingStatus = record.GetString(14),
            EmbeddingModel = record.IsDBNull(15) ? null : record.GetString(15),
            EmbeddingDimensions = record.IsDBNull(16) ? null : record.GetInt32(16),
            EmbeddingUpdatedAt = record.IsDBNull(17) ? null : record.GetFieldValue<DateTimeOffset>(17),
            EmbeddingError = record.IsDBNull(18) ? null : record.GetString(18),
            NotionSyncStatus = record.GetString(19),
            NotionPageId = record.IsDBNull(20) ? null : record.GetString(20),
            NotionLastError = record.IsDBNull(21) ? null : record.GetString(21),
            NotionSyncedAt = record.IsDBNull(22) ? null : record.GetFieldValue<DateTimeOffset>(22),
            CreatedAt = record.GetFieldValue<DateTimeOffset>(23),
            UpdatedAt = record.GetFieldValue<DateTimeOffset>(24)
        };
    }
}
