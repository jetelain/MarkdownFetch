using MarkdownFetch.Core.Models;
using MarkdownFetch.Core.Ports;
using MimeMapping;
using Pmad.Markitdown;

namespace MarkdownFetch.Infrastructure.Converters;

/// <summary>
/// Native in-process markdown converter implementation using Pmad.Markitdown.
/// </summary>
public sealed class NativeMarkdownConverter : INativeMarkdownConverter
{
    private readonly IMarkitdown _markitdown;

    public NativeMarkdownConverter(IMarkitdown? markitdown = null)
    {
        _markitdown = markitdown ?? new Pmad.Markitdown.Markitdown();
    }

    public async Task<ConversionResult> ConvertAsync(DownloadedResource resource, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resource);

        var extension = ResolveExtension(resource);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return ConversionResult.Failure("Unable to determine file type for native conversion. Try using --use-api or curl instead.", 422);
        }

        try
        {
            var markdown = await _markitdown.ConvertAsync(resource.Content, extension, cancellationToken: cancellationToken);
            return ConversionResult.Success(markdown);
        }
        catch (NotSupportedException)
        {
            return ConversionResult.Failure($"Native conversion does not support '{extension}'. Try using --use-api or curl instead.", 422);
        }
        catch (Exception ex)
        {
            return ConversionResult.Failure($"Native conversion error: {ex.Message}", 500);
        }
    }

    public static string ResolveExtension(DownloadedResource resource)
    {
        var ext = Path.GetExtension(resource.FileName);
        if (!string.IsNullOrWhiteSpace(ext))
        {
            return ext.ToLowerInvariant();
        }

        if (!string.IsNullOrWhiteSpace(resource.ContentType))
        {
            var mime = resource.ContentType.Split(';')[0].Trim().ToLowerInvariant();
            var mapped = mime switch
            {
                "text/html" or "application/xhtml+xml" => ".html",
                "application/pdf" => ".pdf",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
                "text/plain" => ".txt",
                "application/json" or "text/json" => ".json",
                "application/xml" or "text/xml" => ".xml",
                _ => null
            };

            if (mapped != null)
            {
                return mapped;
            }

            var extensions = MimeUtility.GetExtensions(mime);
            if (extensions?.Length > 0)
            {
                var first = extensions[0];
                return first.StartsWith('.') ? first.ToLowerInvariant() : "." + first.ToLowerInvariant();
            }
        }

        return string.Empty;
    }
}
