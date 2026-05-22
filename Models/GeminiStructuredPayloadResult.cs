using System.Text.Json;

namespace CareerVault.Api.Models;

public sealed record GeminiStructuredPayloadResult(
    string ModelUsed,
    ProfessionalEntryStructuredDto StructuredEntry,
    JsonElement GeneratedPayload,
    JsonElement RawEnvelope);
