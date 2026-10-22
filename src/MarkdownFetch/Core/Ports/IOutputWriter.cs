namespace MarkdownFetch.Core.Ports;

/// <summary>
/// Port for writing converted markdown content to standard output or a file.
/// </summary>
public interface IOutputWriter
{
    Task WriteAsync(string markdown, string? outputPath, CancellationToken cancellationToken = default);
}
