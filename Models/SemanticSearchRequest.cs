using System.Text.Json.Serialization;

namespace CareerVault.Api.Models;

public sealed class SemanticSearchRequest
{
    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;

    [JsonPropertyName("limit")]
    public int Limit { get; set; } = 10;
}
