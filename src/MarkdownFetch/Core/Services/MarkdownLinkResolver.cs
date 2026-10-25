using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;

namespace MarkdownFetch.Core.Services;

/// <summary>
/// Utility to resolve relative links and image sources in Markdown text to absolute URLs.
/// </summary>
public static partial class MarkdownLinkResolver
{
    [GeneratedRegex(@"(?<prefix>!?\[)(?<text>(?:[^\[\]]|(?<open>\[)|(?<-open>\]))*(?(open)(?!)))\]\((?<url><[^\s>]+>|[^\s\)\""']+)(?<title>\s+[\""'][^\""']*[\""'])?\)", RegexOptions.Compiled)]
    private static partial Regex MarkdownLinkRegex();

    /// <summary>
    /// Rewrites relative links and image sources in the provided Markdown content to absolute URLs using baseUrl.
    /// Preserves code blocks, inline code, and in-page anchor fragments.
    /// </summary>
    public static string ResolveRelativeLinks(string markdown, Uri baseUrl)
    {
        if (string.IsNullOrEmpty(markdown) || baseUrl == null)
        {
            return markdown;
        }

        var hasCrLf = markdown.Contains("\r\n");
        var normalized = markdown.Replace("\r\n", "\n");
        var lines = normalized.Split('\n');
        var result = new StringBuilder(normalized.Length + 128);

        bool inCodeBlock = false;
        string? codeFence = null;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmedStart = line.TrimStart();

            if (trimmedStart.StartsWith("```") || trimmedStart.StartsWith("~~~"))
            {
                var fencePrefix = trimmedStart.Substring(0, 3);
                if (!inCodeBlock)
                {
                    inCodeBlock = true;
                    codeFence = fencePrefix;
                }
                else if (trimmedStart.StartsWith(codeFence!))
                {
                    inCodeBlock = false;
                    codeFence = null;
                }

                result.Append(line);
                if (i < lines.Length - 1) result.Append('\n');
                continue;
            }

            if (inCodeBlock)
            {
                result.Append(line);
                if (i < lines.Length - 1) result.Append('\n');
                continue;
            }

            var processedLine = ProcessLine(line, baseUrl);
            result.Append(processedLine);
            if (i < lines.Length - 1) result.Append('\n');
        }

        var output = result.ToString();
        return hasCrLf ? output.Replace("\n", "\r\n") : output;
    }

    private static string ProcessLine(string line, Uri baseUrl)
    {
        if (!line.Contains("]("))
        {
            return line;
        }

        if (line.Contains('`'))
        {
            var parts = line.Split('`');
            var sb = new StringBuilder(line.Length + 64);
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0) sb.Append('`');
                // Even indices are outside backticks; odd indices are inside inline code
                if (i % 2 == 0)
                {
                    sb.Append(RewriteLinksInSegment(parts[i], baseUrl));
                }
                else
                {
                    sb.Append(parts[i]);
                }
            }
            return sb.ToString();
        }

        return RewriteLinksInSegment(line, baseUrl);
    }

    private static string RewriteLinksInSegment(string segment, Uri baseUrl)
    {
        return MarkdownLinkRegex().Replace(segment, match =>
        {
            var prefix = match.Groups["prefix"].Value;
            var text = match.Groups["text"].Value;
            var rawUrl = match.Groups["url"].Value;
            var title = match.Groups["title"].Value;

            bool isAngleBracketed = rawUrl.StartsWith('<') && rawUrl.EndsWith('>');
            var url = isAngleBracketed ? rawUrl.Substring(1, rawUrl.Length - 2) : rawUrl;

            if (TryResolveUrl(baseUrl, url, out var absoluteUrl))
            {
                var formattedUrl = isAngleBracketed ? $"<{absoluteUrl}>" : absoluteUrl;
                return $"{prefix}{text}]({formattedUrl}{title})";
            }

            return match.Value;
        });
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
}
