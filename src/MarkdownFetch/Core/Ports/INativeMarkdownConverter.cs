using MarkdownFetch.Core.Models;

namespace MarkdownFetch.Core.Ports;

/// <summary>
/// Port for performing native in-process markdown conversion without external services.
/// </summary>
public interface INativeMarkdownConverter
{
    Task<ConversionResult> ConvertAsync(DownloadedResource resource, CancellationToken cancellationToken = default);
    Task<ConversionResult> ConvertAsync(DownloadedResource resource, NativeConversionOptions? options, CancellationToken cancellationToken = default);
}
