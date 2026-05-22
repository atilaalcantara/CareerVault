using System.Text.Json;
using CareerVault.Api.Models;
using CareerVault.Api.Options;
using CareerVault.Api.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Npgsql;
using Pgvector.Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection(GeminiOptions.SectionName));
builder.Services.Configure<NotionOptions>(builder.Configuration.GetSection(NotionOptions.SectionName));
builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection(TelegramOptions.SectionName));
builder.Services.Configure<EmbeddingWorkerOptions>(builder.Configuration.GetSection(EmbeddingWorkerOptions.SectionName));
builder.Services.Configure<LocalEmbeddingsOptions>(builder.Configuration.GetSection(LocalEmbeddingsOptions.SectionName));

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
builder.Services.AddSingleton(static sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("Postgres");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "ConnectionStrings:Postgres nao configurada. Defina a string de conexao para habilitar a persistencia local.");
    }

    var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
    dataSourceBuilder.UseVector();
    return dataSourceBuilder.Build();
});
builder.Services.AddSingleton<FilePayloadBuilder>();
builder.Services.AddSingleton<TelegramUpdateQueue>();
builder.Services.AddSingleton<TelegramMemorySessionStore>();
builder.Services.AddSingleton<IEmbeddingProvider, LocalEmbeddingProvider>();
builder.Services.AddSingleton<CareerVaultRepository>();
builder.Services.AddSingleton<NotionCsvBackfillService>();
builder.Services.AddSingleton<SemanticSearchService>();
builder.Services.AddScoped<CareerMemoryIngestionService>();
builder.Services.AddScoped<TelegramUpdateHandler>();
builder.Services.AddHostedService<TelegramBackgroundWorker>();
builder.Services.AddHostedService<EmbeddingBackgroundWorker>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (args.Length >= 2 && string.Equals(args[0], "import-notion-csv", StringComparison.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var importer = scope.ServiceProvider.GetRequiredService<NotionCsvBackfillService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var importResult = await importer.ImportAsync(args[1], CancellationToken.None);

    logger.LogInformation(
        "Importacao do Notion finalizada. Processadas: {Processed}; importadas: {Imported}; duplicadas ignoradas: {SkippedDuplicates}",
        importResult.Processed,
        importResult.Imported,
        importResult.SkippedDuplicates);

    if (importResult.Warnings.Count > 0)
    {
        foreach (var warning in importResult.Warnings)
        {
            logger.LogWarning("{Warning}", warning);
        }
    }

    return;
}

if (args.Length >= 1 && string.Equals(args[0], "mark-embeddings-stale", StringComparison.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var repository = scope.ServiceProvider.GetRequiredService<CareerVaultRepository>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var embeddingsOptions = scope.ServiceProvider.GetRequiredService<IOptions<LocalEmbeddingsOptions>>();
    var model = args.Length >= 2 ? args[1] : embeddingsOptions.Value.Model;

    var updatedEntries = await repository.MarkEmbeddingsStaleAsync(model, CancellationToken.None);

    logger.LogInformation(
        "Entries marcadas como stale para reprocessamento de embeddings. Modelo: {Model}; total: {UpdatedEntries}; formato alvo: {EmbeddingFormatVersion}",
        model,
        updatedEntries,
        EmbeddingTextBuilder.CurrentFormatVersion);

    return;
}

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
        catch (NpgsqlException ex)
        {
            logger.LogError(ex, "Erro ao persistir no PostgreSQL");
            return Results.Problem(title: "Erro ao persistir no PostgreSQL", detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
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
    .ProducesProblem(StatusCodes.Status500InternalServerError)
    .WithName("IngestCareerMemory")
    .DisableAntiforgery();

app.MapPost("/api/v1/search/semantic", async (
        SemanticSearchRequest request,
        SemanticSearchService semanticSearchService,
        ILogger<Program> logger,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var results = await semanticSearchService.SearchAsync(request.Query, request.Limit, cancellationToken);
            return Results.Ok(results);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Busca semantica invalida.");
            return Results.Problem(title: "Busca semantica invalida", detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    })
    .Accepts<SemanticSearchRequest>("application/json")
    .Produces<IReadOnlyList<SemanticSearchResultItem>>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .WithName("SearchCareerVaultSemantic");

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
