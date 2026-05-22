using System.Text.Json;

namespace CareerVault.Api.Models;

public sealed record GeminiNotionPayloadResult(string ModelUsed, JsonElement GeneratedPayload);
