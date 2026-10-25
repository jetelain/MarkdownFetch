using System.Diagnostics.CodeAnalysis;
using AngleSharp.Html.Dom;
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

        if (options.CleanHtml || options.BaseUrl != null)
        {
            try
            {
                var document = _htmlParser.ParseDocument(rawHtml);

                if (options.CleanHtml && options.IncludeTitle && !string.IsNullOrWhiteSpace(document.Title))
                {
                    var hasH1 = document.QuerySelector("h1") != null;
                    if (!hasH1)
                    {
                        title = document.Title.Trim();
                    }
                }

                if (options.CleanHtml)
                {
                    // Strip non-content / styling / script tags
                    var unwanted = document.QuerySelectorAll("script, style, noscript, svg, nav, footer, aside");
                    foreach (var el in unwanted)
                    {
                        el.Remove();
                    }
                }

                ResolveRelativeUrls(document, options.BaseUrl);

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

    private static void ResolveRelativeUrls(IHtmlDocument document, Uri? baseUrl)
    {
        var effectiveBaseUrl = baseUrl;

        var baseElement = document.QuerySelector("base[href]");
        if (baseElement != null)
        {
            var baseHref = baseElement.GetAttribute("href");
            if (!string.IsNullOrWhiteSpace(baseHref))
            {
                if (effectiveBaseUrl != null)
                {
                    if (Uri.TryCreate(effectiveBaseUrl, baseHref, out var resolvedBase))
                    {
                        effectiveBaseUrl = resolvedBase;
                    }
                }
                else if (Uri.TryCreate(baseHref, UriKind.Absolute, out var resolvedBase))
                {
                    effectiveBaseUrl = resolvedBase;
                }
            }
        }

        if (effectiveBaseUrl == null)
        {
            return;
        }

        var linkElements = document.QuerySelectorAll("a[href], area[href]");
        foreach (var el in linkElements)
        {
            var href = el.GetAttribute("href");
            if (TryResolveUrl(effectiveBaseUrl, href, out var absoluteUrl))
            {
                el.SetAttribute("href", absoluteUrl);
            }
        }

        var srcElements = document.QuerySelectorAll("img[src], audio[src], video[src], source[src]");
        foreach (var el in srcElements)
        {
            var src = el.GetAttribute("src");
            if (TryResolveUrl(effectiveBaseUrl, src, out var absoluteUrl))
            {
                el.SetAttribute("src", absoluteUrl);
            }
        }
    }

    public static bool TryResolveUrl(Uri baseUrl, string? relativeUrl, [NotNullWhen(true)] out string? resolvedUrl)
    {
        resolvedUrl = null;

        if (string.IsNullOrWhiteSpace(relativeUrl))
        {
            return false;
        }

        var trimmed = relativeUrl.Trim();

        // Preserve in-page anchor fragments (e.g. #section-heading)
        if (trimmed.StartsWith('#'))
        {
            return false;
        }

        // Ignore common non-HTTP URI schemes
        if (trimmed.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("tel:", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var existingUri))
        {
            if (existingUri.Scheme == Uri.UriSchemeHttp || existingUri.Scheme == Uri.UriSchemeHttps)
            {
                // Already an absolute HTTP/HTTPS URL
                return false;
            }
        }

        if (!Uri.TryCreate(baseUrl, trimmed, out var resolvedUri))
        {
            return false;
        }

        resolvedUrl = resolvedUri.AbsoluteUri;
        return true;
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
