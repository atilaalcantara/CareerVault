using System.Text.Json;
using CareerVault.Api.Models;

namespace CareerVault.Api.Services;

public static class StructuredPayloadRawBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static JsonElement Build(
        IngestionSourceMetadata source,
        string? userContext,
        GeminiStructuredPayloadResult geminiResult,
        int fileCount)
    {
        return JsonSerializer.SerializeToElement(
            new
            {
                sourceType = source.SourceType,
                sourceExternalId = source.SourceExternalId,
                userContext,
                fileCount,
                geminiModelUsed = geminiResult.ModelUsed,
                structuredEntry = geminiResult.StructuredEntry,
                notionPayload = geminiResult.GeneratedPayload,
                geminiEnvelope = geminiResult.RawEnvelope
            },
            JsonOptions);
    }
}
