namespace MarkdownFetch.Core.Ports;

/// <summary>
/// Port for evaluating whether a requested or redirected URL is permitted by whitelist policies.
/// </summary>
public interface IUrlWhitelistValidator
{
    /// <summary>
    /// Checks whether the specified URL is allowed by the provided whitelist.
    /// If the whitelist is null or empty, all URLs are allowed by default.
    /// </summary>
    /// <param name="url">The URL to test.</param>
    /// <param name="whitelist">The allowed patterns or domain names.</param>
    /// <returns>True if allowed, otherwise false.</returns>
    bool IsAllowed(Uri url, IReadOnlyList<string>? whitelist);
}
