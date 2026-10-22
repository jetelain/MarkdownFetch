namespace Pmad.Markitdown.Converters;

/// <summary>
/// Interface for format-specific markdown converters.
/// </summary>
public interface IFormatConverter
{
    /// <summary>
    /// Determines whether this converter can handle the given extension or media type.
    /// </summary>
    bool CanHandle(string? extension, string? mediaType);

    /// <summary>
    /// Converts the given stream content to Markdown.
    /// </summary>
    Task<string> ConvertAsync(Stream stream, MarkitdownOptions options, CancellationToken cancellationToken = default);
}
