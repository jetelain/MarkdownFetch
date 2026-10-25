using System.Net;
using System.Text;
using MarkdownFetch.Core.Models;
using MarkdownFetch.Core.Ports;
using MarkdownFetch.Core.UseCases;

namespace MarkdownFetch.Tests.Core;

public class FetchMarkdownUseCaseTests
{
    private class FakeResourceDownloader : IResourceDownloader
    {
        public Func<Uri, TimeSpan, CancellationToken, Task<DownloadedResource>>? OnDownload { get; set; }

        public Task<DownloadedResource> DownloadAsync(Uri url, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            if (OnDownload != null)
            {
                return OnDownload(url, timeout, cancellationToken);
            }

            var stream = new MemoryStream(Encoding.UTF8.GetBytes("<html>test</html>"));
            return Task.FromResult(new DownloadedResource(stream, "test.html", "text/html"));
        }
    }

    private class FakeNativeMarkdownConverter : INativeMarkdownConverter
    {
        public Func<DownloadedResource, CancellationToken, Task<ConversionResult>>? OnConvert { get; set; }

        public Task<ConversionResult> ConvertAsync(DownloadedResource resource, CancellationToken cancellationToken = default)
        {
            if (OnConvert != null)
            {
                return OnConvert(resource, cancellationToken);
            }

            return Task.FromResult(ConversionResult.Success("# Test Markdown"));
        }
    }

    private class FakeMarkitdownClient : IMarkitdownClient
    {
        public bool Ready { get; set; } = true;
        public Func<string, DownloadedResource, TimeSpan, CancellationToken, Task<ConversionResult>>? OnConvert { get; set; }

        public Task<bool> IsReadyAsync(string apiUrl, CancellationToken cancellationToken = default) =>
            Task.FromResult(Ready);

        public Task<ConversionResult> ConvertAsync(string apiUrl, DownloadedResource resource, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            if (OnConvert != null)
            {
                return OnConvert(apiUrl, resource, timeout, cancellationToken);
            }

            return Task.FromResult(ConversionResult.Success("# Test API Markdown"));
        }
    }

    private class FakeOutputWriter : IOutputWriter
    {
        public string? WrittenMarkdown { get; private set; }
        public string? TargetOutputPath { get; private set; }

        public Task WriteAsync(string markdown, string? outputPath, CancellationToken cancellationToken = default)
        {
            WrittenMarkdown = markdown;
            TargetOutputPath = outputPath;
            return Task.CompletedTask;
        }
    }

    private class FakeConsoleView : IConsoleView
    {
        public List<string> Messages { get; } = new();
        public List<string> Successes { get; } = new();
        public List<string> Errors { get; } = new();

