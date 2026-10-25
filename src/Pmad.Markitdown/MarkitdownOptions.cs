namespace Pmad.Markitdown;

/// <summary>
/// Options for markdown conversion.
/// </summary>
public sealed class MarkitdownOptions
{
    /// <summary>
    /// If true, extracts document title if available.
    /// </summary>
    public bool IncludeTitle { get; set; } = true;

    /// <summary>
    /// If true, strips navigation, script, and style tags from HTML documents.
    /// </summary>
    public bool CleanHtml { get; set; } = true;

    /// <summary>
    /// Optional base URL used to resolve relative links and images into absolute URLs.
    /// </summary>
    public Uri? BaseUrl { get; set; }

    public MarkitdownOptions()
    {
    }

    public MarkitdownOptions(Uri? baseUrl)
    {
        BaseUrl = baseUrl;
    }

    public MarkitdownOptions(string? baseUrl)
    {
        if (!string.IsNullOrWhiteSpace(baseUrl) && Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            BaseUrl = uri;
        }
    }
}

