using System.Net;
using System.Net.Http.Headers;
using MarkdownFetch.Core.Models;
using MarkdownFetch.Core.Ports;
using MarkdownFetch.Core.Services;
using MarkdownFetch.Infrastructure.Common;

namespace MarkdownFetch.Infrastructure.Http;

/// <summary>
/// HTTP implementation of IResourceDownloader using HttpClient with safe redirect validation.
/// </summary>
public sealed class HttpResourceDownloader : IResourceDownloader
{
    private readonly HttpClient _httpClient;
    private readonly IUrlWhitelistValidator _urlWhitelistValidator;

    public HttpResourceDownloader(
        HttpClient? httpClient = null,
        IUrlWhitelistValidator? urlWhitelistValidator = null)
    {
        _urlWhitelistValidator = urlWhitelistValidator ?? new UrlWhitelistValidator();

        if (httpClient != null)
        {
            _httpClient = httpClient;
        }
        else
        {
            var handler = new SocketsHttpHandler
            {
                AllowAutoRedirect = false
            };
            _httpClient = new HttpClient(handler);
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MarkdownFetch", "1.0"));
        }
    }

    public Task<DownloadedResource> DownloadAsync(Uri url, TimeSpan timeout, CancellationToken cancellationToken = default) =>
        DownloadAsync(url, timeout, null, cancellationToken);

    public async Task<DownloadedResource> DownloadAsync(
        Uri url,
        TimeSpan timeout,
        IReadOnlyList<string>? urlWhitelist,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        var currentUrl = url;
        const int maxRedirects = 10;
        var redirectCount = 0;

        while (true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, currentUrl);
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

            if (IsRedirect(response.StatusCode) && response.Headers.Location != null)
            {
                response.Dispose();
                redirectCount++;
                if (redirectCount > maxRedirects)
                {
                    throw new InvalidOperationException($"Too many redirects (limit: {maxRedirects}).");
                }

                var nextUrl = new Uri(currentUrl, response.Headers.Location);
                if (!_urlWhitelistValidator.IsAllowed(nextUrl, urlWhitelist))
                {
                    throw new InvalidOperationException($"Redirected URL '{nextUrl}' is not permitted by URL whitelist policy.");
                }

                currentUrl = nextUrl;
                continue;
            }

            response.EnsureSuccessStatusCode();

            var finalUrl = response.RequestMessage?.RequestUri ?? currentUrl;
            if (!_urlWhitelistValidator.IsAllowed(finalUrl, urlWhitelist))
            {
                response.Dispose();
                throw new InvalidOperationException($"Redirected URL '{finalUrl}' is not permitted by URL whitelist policy.");
            }

            var fileName = FileNameResolver.ResolveFileName(response.Content.Headers, finalUrl);
            var contentType = response.Content.Headers.ContentType?.ToString();
            var contentStream = await response.Content.ReadAsStreamAsync(cts.Token);

            return new DownloadedResource(contentStream, fileName, contentType, finalUrl);
        }
    }

    private static bool IsRedirect(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.MovedPermanently or
                      HttpStatusCode.Found or
                      HttpStatusCode.SeeOther or
                      HttpStatusCode.TemporaryRedirect or
                      HttpStatusCode.PermanentRedirect;
}
