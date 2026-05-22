using System.Text.Json;
using CareerVault.Api.Models;
using CareerVault.Api.Options;
using CareerVault.Api.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection(GeminiOptions.SectionName));
builder.Services.Configure<NotionOptions>(builder.Configuration.GetSection(NotionOptions.SectionName));
builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection(TelegramOptions.SectionName));

var maxRequestBytes = builder.Configuration.GetValue<long?>("Gemini:MaxRequestBytes") ?? 20_000_000;
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = maxRequestBytes;
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = maxRequestBytes;
});

builder.Services.AddHttpClient<GeminiService>();
builder.Services.AddHttpClient<NotionService>();
builder.Services.AddHttpClient<TelegramService>();
builder.Services.AddSingleton<FilePayloadBuilder>();
builder.Services.AddSingleton<TelegramUpdateQueue>();
builder.Services.AddSingleton<TelegramMemorySessionStore>();
builder.Services.AddScoped<CareerMemoryIngestionService>();
builder.Services.AddScoped<TelegramUpdateHandler>();
builder.Services.AddHostedService<TelegramBackgroundWorker>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => Results.Redirect("/swagger"))
    .ExcludeFromDescription();

app.MapPost("/api/memory/ingest", async (
        HttpRequest request,
        CareerMemoryIngestionService careerMemoryIngestionService,
        IOptions<GeminiOptions> geminiOptions,
        ILogger<Program> logger,
        CancellationToken cancellationToken) =>
    {
        if (!request.HasFormContentType)
        {
            return Results.Problem(
                title: "Content-Type invalido",
                detail: "Envie a requisicao como multipart/form-data.",
                statusCode: StatusCodes.Status415UnsupportedMediaType);
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var files = form.Files.GetFiles("files");
        if (files.Count == 0 && form.Files.Count > 0)
        {
            logger.LogInformation(
                "Nenhum arquivo recebido no campo 'files'. Usando todos os arquivos do multipart. Campos recebidos: {FileFields}",
                string.Join(", ", form.Files.Select(file => file.Name).Distinct()));

            files = form.Files;
        }

        var context = form.TryGetValue("context", out var contextValues)
            ? contextValues.ToString()
            : string.Empty;

        if (files.Count == 0 && string.IsNullOrWhiteSpace(context))
        {
            return Results.Problem(
                title: "Entrada vazia",
                detail: "Envie pelo menos um arquivo em 'files' ou um texto em 'context'.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var totalBytes = files.Sum(file => file.Length);
        if (totalBytes > geminiOptions.Value.MaxRequestBytes)
        {
            return Results.Problem(
                title: "Arquivos excedem o limite configurado",
                detail: $"O total enviado tem {totalBytes} bytes, limite atual: {geminiOptions.Value.MaxRequestBytes} bytes.",
                statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        try
        {
            var ingestResponse = await careerMemoryIngestionService.IngestFormFilesAsync(context, files, cancellationToken);

            if (!ingestResponse.Success)
            {
                return Results.Json(ingestResponse, statusCode: StatusCodes.Status502BadGateway);
            }

            return Results.Ok(ingestResponse);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Erro de validacao no ingest");
            return Results.Problem(title: "Erro de validacao", detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Erro HTTP ao integrar com Gemini ou Notion");
            return Results.Problem(title: "Erro de integracao externa", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Gemini retornou JSON invalido");
            return Results.Problem(title: "Gemini retornou JSON invalido", detail: ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    })
    .Accepts<IngestionRequest>("multipart/form-data")
    .Produces<IngestionResponse>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
    .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
    .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
    .ProducesProblem(StatusCodes.Status502BadGateway)
    .WithName("IngestCareerMemory")
    .DisableAntiforgery();

app.MapPost("/api/telegram/webhook", async (
        HttpRequest request,
        TelegramUpdate update,
        TelegramUpdateQueue updateQueue,
        IOptions<TelegramOptions> telegramOptions,
        CancellationToken cancellationToken) =>
    {
        var webhookSecret = telegramOptions.Value.WebhookSecret;
        if (!string.IsNullOrWhiteSpace(webhookSecret))
        {
            var receivedSecret = request.Headers["X-Telegram-Bot-Api-Secret-Token"].ToString();
            if (!string.Equals(receivedSecret, webhookSecret, StringComparison.Ordinal))
            {
                return Results.Unauthorized();
            }
        }

        await updateQueue.EnqueueAsync(update, cancellationToken);
        return Results.Ok(new { success = true, queued = true });
    })
    .Produces(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status401Unauthorized)
    .WithName("TelegramWebhook");

app.Run();

public sealed class IngestionRequest
{
    [FromForm(Name = "files")]
    public List<IFormFile> Files { get; set; } = new();

    [FromForm(Name = "context")]
    public string? Context { get; set; }
}
