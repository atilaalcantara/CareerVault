namespace CareerVault.Api.Models;

public sealed class ResumeGenerationResult
{
    public required ResumeGenerationPreviewResponseDto Preview { get; init; }
    public required byte[] PdfBytes { get; init; }
    public required string FileName { get; init; }
}
