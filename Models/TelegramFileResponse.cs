using System.Text.Json.Serialization;

namespace CareerVault.Api.Models;

public sealed class TelegramFileResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("result")]
    public TelegramFileResult? Result { get; init; }
}

public sealed class TelegramFileResult
{
    [JsonPropertyName("file_id")]
    public string? FileId { get; init; }

    [JsonPropertyName("file_path")]
    public string? FilePath { get; init; }
}
