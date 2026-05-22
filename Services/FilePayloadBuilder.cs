using CareerVault.Api.Models;
using CareerVault.Api.Options;
using Microsoft.Extensions.Options;

namespace CareerVault.Api.Services;

public sealed class FilePayloadBuilder(IOptions<GeminiOptions> options)
{
    public async Task<GeminiPart> BuildAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length <= 0)
        {
            throw new InvalidOperationException($"Arquivo vazio: {file.FileName}");
        }

        if (file.Length > options.Value.MaxRequestBytes)
        {
            throw new InvalidOperationException($"Arquivo {file.FileName} excede o limite de {options.Value.MaxRequestBytes} bytes.");
        }

        var mimeType = ResolveMimeType(file);

        await using var stream = file.OpenReadStream();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);

        return new GeminiPart
        {
            InlineData = new GeminiInlineData
            {
                MimeType = mimeType,
                Data = Convert.ToBase64String(memory.ToArray())
            }
        };
    }

    public GeminiPart Build(byte[] bytes, string fileName, string? contentType)
    {
        if (bytes.Length <= 0)
        {
            throw new InvalidOperationException($"Arquivo vazio: {fileName}");
        }

        if (bytes.Length > options.Value.MaxRequestBytes)
        {
            throw new InvalidOperationException($"Arquivo {fileName} excede o limite de {options.Value.MaxRequestBytes} bytes.");
        }

        var mimeType = ResolveMimeType(fileName, contentType);

        return new GeminiPart
        {
            InlineData = new GeminiInlineData
            {
                MimeType = mimeType,
                Data = Convert.ToBase64String(bytes)
            }
        };
    }

    private static string ResolveMimeType(IFormFile file)
        => ResolveMimeType(file.FileName, file.ContentType);

    private static string ResolveMimeType(string fileName, string? contentType)
    {
        if (SupportedFileTypes.TryResolve(fileName, contentType, out var mimeType))
        {
            return mimeType;
        }

        throw new InvalidOperationException($"Arquivo nao suportado: {fileName}. {SupportedFileTypes.DescribeAllowedTypes()}");
    }
}
