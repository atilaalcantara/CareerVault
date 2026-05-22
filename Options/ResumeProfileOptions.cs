namespace CareerVault.Api.Options;

public sealed class ResumeProfileOptions
{
    public const string SectionName = "ResumeProfile";

    public string FullName { get; init; } = string.Empty;
    public string Headline { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string LinkedInUrl { get; init; } = string.Empty;
    public string GithubUrl { get; init; } = string.Empty;
    public string PortfolioUrl { get; init; } = string.Empty;
    public string BaseSummary { get; init; } = string.Empty;
    public ResumeProfileEducationOptions[] EducationItems { get; init; } = [];
}

public sealed class ResumeProfileEducationOptions
{
    public string Institution { get; init; } = string.Empty;
    public string Degree { get; init; } = string.Empty;
    public string Details { get; init; } = string.Empty;
}
