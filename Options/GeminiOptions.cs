namespace CareerVault.Api.Options;

public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string ApiKey { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = "https://generativelanguage.googleapis.com/v1beta";
    public string[] Models { get; init; } = [];
    public string[] TextOnlyModels { get; init; } = [];
    public int MaxOutputTokens { get; init; } = 4096;
    public double Temperature { get; init; } = 0.1;
    public long MaxRequestBytes { get; init; } = 20_000_000;
}
