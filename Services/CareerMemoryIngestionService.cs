using CareerVault.Api.Models;
using CareerVault.Api.Options;
using Microsoft.Extensions.Options;

namespace CareerVault.Api.Services;

public sealed class CareerMemoryIngestionService(
    FilePayloadBuilder filePayloadBuilder,
    IAiContentService aiContentService,
    CareerVaultRepository repository,
    NotionService notionService,
    IOptions<LocalEmbeddingsOptions> localEmbeddingsOptions,
    IOptions<GeminiOptions> geminiOptions,
    ILogger<CareerMemoryIngestionService> logger)
{
    public async Task<IngestionResponse> IngestFormFilesAsync(
        string? context,
        IReadOnlyCollection<IFormFile> files,
        CancellationToken cancellationToken)
    {
        var totalBytes = files.Sum(file => file.Length);
        ValidateInput(context, files.Count, totalBytes);

        var fileParts = new List<GeminiPart>();
        foreach (var file in files)
        {
            var part = await filePayloadBuilder.BuildAsync(file, cancellationToken);
            fileParts.Add(part);

            logger.LogInformation(
                "Arquivo preparado para Gemini: {FileName}, {MimeType}, {SizeBytes} bytes",
                file.FileName,
                part.InlineData?.MimeType,
                file.Length);
        }

        return await IngestPartsAsync(
            context,
            fileParts,
            new IngestionSourceMetadata("http_form"),
            IngestionTemporalContext.Now("http_request_received_at"),
            cancellationToken);
    }

    public async Task<IngestionResponse> IngestPartsAsync(
        string? context,
        IReadOnlyCollection<GeminiPart> fileParts,
        IngestionSourceMetadata source,
        IngestionTemporalContext temporalContext,
        CancellationToken cancellationToken)
    {
        ValidateInput(context, fileParts.Count, EstimateBase64PayloadBytes(fileParts));

        var geminiResult = await aiContentService.GenerateStructuredPayloadAsync(context, fileParts, temporalContext, cancellationToken);
        var embeddingText = EmbeddingTextBuilder.Build(geminiResult.StructuredEntry);
        var contentHash = EmbeddingTextBuilder.ComputeSha256(embeddingText);
        var rawPayload = StructuredPayloadRawBuilder.Build(source, context, geminiResult, fileParts.Count);

        var entry = await repository.CreateAsync(
            new ProfessionalEntryCreateRequest
            {
                Source = source,
                StructuredEntry = geminiResult.StructuredEntry,
                RawPayload = rawPayload,
                ContentHash = contentHash,
                EmbeddingModel = localEmbeddingsOptions.Value.Model,
                EmbeddingDimensions = localEmbeddingsOptions.Value.Dimensions
            },
            cancellationToken);

        logger.LogInformation(
            "Entrada salva no PostgreSQL com sucesso. EntryId: {EntryId}; embedding status: {EmbeddingStatus}",
            entry.Id,
            entry.EmbeddingStatus);

        NotionPageResult notionResult;
        try
        {
            notionResult = await notionService.CreatePageAsync(geminiResult.GeneratedPayload, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogError(
                ex,
                "Erro ao salvar no Notion para a entry {EntryId}. O registro foi mantido no PostgreSQL.",
                entry.Id);

            notionResult = new NotionPageResult
            {
                Success = false,
                ErrorBody = ex.Message
            };
        }

        await repository.UpdateNotionSyncAsync(
            entry.Id,
            notionResult.Success,
            notionResult.PageId,
            notionResult.ErrorBody,
            cancellationToken);

        if (!notionResult.Success)
        {
            logger.LogError(
                "Falha ao salvar no Notion para a entry {EntryId}. O registro foi mantido no PostgreSQL. Erro: {Error}",
                entry.Id,
                notionResult.ErrorBody);
        }

        return new IngestionResponse
        {
            Success = true,
            ProfessionalEntryId = entry.Id,
            GeminiModelUsed = geminiResult.ModelUsed,
            StructuredEntry = geminiResult.StructuredEntry,
            NotionPageId = notionResult.PageId,
            NotionUrl = notionResult.Url,
            GeneratedNotionPayload = geminiResult.GeneratedPayload,
            EmbeddingStatus = entry.EmbeddingStatus,
            NotionSyncStatus = notionResult.Success ? "completed" : "failed",
            NotionError = notionResult.ErrorBody
        };
    }

    private void ValidateInput(string? context, int fileCount, long totalBytes)
    {
        if (fileCount == 0 && string.IsNullOrWhiteSpace(context))
        {
            throw new InvalidOperationException("Envie pelo menos um arquivo ou um texto de contexto.");
        }

        if (totalBytes > geminiOptions.Value.MaxRequestBytes)
        {
            throw new InvalidOperationException(
                $"O total enviado tem {totalBytes} bytes, limite atual: {geminiOptions.Value.MaxRequestBytes} bytes.");
        }
    }

    private static long EstimateBase64PayloadBytes(IReadOnlyCollection<GeminiPart> fileParts) =>
        fileParts.Sum(part => part.InlineData?.Data.Length * 3L / 4L ?? 0L);
}
