using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic.FileIO;
using CareerVault.Api.Models;
using CareerVault.Api.Options;
using Microsoft.Extensions.Options;

namespace CareerVault.Api.Services;

public sealed class NotionCsvBackfillService(
    CareerVaultRepository repository,
    IOptions<LocalEmbeddingsOptions> embeddingsOptions,
    ILogger<NotionCsvBackfillService> logger)
{
    private static readonly string[] SupportedColumns =
    [
        "Título",
        "Bullets Currículo",
        "Data",
        "Ideias LinkedIn",
        "Impacto",
        "Projeto",
        "Resumo",
        "Tags",
        "Tecnologias",
        "Tipo"
    ];

    public async Task<NotionCsvImportResult> ImportAsync(string inputPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            throw new InvalidOperationException("Informe o caminho do arquivo CSV ou ZIP exportado do Notion.");
        }

        var resolvedPath = Path.GetFullPath(inputPath);
        if (!File.Exists(resolvedPath))
        {
            throw new FileNotFoundException($"Arquivo nao encontrado: {resolvedPath}");
        }

        var csvPath = await ResolveCsvPathAsync(resolvedPath, cancellationToken);
        var rows = ReadRows(csvPath);
        var result = new NotionCsvImportResult();

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Processed++;

            var structuredEntry = MapRowToStructuredEntry(row, result.Warnings);
            var embeddingText = EmbeddingTextBuilder.Build(structuredEntry);
            var contentHash = EmbeddingTextBuilder.ComputeSha256(embeddingText);

            if (await repository.ExistsByContentHashAsync(contentHash, cancellationToken))
            {
                result.SkippedDuplicates++;
                continue;
            }

            var rawPayload = JsonSerializer.SerializeToElement(row);
            await repository.CreateAsync(
                new ProfessionalEntryCreateRequest
                {
                    Source = new IngestionSourceMetadata("notion_csv_backfill"),
                    StructuredEntry = structuredEntry,
                    RawPayload = rawPayload,
                    ContentHash = contentHash,
                    EmbeddingModel = embeddingsOptions.Value.Model,
                    EmbeddingDimensions = embeddingsOptions.Value.Dimensions,
                    NotionSyncStatus = "completed",
                    NotionSyncedAt = DateTimeOffset.UtcNow
                },
                cancellationToken);

            result.Imported++;
        }

        logger.LogInformation(
            "Backfill do Notion concluido. Processadas: {Processed}; importadas: {Imported}; duplicadas ignoradas: {SkippedDuplicates}",
            result.Processed,
            result.Imported,
            result.SkippedDuplicates);

        return result;
    }

    private static ProfessionalEntryStructuredDto MapRowToStructuredEntry(
        Dictionary<string, string> row,
        List<string> warnings)
    {
        var title = GetValue(row, "Título");
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new InvalidOperationException("Linha do CSV sem valor em 'Título'.");
        }

        var summary = NullIfEmpty(GetValue(row, "Resumo"));
        var project = NullIfEmpty(GetValue(row, "Projeto"));
        var impact = NullIfEmpty(GetValue(row, "Impacto"));
        var bullets = NullIfEmpty(GetValue(row, "Bullets Currículo"));
        var ideas = NullIfEmpty(GetValue(row, "Ideias LinkedIn"));
        var type = NullIfEmpty(GetValue(row, "Tipo"));
        var tags = ParseList(GetValue(row, "Tags"));
        var technologies = ParseList(GetValue(row, "Tecnologias"));

        if (!string.IsNullOrWhiteSpace(type))
        {
            tags = tags
                .Append(type)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(NormalizeListValue)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        var occurredAt = ParseOccurredAt(GetValue(row, "Data"), title, warnings);
        var content = BuildContent(title, summary, impact, bullets, ideas, project, type, technologies, tags);

        return new ProfessionalEntryStructuredDto
        {
            Title = title.Trim(),
            Content = content,
            Summary = summary,
            Project = project,
            OccurredAt = occurredAt,
            Technologies = technologies,
            Tags = tags
        };
    }

    private static string BuildContent(
        string title,
        string? summary,
        string? impact,
        string? bullets,
        string? ideas,
        string? project,
        string? type,
        string[] technologies,
        string[] tags)
    {
        var sections = new List<string>
        {
            $"titulo: {title.Trim()}"
        };

        if (!string.IsNullOrWhiteSpace(project))
        {
            sections.Add($"projeto: {project.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            sections.Add($"tipo: {type.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(summary))
        {
            sections.Add($"resumo: {summary.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(impact))
        {
            sections.Add($"impacto: {impact.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(bullets))
        {
            sections.Add($"bullets_curriculo: {NormalizeMultiline(bullets)}");
        }

        if (!string.IsNullOrWhiteSpace(ideas))
        {
            sections.Add($"ideias_linkedin: {NormalizeMultiline(ideas)}");
        }

        if (technologies.Length > 0)
        {
            sections.Add($"tecnologias: {string.Join(", ", technologies)}");
        }

        if (tags.Length > 0)
        {
            sections.Add($"tags: {string.Join(", ", tags)}");
        }

        return string.Join("\n", sections);
    }

    private static DateTimeOffset? ParseOccurredAt(string value, string title, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces,
                out var parsedOffset))
        {
            return parsedOffset;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsedDate))
        {
            return new DateTimeOffset(parsedDate, TimeSpan.Zero);
        }

        warnings.Add($"Nao foi possivel interpretar a data '{value}' para o titulo '{title}'.");
        return null;
    }

    private static string[] ParseList(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(NormalizeListValue)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

    private static string NormalizeListValue(string value)
    {
        var normalized = Regex.Replace(value.Trim().ToLowerInvariant(), @"\s+", "-");
        return normalized.Trim('-');
    }

    private static string NormalizeMultiline(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim();

    private static string GetValue(Dictionary<string, string> row, string key) =>
        row.TryGetValue(key, out var value) ? value : string.Empty;

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyList<Dictionary<string, string>> ReadRows(string csvPath)
    {
        using var parser = new TextFieldParser(csvPath);
        parser.SetDelimiters(",");
        parser.HasFieldsEnclosedInQuotes = true;

        if (parser.EndOfData)
        {
            return [];
        }

        var headers = parser.ReadFields()
            ?? throw new InvalidOperationException("Nao foi possivel ler o cabecalho do CSV exportado do Notion.");

        ValidateHeaders(headers);

        var rows = new List<Dictionary<string, string>>();
        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields();
            if (fields is null || fields.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Length; i++)
            {
                row[headers[i]] = i < fields.Length ? fields[i] : string.Empty;
            }

            rows.Add(row);
        }

        return rows;
    }

    private static void ValidateHeaders(string[] headers)
    {
        var missing = SupportedColumns
            .Where(expected => !headers.Contains(expected, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"CSV exportado do Notion nao contem as colunas esperadas: {string.Join(", ", missing)}.");
        }
    }

    private static async Task<string> ResolveCsvPathAsync(string inputPath, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(inputPath);
        if (string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase))
        {
            return inputPath;
        }

        if (!string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Informe um arquivo .csv ou .zip exportado do Notion.");
        }

        var extractRoot = Path.Combine(
            Path.GetTempPath(),
            "career-vault-notion-import",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(extractRoot);

        await ExtractZipRecursivelyAsync(inputPath, extractRoot, cancellationToken);
        var csvFiles = Directory
            .EnumerateFiles(extractRoot, "*.csv", System.IO.SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (csvFiles.Length == 0)
        {
            throw new InvalidOperationException("Nenhum CSV encontrado no ZIP exportado do Notion.");
        }

        var prioritized = csvFiles.FirstOrDefault(path => !path.EndsWith("_all.csv", StringComparison.OrdinalIgnoreCase))
            ?? csvFiles[0];

        return prioritized;
    }

    private static async Task ExtractZipRecursivelyAsync(string zipPath, string destinationDirectory, CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var destinationPath = Path.Combine(destinationDirectory, entry.FullName);
            var directory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            await using (var source = entry.Open())
            await using (var target = File.Create(destinationPath))
            {
                await source.CopyToAsync(target, cancellationToken);
            }

            if (destinationPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                var nestedDirectory = Path.Combine(
                    Path.GetDirectoryName(destinationPath) ?? destinationDirectory,
                    Path.GetFileNameWithoutExtension(destinationPath));
                Directory.CreateDirectory(nestedDirectory);
                await ExtractZipRecursivelyAsync(destinationPath, nestedDirectory, cancellationToken);
            }
        }
    }
}
