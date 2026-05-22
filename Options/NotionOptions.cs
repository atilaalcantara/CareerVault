namespace CareerVault.Api.Options;

public sealed class NotionOptions
{
    public const string SectionName = "Notion";

    public string Token { get; init; } = string.Empty;
    public string Version { get; init; } = "2022-06-28";
    public string DatabaseId { get; init; } = "e4fba8dd-e59a-4f41-836f-47ee9ef3b75f";
}