        public void WriteMessage(string message) => Messages.Add(message);
        public void WriteSuccess(string message) => Successes.Add(message);
        public void WriteError(string message) => Errors.Add(message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenApiNotReady_Returns1AndPrintsErrorWithDockerHint()
    {
        var downloader = new FakeResourceDownloader();
        var nativeConverter = new FakeNativeMarkdownConverter();
        var client = new FakeMarkitdownClient { Ready = false };
        var writer = new FakeOutputWriter();
        var console = new FakeConsoleView();

        var useCase = new FetchMarkdownUseCase(downloader, nativeConverter, client, writer, console);
        var options = new MarkdownFetchOptions
        {
            Url = new Uri("https://example.com"),
            Engine = ConversionEngine.Api,
            ApiUrl = "http://127.0.0.1:8490"
        };

        var result = await useCase.ExecuteAsync(options);

        Assert.Equal(1, result);
        Assert.Contains(console.Errors, e => e.Contains("Markitdown API is not reachable"));
        Assert.Contains(console.Errors, e => e.Contains("docker run"));
    }

    [Fact]
    public async Task ExecuteAsync_NativeEngine_DoesNotCheckApiReady()
    {
        var downloader = new FakeResourceDownloader();
        var nativeConverter = new FakeNativeMarkdownConverter();
        var client = new FakeMarkitdownClient { Ready = false }; // API is NOT ready
        var writer = new FakeOutputWriter();
        var console = new FakeConsoleView();

        var useCase = new FetchMarkdownUseCase(downloader, nativeConverter, client, writer, console);
        var options = new MarkdownFetchOptions
        {
            Url = new Uri("https://example.com"),
            Engine = ConversionEngine.Native
        };

        var result = await useCase.ExecuteAsync(options);

        Assert.Equal(0, result);
        Assert.Equal("# Test Markdown", writer.WrittenMarkdown);
        Assert.Empty(console.Errors);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDownloadFails_Returns1AndPrintsError()
    {
        var downloader = new FakeResourceDownloader
        {
            OnDownload = (_, _, _) => throw new HttpRequestException("404 Not Found", null, HttpStatusCode.NotFound)
        };
        var nativeConverter = new FakeNativeMarkdownConverter();
        var client = new FakeMarkitdownClient();
        var writer = new FakeOutputWriter();
        var console = new FakeConsoleView();

        var useCase = new FetchMarkdownUseCase(downloader, nativeConverter, client, writer, console);
        var options = new MarkdownFetchOptions
        {
            Url = new Uri("https://example.com/missing")
        };

        var result = await useCase.ExecuteAsync(options);

        Assert.Equal(1, result);
        Assert.Contains(console.Errors, e => e.Contains("Unable to download"));
        Assert.Contains(console.Errors, e => e.Contains("404"));
    }

    [Fact]
    public async Task ExecuteAsync_WhenNativeFileTypeNotSupported_Returns1AndPrintsUnsupportedMessage()
    {
        var downloader = new FakeResourceDownloader();
        var nativeConverter = new FakeNativeMarkdownConverter
        {
            OnConvert = (_, _) => Task.FromResult(ConversionResult.Failure("Unsupported", 422))
        };
        var client = new FakeMarkitdownClient();
        var writer = new FakeOutputWriter();
        var console = new FakeConsoleView();

        var useCase = new FetchMarkdownUseCase(downloader, nativeConverter, client, writer, console);
        var options = new MarkdownFetchOptions
        {
            Url = new Uri("https://example.com/unsupported.bin"),
            Engine = ConversionEngine.Native
        };

        var result = await useCase.ExecuteAsync(options);

        Assert.Equal(1, result);
        Assert.Contains(console.Errors, e => e.Contains("File type is not supported. Try using --use-api or curl instead."));
    }

    [Fact]
    public async Task ExecuteAsync_WhenApiFileTypeNotSupported_Returns1AndPrintsUnsupportedMessage()
    {
        var downloader = new FakeResourceDownloader();
        var nativeConverter = new FakeNativeMarkdownConverter();
        var client = new FakeMarkitdownClient
        {
            OnConvert = (_, _, _, _) => Task.FromResult(ConversionResult.Failure("Unsupported", 422))
        };
        var writer = new FakeOutputWriter();
        var console = new FakeConsoleView();

        var useCase = new FetchMarkdownUseCase(downloader, nativeConverter, client, writer, console);
        var options = new MarkdownFetchOptions
        {
            Url = new Uri("https://example.com/unsupported.bin"),
            Engine = ConversionEngine.Api
        };

        var result = await useCase.ExecuteAsync(options);

        Assert.Equal(1, result);
        Assert.Contains(console.Errors, e => e.Contains("File type is not supported. Try curl instead."));
    }

    [Fact]
    public async Task ExecuteAsync_WhenConversionFails_Returns1AndPrintsErrorMessage()
    {
        var downloader = new FakeResourceDownloader();
        var nativeConverter = new FakeNativeMarkdownConverter
        {
            OnConvert = (_, _) => Task.FromResult(ConversionResult.Failure("Internal conversion failure", 500))
        };
        var client = new FakeMarkitdownClient();
        var writer = new FakeOutputWriter();
        var console = new FakeConsoleView();

        var useCase = new FetchMarkdownUseCase(downloader, nativeConverter, client, writer, console);
        var options = new MarkdownFetchOptions
        {
            Url = new Uri("https://example.com/file")
        };

        var result = await useCase.ExecuteAsync(options);

        Assert.Equal(1, result);
        Assert.Contains(console.Errors, e => e.Contains("Internal conversion failure"));
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuccessWithoutOutputPath_WritesToStdoutAndReturns0()
    {
        var downloader = new FakeResourceDownloader();
        var nativeConverter = new FakeNativeMarkdownConverter();
        var client = new FakeMarkitdownClient();
        var writer = new FakeOutputWriter();
        var console = new FakeConsoleView();

        var useCase = new FetchMarkdownUseCase(downloader, nativeConverter, client, writer, console);
        var options = new MarkdownFetchOptions
        {
            Url = new Uri("https://example.com")
        };

        var result = await useCase.ExecuteAsync(options);

        Assert.Equal(0, result);
        Assert.Equal("# Test Markdown", writer.WrittenMarkdown);
        Assert.Null(writer.TargetOutputPath);
        Assert.Empty(console.Successes);
        Assert.Empty(console.Errors);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuccessWithOutputPath_WritesToFileAndPrintsSuccess()
    {
        var downloader = new FakeResourceDownloader();
        var nativeConverter = new FakeNativeMarkdownConverter();
        var client = new FakeMarkitdownClient();
        var writer = new FakeOutputWriter();
        var console = new FakeConsoleView();

        var useCase = new FetchMarkdownUseCase(downloader, nativeConverter, client, writer, console);
        var options = new MarkdownFetchOptions
        {
            Url = new Uri("https://example.com"),
            OutputPath = "docs/output.md"
        };

        var result = await useCase.ExecuteAsync(options);

        Assert.Equal(0, result);
        Assert.Equal("# Test Markdown", writer.WrittenMarkdown);
        Assert.Equal("docs/output.md", writer.TargetOutputPath);
        Assert.Contains(console.Successes, s => s.Contains("Saved to: docs/output.md"));
        Assert.Empty(console.Errors);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMarkdownContainsRelativeLinks_ResolvesToAbsoluteLinks()
    {
        var downloader = new FakeResourceDownloader
        {
            OnDownload = (url, _, _) =>
            {
                var stream = new MemoryStream(Encoding.UTF8.GetBytes("<html></html>"));
                return Task.FromResult(new DownloadedResource(stream, "index.html", "text/html", url));
            }
        };
        var nativeConverter = new FakeNativeMarkdownConverter
        {
            OnConvert = (res, _) => Task.FromResult(ConversionResult.Success("[Get started](/signup)\n![Logo](/logo.png)"))
        };
        var client = new FakeMarkitdownClient();
        var writer = new FakeOutputWriter();
        var console = new FakeConsoleView();

        var useCase = new FetchMarkdownUseCase(downloader, nativeConverter, client, writer, console);
        var options = new MarkdownFetchOptions
        {
            Url = new Uri("https://ollama.com/")
        };

        var result = await useCase.ExecuteAsync(options);

        Assert.Equal(0, result);
        Assert.Contains("[Get started](https://ollama.com/signup)", writer.WrittenMarkdown);
        Assert.Contains("![Logo](https://ollama.com/logo.png)", writer.WrittenMarkdown);
    }

    [Fact]
    public async Task ExecuteAsync_ApiEngine_ResolvesRelativeLinksToAbsoluteLinks()
    {
        var downloader = new FakeResourceDownloader
        {
            OnDownload = (url, _, _) =>
            {
                var stream = new MemoryStream(Encoding.UTF8.GetBytes("<html></html>"));
                return Task.FromResult(new DownloadedResource(stream, "index.html", "text/html", url));
            }
        };
        var nativeConverter = new FakeNativeMarkdownConverter();
        var client = new FakeMarkitdownClient
        {
            OnConvert = (_, _, _, _) => Task.FromResult(ConversionResult.Success("# Title\n\n[Docs](/docs)\n![Img](/img.svg)"))
        };
        var writer = new FakeOutputWriter();
        var console = new FakeConsoleView();

        var useCase = new FetchMarkdownUseCase(downloader, nativeConverter, client, writer, console);
        var options = new MarkdownFetchOptions
        {
            Url = new Uri("https://example.com/site/"),
            Engine = ConversionEngine.Api
        };

        var result = await useCase.ExecuteAsync(options);

        Assert.Equal(0, result);
        Assert.Contains("[Docs](https://example.com/docs)", writer.WrittenMarkdown);
        Assert.Contains("![Img](https://example.com/img.svg)", writer.WrittenMarkdown);
    }
}

