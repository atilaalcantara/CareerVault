using System.Security.Cryptography;
using System.Text;
using CareerVault.Api.Models;

namespace CareerVault.Api.Services;

public static class EmbeddingTextBuilder
{
    public const string CurrentFormatVersion = "v2-natural";

    public static string Build(ProfessionalEntryStructuredDto entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var title = NormalizeScalar(entry.Title);
        var content = NormalizeScalar(entry.Content);
        var summary = NormalizeScalar(entry.Summary);
        var company = NormalizeScalar(entry.Company);
        var project = NormalizeScalar(entry.Project);
        var role = NormalizeScalar(entry.Role);
        var technologies = NormalizeCollection(entry.Technologies);
        var tags = NormalizeCollection(entry.Tags);

        var sections = new List<string>();

        var contextSentence = BuildContextSentence(role, company, project);
        if (!string.IsNullOrWhiteSpace(contextSentence))
        {
            sections.Add(contextSentence);
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            sections.Add(EnsureSentence($"Atividade principal: {title}"));
        }

        if (!string.IsNullOrWhiteSpace(summary))
        {
            sections.Add(EnsureSentence($"Resumo da experiencia: {summary}"));
        }

        var contentNarrative = BuildContentNarrative(content, title, summary, company, project, role);
        if (!string.IsNullOrWhiteSpace(contentNarrative))
        {
            sections.Add(contentNarrative);
        }

        if (technologies.Length > 0)
        {
            sections.Add(EnsureSentence($"Tecnologias principais: {string.Join(", ", technologies)}"));
        }

        if (tags.Length > 0)
        {
            sections.Add(EnsureSentence($"Contexto e temas relacionados: {string.Join(", ", tags)}"));
        }

        return string.Join('\n', sections);
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

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(BuildHashInput(text)));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string BuildHashInput(string text) => $"{CurrentFormatVersion}\n{text}";

    private static string BuildContextSentence(
        string role,
        string company,
        string project)
    {
        if (string.IsNullOrWhiteSpace(role) && string.IsNullOrWhiteSpace(company) && string.IsNullOrWhiteSpace(project))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.Append(!string.IsNullOrWhiteSpace(role)
            ? $"Atuacao em {role}"
            : "Registro profissional");

        if (!string.IsNullOrWhiteSpace(company))
        {
            builder.Append($" na empresa {company}");
        }

        if (!string.IsNullOrWhiteSpace(project))
        {
            builder.Append(!string.IsNullOrWhiteSpace(company)
                ? $" no projeto {project}"
                : $" relacionado ao projeto {project}");
        }

        return EnsureSentence(builder.ToString());
    }

    private static string BuildContentNarrative(
        string content,
        string title,
        string summary,
        string company,
        string project,
        string role)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var normalizedContent = NormalizeMultilineText(content);
        var lines = normalizedContent
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (lines.Length == 0)
        {
            return string.Empty;
        }

        var knownScalars = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddKnownScalar(knownScalars, title);
        AddKnownScalar(knownScalars, summary);
        AddKnownScalar(knownScalars, company);
        AddKnownScalar(knownScalars, project);
        AddKnownScalar(knownScalars, role);

        var rewrittenLines = new List<string>();
        foreach (var line in lines)
        {
            var rewritten = RewriteContentLine(line, knownScalars);
            if (!string.IsNullOrWhiteSpace(rewritten))
            {
                rewrittenLines.Add(rewritten);
            }
        }

        return string.Join(' ', rewrittenLines);
    }

    private static string RewriteContentLine(string line, HashSet<string> knownScalars)
    {
        var separatorIndex = line.IndexOf(':');
        if (separatorIndex <= 0 || separatorIndex >= line.Length - 1)
        {
            return EnsureSentence(line);
        }

        var key = line[..separatorIndex].Trim();
        var value = NormalizeInlineText(line[(separatorIndex + 1)..]);
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (knownScalars.Contains(value) && IsDuplicatedKey(key))
        {
            return string.Empty;
        }

        return key.ToLowerInvariant() switch
        {
            "impacto" => EnsureSentence($"Impacto observado: {value}"),
            "bullets_curriculo" => EnsureSentence($"Destaques de curriculo: {value}"),
            "ideias_linkedin" => EnsureSentence($"Possiveis ganchos para comunicacao profissional: {value}"),
            "tecnologias" => EnsureSentence($"Tecnologias mencionadas: {value}"),
            "tags" => EnsureSentence($"Contexto adicional: {value}"),
            "tipo" => EnsureSentence($"Tipo de atividade: {value}"),
            _ when IsDuplicatedKey(key) => string.Empty,
            _ => EnsureSentence($"{BeautifyLabel(key)}: {value}")
        };
    }

    private static bool IsDuplicatedKey(string key) =>
        key.Equals("title", StringComparison.OrdinalIgnoreCase)
        || key.Equals("titulo", StringComparison.OrdinalIgnoreCase)
        || key.Equals("content", StringComparison.OrdinalIgnoreCase)
        || key.Equals("summary", StringComparison.OrdinalIgnoreCase)
        || key.Equals("resumo", StringComparison.OrdinalIgnoreCase)
        || key.Equals("company", StringComparison.OrdinalIgnoreCase)
        || key.Equals("empresa", StringComparison.OrdinalIgnoreCase)
        || key.Equals("project", StringComparison.OrdinalIgnoreCase)
        || key.Equals("projeto", StringComparison.OrdinalIgnoreCase)
        || key.Equals("role", StringComparison.OrdinalIgnoreCase)
        || key.Equals("papel", StringComparison.OrdinalIgnoreCase);

    private static string BeautifyLabel(string key)
    {
        var words = key
            .Replace('_', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return string.Join(
            ' ',
            words.Select(word =>
                word.Length == 0
                    ? string.Empty
                    : char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant()));
    }

    private static void AddKnownScalar(HashSet<string> values, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            values.Add(value);
        }
    }

    private static string EnsureSentence(string value)
    {
        var normalized = NormalizeInlineText(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        return normalized[^1] is '.' or '!' or '?'
            ? normalized
            : $"{normalized}.";
    }

    private static string NormalizeScalar(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : NormalizeMultilineText(value);

    private static string NormalizeMultilineText(string value) =>
        value.Trim().Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static string NormalizeInlineText(string value) =>
        string.Join(
            ' ',
            NormalizeMultilineText(value)
                .Split(['\n', '\t', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static string[] NormalizeCollection(IEnumerable<string>? values) =>
        values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray()
        ?? [];
}
