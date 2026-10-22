namespace Pmad.Markitdown.Converters;

/// <summary>
/// Converts Word (.docx) documents to clean Markdown using Mammoth and HtmlToMarkdownConverter.
/// </summary>
public sealed class DocxToMarkdownConverter : IFormatConverter
{
    private readonly HtmlToMarkdownConverter _htmlConverter;

    public DocxToMarkdownConverter(HtmlToMarkdownConverter htmlConverter)
    {
        _htmlConverter = htmlConverter ?? throw new ArgumentNullException(nameof(htmlConverter));
    }

    public bool CanHandle(string? extension, string? mediaType)
    {
        if (!string.IsNullOrEmpty(extension))
        {
            var ext = extension.TrimStart('.').ToLowerInvariant();
            if (ext is "docx") return true;
        }

        if (!string.IsNullOrEmpty(mediaType))
        {
            var mime = mediaType.Split(';')[0].Trim().ToLowerInvariant();
            if (mime is "application/vnd.openxmlformats-officedocument.wordprocessingml.document") return true;
        }

        return false;
    }

    public async Task<string> ConvertAsync(Stream stream, MarkitdownOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        MemoryStream? memoryStream = null;
        Stream effectiveStream = stream;

        try
        {
            if (!effectiveStream.CanSeek)
            {
                memoryStream = new MemoryStream();
                await effectiveStream.CopyToAsync(memoryStream, cancellationToken);
                memoryStream.Position = 0;
                effectiveStream = memoryStream;
            }
            else if (effectiveStream.Position != 0)
            {
                effectiveStream.Position = 0;
            }

            var converter = new Mammoth.DocumentConverter();
            var result = converter.ConvertToHtml(effectiveStream);
            var html = result.Value ?? string.Empty;

            return _htmlConverter.ConvertHtml(html, options);
        }
        finally
        {
            memoryStream?.Dispose();
        }
    }
}
