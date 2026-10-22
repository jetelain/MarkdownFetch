using MarkdownFetch.Core.Models;

namespace MarkdownFetch.Core.Ports;

/// <summary>
/// Port for communicating with the markitdown API service.
/// </summary>
public interface IMarkitdownClient
{
    Task<bool> IsReadyAsync(string apiUrl, CancellationToken cancellationToken = default);

    Task<ConversionResult> ConvertAsync(string apiUrl, DownloadedResource resource, TimeSpan timeout, CancellationToken cancellationToken = default);
}
