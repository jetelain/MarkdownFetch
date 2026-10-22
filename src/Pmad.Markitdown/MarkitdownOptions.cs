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
}
