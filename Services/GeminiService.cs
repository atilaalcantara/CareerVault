using System.Net;
using System.Text;
using System.Text.Json;
using CareerVault.Api.Models;
using CareerVault.Api.Options;
using Microsoft.Extensions.Options;

namespace CareerVault.Api.Services;

public sealed class GeminiService(
    HttpClient httpClient,
    IOptions<GeminiOptions> options,
    IOptions<NotionOptions> notionOptions,
    ILogger<GeminiService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10)
    ];

    public async Task<GeminiNotionPayloadResult> GenerateNotionPayloadAsync(
        string? userContext,
        IReadOnlyCollection<GeminiPart> fileParts,
        IngestionTemporalContext temporalContext,
        CancellationToken cancellationToken)
    {
        var config = options.Value;
        ValidateOptions(config);

        var errors = new List<string>();
        foreach (var model in config.Models.Where(model => !string.IsNullOrWhiteSpace(model)))
        {
            try
            {
                logger.LogInformation("Chamando Gemini com modelo {Model}", model);
                var generatedText = await GenerateWithRetriesAsync(config, model, userContext, fileParts, temporalContext, cancellationToken);
                var payload = ParseAndValidateNotionPayload(generatedText, notionOptions.Value.DatabaseId);

                return new GeminiNotionPayloadResult(model, payload);
            }
            catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or JsonException or TaskCanceledException)
            {
                errors.Add($"{model}: {ex.Message}");
                logger.LogWarning(ex, "Falha ao usar modelo Gemini {Model}", model);
            }
        }

        throw new HttpRequestException($"Todos os modelos Gemini configurados falharam. Detalhes: {string.Join(" | ", errors)}");
    }

    private async Task<string> GenerateWithRetriesAsync(
        GeminiOptions config,
        string model,
        string? userContext,
        IReadOnlyCollection<GeminiPart> fileParts,
        IngestionTemporalContext temporalContext,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt <= RetryDelays.Length; attempt++)
        {
            using var request = BuildHttpRequest(config, model, userContext, fileParts, temporalContext);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return ExtractText(body);
            }

            if (!IsTransient(response.StatusCode) || attempt == RetryDelays.Length)
            {
                throw new HttpRequestException(
                    $"Gemini retornou {(int)response.StatusCode} {response.ReasonPhrase}: {body}",
                    null,
                    response.StatusCode);
            }

            await Task.Delay(RetryDelays[attempt], cancellationToken);
        }

        throw new HttpRequestException("Falha inesperada ao chamar Gemini.");
    }

    private static HttpRequestMessage BuildHttpRequest(
        GeminiOptions config,
        string model,
        string? userContext,
        IReadOnlyCollection<GeminiPart> fileParts,
        IngestionTemporalContext temporalContext)
    {
        var endpoint = $"{config.BaseUrl.TrimEnd('/')}/models/{Uri.EscapeDataString(model)}:generateContent?key={Uri.EscapeDataString(config.ApiKey)}";
        var parts = new List<GeminiPart>
        {
            new()
            {
                Text = BuildPrompt(userContext, temporalContext)
            }
        };
        parts.AddRange(fileParts);

        var payload = new GeminiGenerateContentRequest
        {
            Contents =
            [
                new GeminiContent
                {
                    Parts = parts
                }
            ],
            GenerationConfig = new GeminiGenerationConfig
            {
                ResponseMimeType = "application/json",
                MaxOutputTokens = config.MaxOutputTokens,
                Temperature = config.Temperature
            }
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        return new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static string BuildPrompt(string? userContext, IngestionTemporalContext temporalContext)
    {
        var temporalBlock = BuildTemporalBlock(temporalContext);
        if (string.IsNullOrWhiteSpace(userContext))
        {
            return $"""
{IngestionPrompt.Text}

{temporalBlock}
""";
        }

        return $"""
{IngestionPrompt.Text}

{temporalBlock}

Tarefa atual:
Use tambem o contexto textual opcional abaixo como sinal do usuario, sem inventar fatos alem dele e dos arquivos.

Contexto do usuario:
{userContext}
""";
    }

    private static string BuildTemporalBlock(IngestionTemporalContext temporalContext)
    {
        var reference = temporalContext.ReferenceDateTime;
        return $"""
Contexto temporal enviado pela API:
- Fonte da data de referencia: {temporalContext.Source}
- Timezone: {temporalContext.TimeZoneId}
- Data atual/de referencia: {reference:yyyy-MM-dd}
- Horario atual/de referencia: {reference:HH:mm:ss zzz}
- Data e horario ISO: {reference:yyyy-MM-ddTHH:mm:sszzz}

Regras adicionais de data:
- Se o conteudo tiver uma data explicita do evento, certificacao, entrega ou documento, use essa data.
- Se o usuario disser "hoje", use {reference:yyyy-MM-dd}.
- Se o usuario disser "ontem", use a data imediatamente anterior a {reference:yyyy-MM-dd}.
- Se o usuario disser expressoes relativas como "semana passada", "mes passado" ou "ha X dias", calcule com base na data de referencia acima.
- Se nao houver data explicita nem relativa no conteudo, use a data de referencia acima no campo properties.Data.date.
""";
    }

    private static string ExtractText(string responseBody)
    {
        var response = JsonSerializer.Deserialize<GeminiGenerateContentResponse>(responseBody, JsonOptions);
        var text = response?.Candidates.FirstOrDefault()?.Content?.Parts.FirstOrDefault()?.Text;

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Resposta do Gemini nao contem candidates[0].content.parts[0].text.");
        }

        return text;
    }

    private static JsonElement ParseAndValidateNotionPayload(string generatedText, string databaseId)
    {
        using var document = JsonDocument.Parse(generatedText);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Payload gerado deve ser um objeto JSON.");
        }

        if (!root.TryGetProperty("parent", out var parent) ||
            !parent.TryGetProperty("database_id", out var databaseIdElement) ||
            databaseIdElement.GetString() != databaseId)
        {
            throw new JsonException("Payload gerado nao contem parent.database_id esperado.");
        }

        if (!root.TryGetProperty("properties", out var properties) || properties.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Payload gerado nao contem properties valido.");
        }

        var requiredProperties = new[]
        {
            "Título",
            "Data",
            "Projeto",
            "Tipo",
            "Tecnologias",
            "Resumo",
            "Impacto",
            "Bullets Currículo",
            "Ideias LinkedIn",
            "Tags"
        };

        foreach (var propertyName in requiredProperties)
        {
            if (!properties.TryGetProperty(propertyName, out _))
            {
                throw new JsonException($"Payload gerado nao contem properties.{propertyName}.");
            }
        }

        return root.Clone();
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

    private static void ValidateOptions(GeminiOptions config)
    {
        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            throw new InvalidOperationException("Gemini:ApiKey nao configurado. Use a variavel de ambiente GEMINI__APIKEY.");
        }

        if (config.Models.Length == 0)
        {
            throw new InvalidOperationException("Gemini:Models precisa ter pelo menos um modelo configurado.");
        }
    }
}
