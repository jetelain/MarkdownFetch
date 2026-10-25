using System.Net;
using System.Text;
using MarkdownFetch.Core.Services;
using MarkdownFetch.Infrastructure.Http;

namespace MarkdownFetch.Tests.Infrastructure;

public class HttpResourceDownloaderTests
{
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        public List<Uri> RequestedUris { get; } = new();
        public Func<HttpRequestMessage, HttpResponseMessage>? ResponseFactory { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedUris.Add(request.RequestUri!);
            if (ResponseFactory != null)
            {
                return Task.FromResult(ResponseFactory(request));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html>content</html>", Encoding.UTF8, "text/html")
            });
        }
    }

    [Fact]
    public async Task DownloadAsync_NormalDownload_ReturnsDownloadedResource()
    {
        var handler = new MockHttpMessageHandler
        {
            ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("Hello world", Encoding.UTF8, "text/plain")
            }
        };

        var httpClient = new HttpClient(handler);
        var downloader = new HttpResourceDownloader(httpClient);

        var resource = await downloader.DownloadAsync(new Uri("https://example.com/file.txt"), TimeSpan.FromSeconds(5));

        Assert.NotNull(resource);
        Assert.Equal("file.txt", resource.FileName);
        Assert.Equal("https://example.com/file.txt", resource.Url?.ToString());

        using var reader = new StreamReader(resource.Content);
        var content = await reader.ReadToEndAsync();
        Assert.Equal("Hello world", content);
    }

    [Fact]
    public async Task DownloadAsync_RedirectWithinWhitelist_FollowsRedirectAndReturnsResource()
    {
        var handler = new MockHttpMessageHandler
        {
            ResponseFactory = req =>
            {
                if (req.RequestUri == new Uri("https://example.com/start"))
                {
                    var redirect = new HttpResponseMessage(HttpStatusCode.Redirect);
                    redirect.Headers.Location = new Uri("https://example.com/end");
                    return redirect;
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("Final content", Encoding.UTF8, "text/plain")
                };
            }
        };

        var httpClient = new HttpClient(handler);
        var downloader = new HttpResourceDownloader(httpClient, new UrlWhitelistValidator());
        var whitelist = new[] { "example.com" };

        var resource = await downloader.DownloadAsync(
            new Uri("https://example.com/start"),
            TimeSpan.FromSeconds(5),
            whitelist);

        Assert.Equal("https://example.com/end", resource.Url?.ToString());
        Assert.Equal(2, handler.RequestedUris.Count);
        Assert.Equal(new Uri("https://example.com/start"), handler.RequestedUris[0]);
        Assert.Equal(new Uri("https://example.com/end"), handler.RequestedUris[1]);
    }

    [Fact]
    public async Task DownloadAsync_RedirectToNonWhitelistedHost_ThrowsBeforeIssuingRequest()
    {
        var handler = new MockHttpMessageHandler
        {
            ResponseFactory = req =>
            {
                if (req.RequestUri == new Uri("https://allowed.com/start"))
                {
                    var redirect = new HttpResponseMessage(HttpStatusCode.Redirect);
                    redirect.Headers.Location = new Uri("http://169.254.169.254/latest/meta-data");
                    return redirect;
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("Should not be fetched", Encoding.UTF8, "text/plain")
                };
            }
        };

        var httpClient = new HttpClient(handler);
        var downloader = new HttpResourceDownloader(httpClient, new UrlWhitelistValidator());
        var whitelist = new[] { "allowed.com" };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            downloader.DownloadAsync(
                new Uri("https://allowed.com/start"),
                TimeSpan.FromSeconds(5),
                whitelist));

        Assert.Contains("not permitted by URL whitelist policy", ex.Message);
        Assert.Contains("169.254.169.254", ex.Message);

        // Security assertion: NO request was ever sent to the metadata endpoint!
        Assert.Single(handler.RequestedUris);
        Assert.Equal(new Uri("https://allowed.com/start"), handler.RequestedUris[0]);
        Assert.DoesNotContain(handler.RequestedUris, u => u.Host == "169.254.169.254");
    }

    [Fact]
    public async Task DownloadAsync_TooManyRedirects_ThrowsInvalidOperationException()
    {
        var handler = new MockHttpMessageHandler
        {
            ResponseFactory = req =>
            {
                var redirect = new HttpResponseMessage(HttpStatusCode.Redirect);
                redirect.Headers.Location = new Uri(req.RequestUri!, $"/loop_{Guid.NewGuid()}");
                return redirect;
            }
        };

        var httpClient = new HttpClient(handler);
        var downloader = new HttpResourceDownloader(httpClient);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            downloader.DownloadAsync(new Uri("https://example.com/loop"), TimeSpan.FromSeconds(5)));

        Assert.Contains("Too many redirects", ex.Message);
    }
}
