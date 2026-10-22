namespace Pmad.Markitdown.Converters;

/// <summary>
/// Converts plain text, markdown, or unstructured textual data by normalizing line breaks.
/// </summary>
public sealed class PlainTextConverter : IFormatConverter
{
    public bool CanHandle(string? extension, string? mediaType)
    {
        if (!string.IsNullOrEmpty(extension))
        {
            var ext = extension.TrimStart('.').ToLowerInvariant();
            if (ext is "txt" or "text" or "md" or "markdown" or "log" or "json" or "xml" or "yaml" or "yml" or "csv" or "tsv")
            {
                return true;
            }
        }

        if (!string.IsNullOrEmpty(mediaType))
        {
            var mime = mediaType.Split(';')[0].Trim().ToLowerInvariant();
            if (mime.StartsWith("text/", StringComparison.Ordinal) ||
                mime is "application/json" or "application/xml" or "text/plain")
            {
                return true;
            }
        }

        return false;
    }

    public async Task<string> ConvertAsync(Stream stream, MarkitdownOptions options, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        var text = await reader.ReadToEndAsync(cancellationToken);
        return text.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
    }
}
