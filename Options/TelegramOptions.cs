namespace CareerVault.Api.Options;

public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    public string BotToken { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = "https://api.telegram.org";
    public string FileBaseUrl { get; init; } = "https://api.telegram.org/file";
    public string? WebhookSecret { get; init; }
    public long[] AllowedUserIds { get; init; } = [];
    public int QueueCapacity { get; init; } = 100;
}
