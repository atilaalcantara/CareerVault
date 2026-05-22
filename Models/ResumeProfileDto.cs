namespace CareerVault.Api.Models;

public sealed class ResumeProfileDto
{
    public string FullName { get; init; } = string.Empty;
    public string Headline { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string LinkedInUrl { get; init; } = string.Empty;
    public string GithubUrl { get; init; } = string.Empty;
    public string PortfolioUrl { get; init; } = string.Empty;
    public string BaseSummary { get; init; } = string.Empty;
    public ResumeEducationItemDto[] EducationItems { get; init; } = [];
}
