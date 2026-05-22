using CareerVault.Api.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CareerVault.Api.Services;

public sealed class QuestPdfResumeRenderer : IResumePdfRenderer
{
    public byte[] Render(ResumeProfileDto profile, TailoredResumeDraftDto draft)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(24);
                page.DefaultTextStyle(text => text.FontSize(10).FontFamily(Fonts.Arial));

                page.Content().Column(column =>
                {
                    column.Spacing(12);
                    ComposeHeader(column, profile, draft);
                    ComposeSummary(column, profile, draft);
                    ComposeSkills(column, draft);
                    ComposeExperience(column, draft);
                    ComposeEducation(column, profile, draft);
                    ComposeSimpleSection(column, "Certificacoes", draft.CertificationItems);
                    ComposeSimpleSection(column, "Projetos", draft.ProjectItems);
                });
            });
        }).GeneratePdf();
    }

    private static void ComposeHeader(ColumnDescriptor column, ResumeProfileDto profile, TailoredResumeDraftDto draft)
    {
        column.Item().Column(header =>
        {
            header.Spacing(4);
            header.Item().Text(profile.FullName).FontSize(20).SemiBold();

            var headline = string.IsNullOrWhiteSpace(draft.Headline) ? profile.Headline : draft.Headline;
            if (!string.IsNullOrWhiteSpace(headline))
            {
                header.Item().Text(headline).FontSize(11);
            }

            var contactLine = string.Join(
                " | ",
                new[]
                {
                    profile.Location,
                    profile.Phone,
                    profile.Email,
                    profile.LinkedInUrl,
                    profile.GithubUrl,
                    profile.PortfolioUrl
                }.Where(value => !string.IsNullOrWhiteSpace(value)));

            if (!string.IsNullOrWhiteSpace(contactLine))
            {
                header.Item().Text(contactLine).FontSize(9).FontColor(Colors.Grey.Darken2);
            }
        });
    }

    private static void ComposeSummary(ColumnDescriptor column, ResumeProfileDto profile, TailoredResumeDraftDto draft)
    {
        var summary = string.IsNullOrWhiteSpace(draft.ProfessionalSummary)
            ? profile.BaseSummary
            : draft.ProfessionalSummary;

        if (string.IsNullOrWhiteSpace(summary))
        {
            return;
        }

        column.Item().Column(section =>
        {
            ComposeSectionTitle(section, "Resumo Profissional");
            section.Item().Text(summary);
        });
    }

    private static void ComposeSkills(ColumnDescriptor column, TailoredResumeDraftDto draft)
    {
        if (draft.CoreSkills.Length == 0)
        {
            return;
        }

        column.Item().Column(section =>
        {
            ComposeSectionTitle(section, "Competencias-Chave");
            section.Item().Text(string.Join(" | ", draft.CoreSkills));
        });
    }

    private static void ComposeExperience(ColumnDescriptor column, TailoredResumeDraftDto draft)
    {
        if (draft.ExperienceItems.Length == 0)
        {
            return;
        }

        column.Item().Column(section =>
        {
            ComposeSectionTitle(section, "Experiencia Relevante");
            section.Spacing(8);

            foreach (var item in draft.ExperienceItems)
            {
                section.Item().Column(experience =>
                {
                    experience.Spacing(2);

                    var header = string.Join(
                        " | ",
                        new[] { item.Title, item.Company, item.Project, item.Period }
                            .Where(value => !string.IsNullOrWhiteSpace(value)));

                    experience.Item().Text(header).SemiBold();

                    foreach (var bullet in item.Bullets.Where(value => !string.IsNullOrWhiteSpace(value)))
                    {
                        experience.Item().Row(row =>
                        {
                            row.ConstantItem(10).Text("•");
                            row.RelativeItem().Text(bullet);
                        });
                    }
                });
            }
        });
    }

    private static void ComposeEducation(ColumnDescriptor column, ResumeProfileDto profile, TailoredResumeDraftDto draft)
    {
        var educationItems = MergeEducation(profile, draft);
        if (educationItems.Length == 0)
        {
            return;
        }

        column.Item().Column(section =>
        {
            ComposeSectionTitle(section, "Formacao");
            section.Spacing(6);

            foreach (var item in educationItems)
            {
                section.Item().Column(education =>
                {
                    education.Item().Text($"{item.Degree} - {item.Institution}".Trim(' ', '-')).SemiBold();

                    if (!string.IsNullOrWhiteSpace(item.Details))
                    {
                        education.Item().Text(item.Details);
                    }
                });
            }
        });
    }

    private static ResumeEducationItemDto[] MergeEducation(ResumeProfileDto profile, TailoredResumeDraftDto draft)
    {
        var merged = new List<ResumeEducationItemDto>();
        var knownKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in profile.EducationItems.Concat(draft.EducationItems))
        {
            if (string.IsNullOrWhiteSpace(item.Institution) && string.IsNullOrWhiteSpace(item.Degree))
            {
                continue;
            }

            var key = $"{item.Degree}|{item.Institution}".Trim();
            if (!knownKeys.Add(key))
            {
                continue;
            }

            merged.Add(item);
        }

        return merged.ToArray();
    }

    private static void ComposeSimpleSection(ColumnDescriptor column, string title, ResumeSimpleItemDto[] items)
    {
        if (items.Length == 0)
        {
            return;
        }

        column.Item().Column(section =>
        {
            ComposeSectionTitle(section, title);
            section.Spacing(4);

            foreach (var item in items)
            {
                section.Item().Column(block =>
                {
                    if (!string.IsNullOrWhiteSpace(item.Title))
                    {
                        block.Item().Text(item.Title).SemiBold();
                    }

                    if (!string.IsNullOrWhiteSpace(item.Details))
                    {
                        block.Item().Text(item.Details);
                    }
                });
            }
        });
    }

    private static void ComposeSectionTitle(ColumnDescriptor column, string title)
    {
        column.Item().PaddingBottom(2).Text(title).FontSize(11).SemiBold();
        column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
    }
}
