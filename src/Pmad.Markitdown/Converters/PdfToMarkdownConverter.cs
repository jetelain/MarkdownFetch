using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Pmad.Markitdown.Converters;

/// <summary>
/// Converts PDF documents to clean Markdown using PdfPig.
/// </summary>
public sealed partial class PdfToMarkdownConverter : IFormatConverter
{
    public bool CanHandle(string? extension, string? mediaType)
    {
        if (!string.IsNullOrEmpty(extension))
        {
            var ext = extension.TrimStart('.').ToLowerInvariant();
            if (ext is "pdf") return true;
        }

        if (!string.IsNullOrEmpty(mediaType))
        {
            var mime = mediaType.Split(';')[0].Trim().ToLowerInvariant();
            if (mime is "application/pdf" or "application/x-pdf") return true;
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

            using var document = PdfDocument.Open(effectiveStream);
            var sb = new StringBuilder();

            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var pageText = ContentOrderTextExtractor.GetText(page);
                if (string.IsNullOrWhiteSpace(pageText))
                {
                    pageText = page.Text;
                }

                var normalizedText = NormalizeExtractedText(pageText);
                if (!string.IsNullOrWhiteSpace(normalizedText))
                {
                    if (sb.Length > 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine();
                    }
                    sb.Append(normalizedText);
                }
            }

            return sb.ToString().Trim();
        }
        finally
        {
            memoryStream?.Dispose();
        }
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    private static string NormalizeExtractedText(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var sb = new StringBuilder();

        foreach (var rawLine in lines)
        {
            var line = WhitespaceRegex().Replace(rawLine.Trim(), " ");
            if (!string.IsNullOrEmpty(line))
            {
                sb.AppendLine(line);
            }
        }

        return sb.ToString().Trim();
    }
}
