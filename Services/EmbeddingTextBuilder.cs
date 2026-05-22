using System.Security.Cryptography;
using System.Text;
using CareerVault.Api.Models;

namespace CareerVault.Api.Services;

public static class EmbeddingTextBuilder
{
    public static string Build(ProfessionalEntryStructuredDto entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var technologies = NormalizeCollection(entry.Technologies);
        var tags = NormalizeCollection(entry.Tags);

        return string.Join(
            '\n',
            [
                $"title: {NormalizeScalar(entry.Title)}",
                $"content: {NormalizeScalar(entry.Content)}",
                $"summary: {NormalizeScalar(entry.Summary)}",
                $"company: {NormalizeScalar(entry.Company)}",
                $"project: {NormalizeScalar(entry.Project)}",
                $"role: {NormalizeScalar(entry.Role)}",
                $"technologies: {string.Join(", ", technologies)}",
                $"tags: {string.Join(", ", tags)}"
            ]);
    }

    public static string Build(ProfessionalEntryEmbeddingJob entry) =>
        Build(new ProfessionalEntryStructuredDto
        {
            Title = entry.Title,
            Content = entry.Content,
            Summary = entry.Summary,
            Company = entry.Company,
            Project = entry.Project,
            Role = entry.Role,
            Technologies = entry.Technologies,
            Tags = entry.Tags
        });

    public static string ComputeSha256(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string NormalizeScalar(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static string[] NormalizeCollection(IEnumerable<string>? values) =>
        values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray()
        ?? [];
}
