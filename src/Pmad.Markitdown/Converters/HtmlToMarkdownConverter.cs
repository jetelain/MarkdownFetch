using AngleSharp.Html.Parser;
using ReverseMarkdown;

namespace Pmad.Markitdown.Converters;

/// <summary>
/// Converts HTML documents to clean Markdown using AngleSharp and ReverseMarkdown.
/// </summary>
public sealed class HtmlToMarkdownConverter : IFormatConverter
{
    private readonly ReverseMarkdown.Converter _converter;
    private readonly HtmlParser _htmlParser;

    public HtmlToMarkdownConverter()
    {
        var config = new ReverseMarkdown.Config
        {
            GithubFlavored = true
        };
        config.Formatting.RemoveComments = true;
        config.Tables.WithoutHeaderRow = Config.TableWithoutHeaderRowHandlingOption.EmptyRow;
        config.Tags.Unknown = Config.UnknownTagsOption.Bypass;
        _converter = new ReverseMarkdown.Converter(config);
        _htmlParser = new HtmlParser();
    }

    public bool CanHandle(string? extension, string? mediaType)
    {
        if (!string.IsNullOrEmpty(extension))
        {
            var ext = extension.TrimStart('.').ToLowerInvariant();
            if (ext is "html" or "htm" or "xhtml") return true;
        }

        if (!string.IsNullOrEmpty(mediaType))
        {
            var mime = mediaType.Split(';')[0].Trim().ToLowerInvariant();
            if (mime is "text/html" or "application/xhtml+xml") return true;
        }

        return false;
    }

    public async Task<string> ConvertAsync(Stream stream, MarkitdownOptions options, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        var rawHtml = await reader.ReadToEndAsync(cancellationToken);
        return ConvertHtml(rawHtml, options);
    }

    public string ConvertHtml(string rawHtml, MarkitdownOptions options)
    {
        if (string.IsNullOrWhiteSpace(rawHtml))
        {
            return string.Empty;
        }

        string htmlToConvert = rawHtml;

        string? title = null;

        if (options.CleanHtml)
        {
            try
            {
                var document = _htmlParser.ParseDocument(rawHtml);

                if (options.IncludeTitle && !string.IsNullOrWhiteSpace(document.Title))
                {
                    var hasH1 = document.QuerySelector("h1") != null;
                    if (!hasH1)
                    {
                        title = document.Title.Trim();
                    }
                }

                // Strip non-content / styling / script tags
                var unwanted = document.QuerySelectorAll("script, style, noscript, svg, nav, footer, aside");
                foreach (var el in unwanted)
                {
                    el.Remove();
                }

                if (document.Body != null)
                {
                    htmlToConvert = document.Body.InnerHtml;
                }
            }
            catch
            {
                htmlToConvert = rawHtml;
            }
        }

        var markdown = _converter.Convert(htmlToConvert);
        var cleaned = CleanMarkdown(markdown);

        if (!string.IsNullOrWhiteSpace(title) && !cleaned.StartsWith("# "))
        {
            return $"# {title}\n\n{cleaned}".Trim();
        }

        return cleaned;
    }

    private static string CleanMarkdown(string markdown)
    {
        if (string.IsNullOrEmpty(markdown)) return string.Empty;

        markdown = markdown.Replace("\r\n", "\n").Replace('\r', '\n');

        while (markdown.Contains("\n\n\n"))
        {
            markdown = markdown.Replace("\n\n\n", "\n\n");
        }

        return markdown.Trim();
    }
}
