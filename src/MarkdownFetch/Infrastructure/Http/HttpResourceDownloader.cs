using System.Net.Http.Headers;
using MarkdownFetch.Core.Models;
using MarkdownFetch.Core.Ports;
using MarkdownFetch.Infrastructure.Common;

namespace MarkdownFetch.Infrastructure.Http;

/// <summary>
/// HTTP implementation of IResourceDownloader using HttpClient.
/// </summary>
public sealed class HttpResourceDownloader : IResourceDownloader
{
    private readonly HttpClient _httpClient;

    public HttpResourceDownloader(HttpClient? httpClient = null)
    {
        if (httpClient != null)
        {
            _httpClient = httpClient;
        }
        else
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MarkdownFetch", "1.0"));
        }
    }

    public async Task<DownloadedResource> DownloadAsync(Uri url, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        response.EnsureSuccessStatusCode();

        var fileName = FileNameResolver.ResolveFileName(response.Content.Headers, url);
        var contentType = response.Content.Headers.ContentType?.ToString();
        var contentStream = await response.Content.ReadAsStreamAsync(cts.Token);

        return new DownloadedResource(contentStream, fileName, contentType);
    }
}
