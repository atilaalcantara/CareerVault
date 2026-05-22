using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CareerVault.Api.Models;
using CareerVault.Api.Options;
using Microsoft.Extensions.Options;

namespace CareerVault.Api.Services;

public sealed class NotionService(
    HttpClient httpClient,
    IOptions<NotionOptions> options,
    ILogger<NotionService> logger)
{
    public async Task<NotionPageResult> CreatePageAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        var config = options.Value;
        ValidateOptions(config);

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.notion.com/v1/pages");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", NormalizeBearerToken(config.Token));
        request.Headers.Add("Notion-Version", config.Version);
        request.Content = new StringContent(payload.GetRawText(), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Notion retornou erro HTTP {StatusCode}", (int)response.StatusCode);
            return new NotionPageResult
            {
                Success = false,
                ErrorBody = body
            };
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        return new NotionPageResult
        {
            Success = true,
            PageId = root.TryGetProperty("id", out var id) ? id.GetString() : null,
            Url = root.TryGetProperty("url", out var url) ? url.GetString() : null
        };
    }

    private static string NormalizeBearerToken(string token)
    {
        const string bearerPrefix = "Bearer ";
        return token.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? token[bearerPrefix.Length..].Trim()
            : token.Trim();
    }

    private static void ValidateOptions(NotionOptions config)
    {
        if (string.IsNullOrWhiteSpace(config.Token))
        {
            throw new InvalidOperationException("Notion:Token nao configurado. Use a variavel de ambiente NOTION__TOKEN.");
        }

        if (string.IsNullOrWhiteSpace(config.Version))
        {
            throw new InvalidOperationException("Notion:Version nao configurado.");
        }
    }
}
