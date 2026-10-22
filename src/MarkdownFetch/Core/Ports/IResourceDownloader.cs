using MarkdownFetch.Core.Models;

namespace MarkdownFetch.Core.Ports;

/// <summary>
/// Port for downloading remote web resources.
/// </summary>
public interface IResourceDownloader
{
    Task<DownloadedResource> DownloadAsync(Uri url, TimeSpan timeout, CancellationToken cancellationToken = default);
}
