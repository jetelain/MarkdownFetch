using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace Pmad.Markitdown.Filters;

/// <summary>
/// Filters hidden elements and invisible characters from HTML content to prevent hidden prompt injection
/// or adversarial content injection.
/// </summary>
public static class HtmlHiddenTextFilter
{
    private static readonly Regex DisplayNoneRegex = new(
        @"(?:^|;)\s*display\s*:\s*none(?:\s*!important)?(?:\s*;|\s*$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex VisibilityHiddenRegex = new(
        @"(?:^|;)\s*visibility\s*:\s*(?:hidden|collapse)(?:\s*!important)?(?:\s*;|\s*$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex OpacityZeroRegex = new(
        @"(?:^|;)\s*opacity\s*:\s*0(?:\.0+)?(?:\s*!important)?(?:\s*;|\s*$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex FontSizeZeroRegex = new(
        @"(?:^|;)\s*font-size\s*:\s*0(?:px|pt|em|rem|%|vw|vh)?(?:\s*!important)?(?:\s*;|\s*$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ColorTransparentRegex = new(
        @"(?:^|;)\s*color\s*:\s*(?:transparent|rgba\([^)]*,\s*0(?:\.0+)?\s*\)|hsla\([^)]*,\s*0(?:\.0+)?\s*\)|#00000000|#0000)(?:\s*!important)?(?:\s*;|\s*$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TextIndentNegativeRegex = new(
        @"(?:^|;)\s*text-indent\s*:\s*-\s*[0-9]{3,}px(?:\s*!important)?(?:\s*;|\s*$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PositionOffscreenRegex = new(
        @"(?:^|;)\s*(?:left|top|right|bottom)\s*:\s*-\s*[0-9]{4,}px(?:\s*!important)?(?:\s*;|\s*$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ClipZeroRegex = new(
        @"(?:^|;)\s*clip\s*:\s*rect\(\s*0(?:px)?(?:\s*,\s*|\s+)0(?:px)?(?:\s*,\s*|\s+)0(?:px)?(?:\s*,\s*|\s+)0(?:px)?\s*\)(?:\s*!important)?(?:\s*;|\s*$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ClipPathZeroRegex = new(
        @"(?:^|;)\s*clip-path\s*:\s*(?:inset\(100%\)|circle\(0(?:px)?\))(?:\s*!important)?(?:\s*;|\s*$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex InvisibleCharsRegex = new(
        @"[\u200B-\u200D\uFEFF\u2060]",
        RegexOptions.Compiled);

    /// <summary>
    /// Removes elements from the document DOM that are hidden via attributes or inline CSS styles.
    /// </summary>
    public static void RemoveHiddenElements(IHtmlDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        // 1. Remove elements with HTML attributes indicating hidden content
        var attrHiddenElements = document.QuerySelectorAll("[hidden], [aria-hidden='true'], input[type='hidden'], template");
        foreach (var el in attrHiddenElements)
        {
            el.Remove();
        }

        // 2. Remove elements with inline CSS styles that hide content
        var styledElements = document.QuerySelectorAll("[style]");
        foreach (var el in styledElements)
        {
            if (el.ParentElement == null)
            {
                // Already detached by an ancestor removal
                continue;
            }

            var style = el.GetAttribute("style");
            if (!string.IsNullOrWhiteSpace(style) && IsHiddenStyle(style))
            {
                el.Remove();
            }
        }
    }

    /// <summary>
    /// Determines whether an inline CSS style string contains rules that make content invisible or hidden.
    /// </summary>
    public static bool IsHiddenStyle(string style)
    {
        if (string.IsNullOrWhiteSpace(style))
        {
            return false;
        }

        return DisplayNoneRegex.IsMatch(style)
            || VisibilityHiddenRegex.IsMatch(style)
            || OpacityZeroRegex.IsMatch(style)
            || FontSizeZeroRegex.IsMatch(style)
            || ColorTransparentRegex.IsMatch(style)
            || TextIndentNegativeRegex.IsMatch(style)
            || PositionOffscreenRegex.IsMatch(style)
            || ClipZeroRegex.IsMatch(style)
            || ClipPathZeroRegex.IsMatch(style);
    }

    /// <summary>
    /// Strips zero-width and invisible unicode formatting characters often used in steganographic prompt injection.
    /// </summary>
    public static string StripInvisibleCharacters(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return InvisibleCharsRegex.Replace(text, string.Empty);
    }

    /// <summary>
    /// Parses the provided HTML, removes hidden elements, strips invisible characters, and returns the sanitized HTML.
    /// </summary>
    public static string FilterHtml(string rawHtml)
    {
        if (string.IsNullOrWhiteSpace(rawHtml))
        {
            return string.Empty;
        }

        var parser = new HtmlParser();
        var document = parser.ParseDocument(rawHtml);
        RemoveHiddenElements(document);

        var result = document.Body != null ? document.Body.InnerHtml : document.DocumentElement.OuterHtml;
        return StripInvisibleCharacters(result);
    }

    /// <summary>
    /// Reads an HTML stream, filters hidden elements and invisible characters, and returns a new memory stream with the clean HTML.
    /// </summary>
    public static async Task<Stream> FilterHtmlStreamAsync(Stream stream, Encoding? encoding = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var enc = encoding ?? Encoding.UTF8;
        using var reader = new StreamReader(stream, enc, leaveOpen: true);
        var rawHtml = await reader.ReadToEndAsync(cancellationToken);

        var filtered = FilterHtml(rawHtml);
        var filteredBytes = enc.GetBytes(filtered);
        return new MemoryStream(filteredBytes);
    }
}
