using CareerVault.Api.Models;
using CareerVault.Api.Options;
using Microsoft.Extensions.Options;

namespace CareerVault.Api.Services;

public sealed class CareerMemoryIngestionService(
    FilePayloadBuilder filePayloadBuilder,
    GeminiService geminiService,
    NotionService notionService,
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
            IngestionTemporalContext.Now("http_request_received_at"),
            cancellationToken);
    }

    public async Task<IngestionResponse> IngestPartsAsync(
        string? context,
        IReadOnlyCollection<GeminiPart> fileParts,
        IngestionTemporalContext temporalContext,
        CancellationToken cancellationToken)
    {
        ValidateInput(context, fileParts.Count, EstimateBase64PayloadBytes(fileParts));

        var geminiResult = await geminiService.GenerateNotionPayloadAsync(context, fileParts, temporalContext, cancellationToken);
        var notionResult = await notionService.CreatePageAsync(geminiResult.GeneratedPayload, cancellationToken);

        return new IngestionResponse
        {
            Success = notionResult.Success,
            GeminiModelUsed = geminiResult.ModelUsed,
            NotionPageId = notionResult.PageId,
            NotionUrl = notionResult.Url,
            GeneratedNotionPayload = geminiResult.GeneratedPayload,
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
