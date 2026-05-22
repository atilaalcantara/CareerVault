namespace CareerVault.Api.Models;

public sealed class NotionCsvImportResult
{
    public int Processed { get; set; }
    public int Imported { get; set; }
    public int SkippedDuplicates { get; set; }
    public List<string> Warnings { get; } = [];
}
