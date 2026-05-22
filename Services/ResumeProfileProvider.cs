using CareerVault.Api.Models;
using CareerVault.Api.Options;
using Microsoft.Extensions.Options;

namespace CareerVault.Api.Services;

public sealed class ResumeProfileProvider(IOptions<ResumeProfileOptions> options)
{
    public ResumeProfileDto GetProfile() =>
        new()
        {
            FullName = options.Value.FullName,
            Headline = options.Value.Headline,
            Email = options.Value.Email,
            Phone = options.Value.Phone,
            Location = options.Value.Location,
            LinkedInUrl = options.Value.LinkedInUrl,
            GithubUrl = options.Value.GithubUrl,
            PortfolioUrl = options.Value.PortfolioUrl,
            BaseSummary = options.Value.BaseSummary,
            EducationItems = options.Value.EducationItems
                .Where(item => !string.IsNullOrWhiteSpace(item.Institution) || !string.IsNullOrWhiteSpace(item.Degree))
                .Select(item => new ResumeEducationItemDto
                {
                    Institution = item.Institution,
                    Degree = item.Degree,
                    Details = item.Details
                })
                .ToArray()
        };
}
