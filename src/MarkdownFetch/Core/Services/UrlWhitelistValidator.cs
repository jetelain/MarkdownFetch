using MarkdownFetch.Core.Ports;

namespace MarkdownFetch.Core.Services;

/// <summary>
/// Validates whether a URL complies with the allowed URL whitelist rules.
/// </summary>
public sealed class UrlWhitelistValidator : IUrlWhitelistValidator
{
    public bool IsAllowed(Uri url, IReadOnlyList<string>? whitelist)
    {
        ArgumentNullException.ThrowIfNull(url);

        // Disallow dangerous schemes
        if (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        // None by default: if whitelist is null or empty, all HTTP/HTTPS URLs are allowed
        if (whitelist == null || whitelist.Count == 0)
        {
            return true;
        }

        foreach (var rule in whitelist)
        {
            if (string.IsNullOrWhiteSpace(rule))
            {
                continue;
            }

            if (MatchesRule(url, rule.Trim()))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesRule(Uri url, string rule)
    {
        if (rule == "*")
        {
            return true;
        }

        // Full URL pattern with scheme (e.g. "https://example.com/api/*" or "http://localhost:8080/*")
        if (rule.Contains("://", StringComparison.Ordinal))
        {
            return MatchesWildcard(url.AbsoluteUri, rule, ignoreCase: true);
        }

        // Host + Path pattern without scheme (e.g. "example.com/api/*" or "*.example.com/docs/*")
        if (rule.Contains('/', StringComparison.Ordinal))
        {
            var hostAndPath = url.Host + url.PathAndQuery;
            var authorityAndPath = url.Authority + url.PathAndQuery;

            if (MatchesWildcard(hostAndPath, rule, ignoreCase: true) ||
                MatchesWildcard(authorityAndPath, rule, ignoreCase: true))
            {
                return true;
            }

            // Also support *.example.com/path matching apex domain example.com/path
            if (rule.StartsWith("*.", StringComparison.Ordinal))
            {
                var apexRule = rule[2..];
                if (MatchesWildcard(hostAndPath, apexRule, ignoreCase: true) ||
                    MatchesWildcard(authorityAndPath, apexRule, ignoreCase: true))
                {
                    return true;
                }
            }

            return false;
        }

        // Host / Host:Port pattern
        string hostPattern = rule;
        int? expectedPort = null;

        var colonIndex = rule.LastIndexOf(':');
        bool isPortColon = colonIndex > 0 &&
                           !rule.EndsWith(':') &&
                           (rule.IndexOf(':') == colonIndex || (rule.StartsWith('[') && colonIndex > rule.IndexOf(']')));

        if (isPortColon)
        {
            var portPart = rule[(colonIndex + 1)..];
            if (int.TryParse(portPart, out var port))
            {
                expectedPort = port;
                hostPattern = rule[..colonIndex];
            }
        }

        // If port was specified, check it
        if (expectedPort.HasValue && url.Port != expectedPort.Value)
        {
            return false;
        }

        // Host wildcard match (e.g. "*.example.com" or ".example.com")
        if (hostPattern.StartsWith("*.", StringComparison.Ordinal))
        {
            var suffix = hostPattern[2..];
            return url.Host.Equals(suffix, StringComparison.OrdinalIgnoreCase)
                || url.Host.EndsWith("." + suffix, StringComparison.OrdinalIgnoreCase);
        }

        if (hostPattern.StartsWith(".", StringComparison.Ordinal))
        {
            var suffix = hostPattern[1..];
            return url.Host.Equals(suffix, StringComparison.OrdinalIgnoreCase)
                || url.Host.EndsWith("." + suffix, StringComparison.OrdinalIgnoreCase);
        }

        if (hostPattern.Contains('*') || hostPattern.Contains('?'))
        {
            return MatchesWildcard(url.Host, hostPattern, ignoreCase: true);
        }

        // Exact host match (handling IPv6 brackets in url.Host vs rule)
        if (url.Host.Equals(hostPattern, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (url.HostNameType == UriHostNameType.IPv6)
        {
            var cleanHost = url.Host.Trim('[', ']');
            var cleanPattern = hostPattern.Trim('[', ']');
            return cleanHost.Equals(cleanPattern, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool MatchesWildcard(string input, string pattern, bool ignoreCase)
    {
        return IsWildcardMatch(input.AsSpan(), pattern.AsSpan(), ignoreCase);
    }

    private static bool IsWildcardMatch(ReadOnlySpan<char> text, ReadOnlySpan<char> pattern, bool ignoreCase)
    {
        int t = 0, p = 0;
        int starIdx = -1, matchIdx = 0;

        while (t < text.Length)
        {
            if (p < pattern.Length && (pattern[p] == '?' ||
                (ignoreCase ? char.ToUpperInvariant(text[t]) == char.ToUpperInvariant(pattern[p]) : text[t] == pattern[p])))
            {
                t++;
                p++;
            }
            else if (p < pattern.Length && pattern[p] == '*')
            {
                starIdx = p;
                matchIdx = t;
                p++;
            }
            else if (starIdx != -1)
            {
                p = starIdx + 1;
                matchIdx++;
                t = matchIdx;
            }
            else
            {
                return false;
            }
        }

        while (p < pattern.Length && pattern[p] == '*')
        {
            p++;
        }

        return p == pattern.Length;
    }
}
