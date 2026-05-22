using System.Text.Json.Serialization;

namespace CareerVault.Api.Models;

public sealed class TelegramUpdate
{
    [JsonPropertyName("update_id")]
    public long UpdateId { get; init; }

    [JsonPropertyName("message")]
    public TelegramMessage? Message { get; init; }
}

public sealed class TelegramMessage
{
    [JsonPropertyName("message_id")]
    public long MessageId { get; init; }

    [JsonPropertyName("from")]
    public TelegramUser? From { get; init; }

    [JsonPropertyName("chat")]
    public TelegramChat? Chat { get; init; }

    [JsonPropertyName("date")]
    public long? Date { get; init; }

    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonPropertyName("caption")]
    public string? Caption { get; init; }

    [JsonPropertyName("voice")]
    public TelegramFileInfo? Voice { get; init; }

    [JsonPropertyName("audio")]
    public TelegramFileInfo? Audio { get; init; }

    [JsonPropertyName("document")]
    public TelegramFileInfo? Document { get; init; }

    [JsonPropertyName("photo")]
    public List<TelegramPhotoSize> Photo { get; init; } = [];
}

public sealed class TelegramUser
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("username")]
    public string? Username { get; init; }
}

public sealed class TelegramChat
{
    [JsonPropertyName("id")]
    public long Id { get; init; }
}

public class TelegramFileInfo
{
    [JsonPropertyName("file_id")]
    public required string FileId { get; init; }

    [JsonPropertyName("file_name")]
    public string? FileName { get; init; }

    [JsonPropertyName("mime_type")]
    public string? MimeType { get; init; }
}

public sealed class TelegramPhotoSize : TelegramFileInfo
{
    [JsonPropertyName("file_size")]
    public long? FileSize { get; init; }

    [JsonPropertyName("width")]
    public int Width { get; init; }

    [JsonPropertyName("height")]
    public int Height { get; init; }
}
