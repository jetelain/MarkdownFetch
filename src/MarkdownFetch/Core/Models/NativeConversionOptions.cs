namespace MarkdownFetch.Core.Models;

/// <summary>
/// Options passed to the native markdown converter.
/// </summary>
public sealed record NativeConversionOptions
{
    /// <summary>
    /// If true, filters hidden elements and invisible characters from HTML content.
    /// </summary>
    public bool FilterHiddenText { get; init; } = true;
}
