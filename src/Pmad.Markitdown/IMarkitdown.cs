namespace Pmad.Markitdown;

/// <summary>
/// Entry point interface for converting documents to Markdown.
/// </summary>
public interface IMarkitdown
{
    /// <summary>
    /// Converts a stream to Markdown given an optional file extension or MIME media type.
    /// </summary>
    Task<string> ConvertAsync(Stream stream, string? extensionOrMediaType = null, MarkitdownOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts a local file to Markdown.
    /// </summary>
    Task<string> ConvertFileAsync(string filePath, MarkitdownOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts text content to Markdown.
    /// </summary>
    Task<string> ConvertTextAsync(string text, string? extensionOrMediaType = "html", MarkitdownOptions? options = null, CancellationToken cancellationToken = default);
}
