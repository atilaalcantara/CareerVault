namespace CareerVault.Api.Services;

public static class SupportedFileTypes
{
    private static readonly Dictionary<string, string> MimeTypesByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".m4a"] = "audio/mp4",
        [".mp3"] = "audio/mpeg",
        [".wav"] = "audio/wav",
        [".ogg"] = "audio/ogg",
        [".oga"] = "audio/ogg",
        [".webm"] = "audio/webm",
        [".pdf"] = "application/pdf",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg"
    };

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "audio/mp4",
        "audio/mpeg",
        "audio/wav",
        "audio/ogg",
        "audio/webm",
        "application/pdf",
        "image/png",
        "image/jpeg"
    };

    public static bool TryResolve(string fileName, string? contentType, out string mimeType)
    {
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            var normalized = NormalizeMimeType(contentType);
            if (AllowedMimeTypes.Contains(normalized))
            {
                mimeType = normalized;
                return true;
            }
        }

        var extension = Path.GetExtension(fileName);
        if (MimeTypesByExtension.TryGetValue(extension, out var resolvedByExtension))
        {
            mimeType = resolvedByExtension;
            return true;
        }

        mimeType = string.Empty;
        return false;
    }

    public static string DescribeAllowedTypes() =>
        "Tipos aceitos: audio gravado no Telegram, .m4a, .mp3, .wav, .ogg, .oga, .webm; imagens .png, .jpg, .jpeg; PDF .pdf.";

    private static string NormalizeMimeType(string mimeType) =>
        mimeType.ToLowerInvariant() switch
        {
            "audio/x-m4a" => "audio/mp4",
            "audio/wave" => "audio/wav",
            "audio/x-wav" => "audio/wav",
            "audio/oga" => "audio/ogg",
            "audio/opus" => "audio/ogg",
            "audio/x-opus+ogg" => "audio/ogg",
            "image/jpg" => "image/jpeg",
            "application/x-pdf" => "application/pdf",
            _ => mimeType
        };
}
