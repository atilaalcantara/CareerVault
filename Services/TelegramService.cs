using System.Text;
using System.Text.Json;
using CareerVault.Api.Models;
using CareerVault.Api.Options;
using Microsoft.Extensions.Options;

namespace CareerVault.Api.Services;

public sealed class TelegramService(
    HttpClient httpClient,
    IOptions<TelegramOptions> options,
    FilePayloadBuilder filePayloadBuilder,
    ILogger<TelegramService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task SendMessageAsync(long chatId, string text, CancellationToken cancellationToken)
    {
        var config = GetValidatedOptions();
        var endpoint = $"{config.BaseUrl.TrimEnd('/')}/bot{config.BotToken}/sendMessage";
        var payload = JsonSerializer.Serialize(new
        {
            chat_id = chatId,
            text
        }, JsonOptions);

        using var response = await httpClient.PostAsync(
            endpoint,
            new StringContent(payload, Encoding.UTF8, "application/json"),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("Telegram sendMessage falhou com HTTP {StatusCode}: {Body}", (int)response.StatusCode, body);
        }
    }

    public async Task<GeminiPart> DownloadAsGeminiPartAsync(
        string fileId,
        string? fileName,
        string? mimeType,
        CancellationToken cancellationToken)
    {
        var config = GetValidatedOptions();
        var getFileEndpoint = $"{config.BaseUrl.TrimEnd('/')}/bot{config.BotToken}/getFile?file_id={Uri.EscapeDataString(fileId)}";

        using var fileResponse = await httpClient.GetAsync(getFileEndpoint, cancellationToken);
        var fileBody = await fileResponse.Content.ReadAsStringAsync(cancellationToken);
        fileResponse.EnsureSuccessStatusCode();

        var telegramFile = JsonSerializer.Deserialize<TelegramFileResponse>(fileBody, JsonOptions);
        var filePath = telegramFile?.Result?.FilePath;
        if (telegramFile?.Ok != true || string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException($"Telegram nao retornou file_path para o arquivo {fileId}: {telegramFile?.Description}");
        }

        var downloadEndpoint = $"{config.FileBaseUrl.TrimEnd('/')}/bot{config.BotToken}/{filePath}";
        var bytes = await httpClient.GetByteArrayAsync(downloadEndpoint, cancellationToken);
        var effectiveFileName = fileName ?? Path.GetFileName(filePath);

        logger.LogInformation(
            "Arquivo baixado do Telegram: {FileName}, {MimeType}, {SizeBytes} bytes",
            effectiveFileName,
            mimeType,
            bytes.Length);

        return filePayloadBuilder.Build(bytes, effectiveFileName, mimeType);
    }

    private TelegramOptions GetValidatedOptions()
    {
        var config = options.Value;
        if (string.IsNullOrWhiteSpace(config.BotToken))
        {
            throw new InvalidOperationException("Telegram:BotToken nao configurado. Use user-secrets ou TELEGRAM__BOTTOKEN.");
        }

        return config;
    }
}
