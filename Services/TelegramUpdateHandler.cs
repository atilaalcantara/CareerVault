using CareerVault.Api.Models;
using CareerVault.Api.Options;
using Microsoft.Extensions.Options;

namespace CareerVault.Api.Services;

public sealed class TelegramUpdateHandler(
    TelegramService telegramService,
    CareerMemoryIngestionService memoryIngestService,
    TelegramMemorySessionStore sessionStore,
    IOptions<TelegramOptions> options,
    ILogger<TelegramUpdateHandler> logger)
{
    private const string UsageMessage = """
CareerVault pronta.

Como usar:
1. Envie /iniciar
2. Mande textos, audios gravados no Telegram, imagens ou PDFs
3. Quando terminar, envie /enviar
4. Confirme com /confirmar ou cancele com /cancelar

Eu vou juntar tudo em uma entrada unica e adicionar no Notion somente depois da confirmacao.

Tipos aceitos: audio gravado no Telegram, .m4a, .mp3, .wav, .ogg, .oga, .webm; imagens .png, .jpg, .jpeg; PDF .pdf.
""";

    public async Task HandleAsync(TelegramUpdate update, CancellationToken cancellationToken)
    {
        var message = update.Message;
        var chatId = message?.Chat?.Id;
        var userId = message?.From?.Id;

        if (message is null || chatId is null)
        {
            return;
        }

        if (!IsAllowedUser(userId))
        {
            logger.LogWarning("Mensagem ignorada de usuario Telegram nao autorizado: {UserId}", userId);
            return;
        }

        var context = message.Text ?? message.Caption ?? string.Empty;
        var command = GetCommand(message.Text);

        if (command is "/start" or "/help")
        {
            await telegramService.SendMessageAsync(chatId.Value, UsageMessage, cancellationToken);
            return;
        }

        try
        {
            if (command == "/iniciar")
            {
                sessionStore.Start(chatId.Value);
                await telegramService.SendMessageAsync(
                    chatId.Value,
                    "Coleta iniciada. Pode mandar textos, audios gravados no Telegram, imagens e PDFs. Quando terminar, envie /enviar.",
                    cancellationToken);
                return;
            }

            if (command == "/cancelar")
            {
                sessionStore.Remove(chatId.Value);
                await telegramService.SendMessageAsync(chatId.Value, "Coleta cancelada.", cancellationToken);
                return;
            }

            if (command == "/enviar")
            {
                await AskForConfirmationAsync(chatId.Value, cancellationToken);
                return;
            }

            if (command == "/confirmar")
            {
                await SendSessionAsync(chatId.Value, update.UpdateId, cancellationToken);
                return;
            }

            if (!sessionStore.TryGet(chatId.Value, out var session))
            {
                await telegramService.SendMessageAsync(chatId.Value, UsageMessage, cancellationToken);
                return;
            }

            AddToSession(session, message, context, out var acceptedCount, out var rejectedFiles);

            if (rejectedFiles.Count > 0)
            {
                await telegramService.SendMessageAsync(
                    chatId.Value,
                    $"Nao aceitei: {string.Join(", ", rejectedFiles)}. {SupportedFileTypes.DescribeAllowedTypes()}",
                    cancellationToken);
            }

            if (acceptedCount > 0 || !string.IsNullOrWhiteSpace(context))
            {
                var snapshot = session.Snapshot();
                await telegramService.SendMessageAsync(
                    chatId.Value,
                    $"Ok, adicionado na coleta. Itens ate agora: {snapshot.Files.Count} arquivo(s). Envie /enviar quando terminar.",
                    cancellationToken);
                return;
            }

            if (rejectedFiles.Count > 0)
            {
                return;
            }

            await telegramService.SendMessageAsync(chatId.Value, "Nao encontrei texto ou arquivo aceito nessa mensagem.", cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or System.Text.Json.JsonException)
        {
            logger.LogError(ex, "Falha ao processar update do Telegram {UpdateId}", update.UpdateId);
            await telegramService.SendMessageAsync(chatId.Value, $"Deu erro ao processar: {Trim(ex.Message)}", cancellationToken);
        }
    }

    private async Task AskForConfirmationAsync(long chatId, CancellationToken cancellationToken)
    {
        if (!sessionStore.TryGet(chatId, out var session))
        {
            await telegramService.SendMessageAsync(chatId, "Nenhuma coleta ativa. Envie /iniciar para comecar.", cancellationToken);
            return;
        }

        var snapshot = session.Snapshot();
        if (snapshot.IsEmpty)
        {
            await telegramService.SendMessageAsync(chatId, "A coleta esta vazia. Mande texto, audio gravado no Telegram, imagem ou PDF antes de /enviar.", cancellationToken);
            return;
        }

        session.MarkWaitingConfirmation();

        var hasText = string.IsNullOrWhiteSpace(snapshot.Context) ? "nao" : "sim";
        await telegramService.SendMessageAsync(
            chatId,
            $"Pronto para enviar para o Notion. Resumo da coleta: texto: {hasText}; arquivos: {snapshot.Files.Count}. Envie /confirmar para enviar ou /cancelar para cancelar.",
            cancellationToken);
    }

    private async Task SendSessionAsync(long chatId, long updateId, CancellationToken cancellationToken)
    {
        if (!sessionStore.TryGet(chatId, out var session))
        {
            await telegramService.SendMessageAsync(chatId, "Nenhuma coleta ativa. Envie /iniciar para comecar.", cancellationToken);
            return;
        }

        var snapshot = session.Snapshot();
        if (snapshot.IsEmpty)
        {
            await telegramService.SendMessageAsync(chatId, "A coleta esta vazia. Mande texto, audio gravado no Telegram, imagem ou PDF antes de /enviar.", cancellationToken);
            return;
        }

        if (!snapshot.WaitingConfirmation)
        {
            await telegramService.SendMessageAsync(chatId, "Antes de enviar, use /enviar para revisar e confirmar a coleta.", cancellationToken);
            return;
        }

            await telegramService.SendMessageAsync(chatId, "Confirmado. Processando e enviando para o Notion...", cancellationToken);

        var parts = new List<GeminiPart>();
        foreach (var file in snapshot.Files)
        {
            parts.Add(await telegramService.DownloadAsGeminiPartAsync(
                file.FileId,
                file.FileName,
                file.MimeType,
                cancellationToken));
        }

        var result = await memoryIngestService.IngestPartsAsync(
            snapshot.Context,
            parts,
            snapshot.ReferenceDate ?? IngestionTemporalContext.Now("telegram_confirm_command_received_at"),
            cancellationToken);
        if (result.Success)
        {
            sessionStore.Remove(chatId);

            var successMessage = string.IsNullOrWhiteSpace(result.NotionUrl)
                ? "Adicionado ao Notion com sucesso."
                : $"Adicionado ao Notion com sucesso: {result.NotionUrl}";

            await telegramService.SendMessageAsync(chatId, successMessage, cancellationToken);
            return;
        }

        logger.LogWarning("Notion retornou erro ao processar update Telegram {UpdateId}: {Error}", updateId, result.NotionError);
        await telegramService.SendMessageAsync(chatId, $"Nao consegui adicionar ao Notion. A coleta foi mantida para voce tentar /enviar de novo. Erro: {Trim(result.NotionError)}", cancellationToken);
    }

    private bool IsAllowedUser(long? userId)
    {
        var allowedUserIds = options.Value.AllowedUserIds;
        return allowedUserIds.Length == 0 || (userId is not null && allowedUserIds.Contains(userId.Value));
    }

    private static void AddToSession(
        TelegramMemorySession session,
        TelegramMessage message,
        string context,
        out int acceptedCount,
        out List<string> rejectedFiles)
    {
        acceptedCount = 0;
        rejectedFiles = [];

        if (!string.IsNullOrWhiteSpace(context))
        {
            session.AddContext(context, GetMessageTemporalContext(message));
            acceptedCount++;
        }

        var files = CollectSupportedFiles(message, rejectedFiles);
        session.AddFiles(files, GetMessageTemporalContext(message));
        acceptedCount += files.Count;
    }

    private static IngestionTemporalContext GetMessageTemporalContext(TelegramMessage message) =>
        message.Date is { } unixSeconds
            ? IngestionTemporalContext.FromUnixSeconds(unixSeconds, "telegram_message_date")
            : IngestionTemporalContext.Now("telegram_message_received_without_date");

    private static List<TelegramFileInfo> CollectSupportedFiles(TelegramMessage message, List<string> rejectedFiles)
    {
        var files = new List<TelegramFileInfo>();

        if (message.Voice is not null)
        {
            AddIfSupported(files, rejectedFiles, new TelegramFileInfo
            {
                FileId = message.Voice.FileId,
                FileName = message.Voice.FileName ?? "telegram-voice.oga",
                MimeType = message.Voice.MimeType ?? "audio/ogg"
            }, "audio gravado no Telegram");
        }

        if (message.Audio is not null)
        {
            AddIfSupported(files, rejectedFiles, message.Audio, "audio");
        }

        if (message.Document is not null)
        {
            AddIfSupported(files, rejectedFiles, message.Document, message.Document.FileName ?? "documento");
        }

        var bestPhoto = message.Photo
            .OrderByDescending(photo => photo.FileSize ?? 0)
            .ThenByDescending(photo => photo.Width * photo.Height)
            .FirstOrDefault();

        if (bestPhoto is not null)
        {
            files.Add(new TelegramFileInfo
            {
                FileId = bestPhoto.FileId,
                FileName = "photo.jpg",
                MimeType = "image/jpeg"
            });
        }

        return files;
    }

    private static void AddIfSupported(
        List<TelegramFileInfo> acceptedFiles,
        List<string> rejectedFiles,
        TelegramFileInfo file,
        string displayName)
    {
        var fileName = file.FileName ?? displayName;
        if (!SupportedFileTypes.TryResolve(fileName, file.MimeType, out var mimeType))
        {
            rejectedFiles.Add(fileName);
            return;
        }

        acceptedFiles.Add(new TelegramFileInfo
        {
            FileId = file.FileId,
            FileName = fileName,
            MimeType = mimeType
        });
    }

    private static string? GetCommand(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || !text.StartsWith('/'))
        {
            return null;
        }

        var firstToken = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return firstToken?.Split('@')[0].ToLowerInvariant();
    }

    private static string Trim(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "erro sem detalhes.";
        }

        return text.Length <= 800 ? text : text[..800];
    }
}
