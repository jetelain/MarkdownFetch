namespace MarkdownFetch.Core.Models;

/// <summary>
/// Represents a downloaded web resource stream and metadata.
/// </summary>
public sealed class DownloadedResource : IAsyncDisposable, IDisposable
{
    public Stream Content { get; }
    public string FileName { get; }
    public string? ContentType { get; }
    public Uri? Url { get; }

    public DownloadedResource(Stream content, string fileName, string? contentType, Uri? url = null)
    {
        Content = content ?? throw new ArgumentNullException(nameof(content));
        FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
        ContentType = contentType;
        Url = url;
    }

    public void Dispose() => Content.Dispose();

    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

