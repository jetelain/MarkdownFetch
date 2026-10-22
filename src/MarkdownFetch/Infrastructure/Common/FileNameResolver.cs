using System.Net.Http.Headers;
using System.Net.Mime;
using MimeMapping;

namespace MarkdownFetch.Infrastructure.Common;

/// <summary>
/// Utility to extract or determine appropriate file names and extensions from HTTP response headers and URLs.
/// </summary>
public static class FileNameResolver
{
    public static string ResolveFileName(HttpContentHeaders headers, Uri? sourceUri = null)
    {
        ArgumentNullException.ThrowIfNull(headers);

        string? fileName = null;

        if (headers.ContentDisposition != null)
        {
            fileName = headers.ContentDisposition.FileName?.Trim('"');
        }
        else if (headers.Contains("Content-Disposition"))
        {
            var headerValues = headers.GetValues("Content-Disposition");
            var contentDisposition = string.Join(", ", headerValues);
            if (!string.IsNullOrEmpty(contentDisposition))
            {
                try
                {
                    fileName = new ContentDisposition(contentDisposition).FileName?.Trim('"');
                }
                catch
                {
                    // Ignore parsing errors for malformed Content-Disposition headers
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(fileName))
        {
            return Path.GetFileName(fileName);
        }

        var mediaType = headers.ContentType?.MediaType;

        // If mediaType is generic octet-stream or not provided, check URL path first
        if (sourceUri != null && sourceUri.IsAbsoluteUri)
        {
            var path = sourceUri.AbsolutePath.TrimEnd('/');
            var candidate = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(candidate) && Path.HasExtension(candidate))
            {
                if (string.IsNullOrWhiteSpace(mediaType) || mediaType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(mediaType))
        {
            var extensions = MimeUtility.GetExtensions(mediaType);
            if (extensions?.Length > 0)
            {
                return "file." + extensions[0].TrimStart('.');
            }
        }

        if (sourceUri != null && sourceUri.IsAbsoluteUri)
        {
            var path = sourceUri.AbsolutePath.TrimEnd('/');
            var candidate = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(candidate) && Path.HasExtension(candidate))
            {
                return candidate;
            }
        }

        return "file.html";
    }
}
