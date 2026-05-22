using CareerVault.Api.Models;

namespace CareerVault.Api.Services;

public interface IResumePdfRenderer
{
    byte[] Render(ResumeProfileDto profile, TailoredResumeDraftDto draft);
}
