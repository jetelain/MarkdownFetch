using System.Text.RegularExpressions;
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

        // Host / Host:Port pattern
        string hostPattern = rule;
        int? expectedPort = null;

        var colonIndex = rule.LastIndexOf(':');
        if (colonIndex > 0 && !rule.EndsWith(':') && !rule.Contains('/'))
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

        // Exact host match
        return url.Host.Equals(hostPattern, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesWildcard(string input, string pattern, bool ignoreCase)
    {
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace(@"\*", ".*")
            .Replace(@"\?", ".") + "$";

        var options = RegexOptions.CultureInvariant;
        if (ignoreCase)
        {
            options |= RegexOptions.IgnoreCase;
        }

        return Regex.IsMatch(input, regexPattern, options);
    }
}
