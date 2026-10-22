using System.CommandLine;
using MarkdownFetch.Core.Models;
using MarkdownFetch.Core.Ports;
using MarkdownFetch.Core.UseCases;
using MarkdownFetch.Presentation.CommandLine;

namespace MarkdownFetch.Tests.Presentation;

public class CommandLineConfigurationTests
{
    private class DummyDownloader : IResourceDownloader
    {
        public Task<DownloadedResource> DownloadAsync(Uri url, TimeSpan timeout, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private class DummyClient : IMarkitdownClient
    {
        public Task<bool> IsReadyAsync(string apiUrl, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<ConversionResult> ConvertAsync(string apiUrl, DownloadedResource resource, TimeSpan timeout, CancellationToken cancellationToken = default) =>
            Task.FromResult(ConversionResult.Success(""));
    }

    private class DummyWriter : IOutputWriter
    {
        public Task WriteAsync(string markdown, string? outputPath, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private class DummyConsole : IConsoleView
    {
        public void WriteMessage(string message) { }
        public void WriteSuccess(string message) { }
        public void WriteError(string message) { }
    }

    [Fact]
    public void CreateRootCommand_HasExpectedArgumentsAndOptions()
    {
        var rootCommand = CommandLineConfiguration.CreateRootCommand(() =>
            new FetchMarkdownUseCase(new DummyDownloader(), new DummyClient(), new DummyWriter(), new DummyConsole()));

        var arguments = rootCommand.Arguments.Select(a => a.Name).ToList();
        var options = rootCommand.Options.Select(o => o.Name).Concat(rootCommand.Options.SelectMany(o => o.Aliases)).ToList();

        Assert.Contains("url", arguments);
        Assert.Contains("output", arguments);
        Assert.Contains("--output", options);
        Assert.Contains("-o", options);
        Assert.Contains("--engine", options);
        Assert.Contains("--use-api", options);
        Assert.Contains("--api-url", options);
        Assert.Contains("-a", options);
        Assert.Contains("--timeout", options);
        Assert.Contains("-t", options);
    }

    [Fact]
    public async Task CreateRootCommand_DefaultsToNativeEngine()
    {
        MarkdownFetchOptions? capturedOptions = null;

        var rootCommand = CommandLineConfiguration.CreateRootCommand(() =>
            new InspectingUseCase(opt => capturedOptions = opt));

        var exitCode = await rootCommand.Parse("https://example.com/page").InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.NotNull(capturedOptions);
        Assert.Equal(ConversionEngine.Native, capturedOptions.Engine);
    }

    [Fact]
    public async Task CreateRootCommand_WithUseApiFlag_SetsApiEngine()
    {
        MarkdownFetchOptions? capturedOptions = null;

        var rootCommand = CommandLineConfiguration.CreateRootCommand(() =>
            new InspectingUseCase(opt => capturedOptions = opt));

        var exitCode = await rootCommand.Parse("https://example.com/page --use-api").InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.NotNull(capturedOptions);
        Assert.Equal(ConversionEngine.Api, capturedOptions.Engine);
    }

    [Fact]
    public async Task CreateRootCommand_WithEngineApi_SetsApiEngine()
    {
        MarkdownFetchOptions? capturedOptions = null;

        var rootCommand = CommandLineConfiguration.CreateRootCommand(() =>
            new InspectingUseCase(opt => capturedOptions = opt));

        var exitCode = await rootCommand.Parse("https://example.com/page --engine api").InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.NotNull(capturedOptions);
        Assert.Equal(ConversionEngine.Api, capturedOptions.Engine);
    }

    [Fact]
    public async Task CreateRootCommand_WithInvalidEngine_Returns1()
    {
        var invoked = false;

        var rootCommand = CommandLineConfiguration.CreateRootCommand(() =>
            new InspectingUseCase(_ => invoked = true));

        var exitCode = await rootCommand.Parse("https://example.com/page --engine invalid").InvokeAsync();

        Assert.Equal(1, exitCode);
        Assert.False(invoked);
    }

    [Fact]
    public async Task CreateRootCommand_ParsesArgumentsAndInvokesUseCase()
    {
        MarkdownFetchOptions? capturedOptions = null;

        var rootCommand = CommandLineConfiguration.CreateRootCommand(() =>
            new InspectingUseCase(opt => capturedOptions = opt));

        var exitCode = await rootCommand.Parse("https://example.com/page -o mypage.md --api-url http://custom:9000 -t 45").InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.NotNull(capturedOptions);
        Assert.Equal(new Uri("https://example.com/page"), capturedOptions.Url);
        Assert.Equal("mypage.md", capturedOptions.OutputPath);
        Assert.Equal("http://custom:9000", capturedOptions.ApiUrl);
        Assert.Equal(TimeSpan.FromSeconds(45), capturedOptions.Timeout);
    }

    [Fact]
    public async Task CreateRootCommand_WithPositionalOutput_SetsOutputPath()
    {
        MarkdownFetchOptions? capturedOptions = null;

        var rootCommand = CommandLineConfiguration.CreateRootCommand(() =>
            new InspectingUseCase(opt => capturedOptions = opt));

        var exitCode = await rootCommand.Parse("https://example.com/page result.md").InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.NotNull(capturedOptions);
        Assert.Equal(new Uri("https://example.com/page"), capturedOptions.Url);
        Assert.Equal("result.md", capturedOptions.OutputPath);
    }

    [Fact]
    public async Task CreateRootCommand_WithInvalidUrl_Returns1AndDoesNotInvokeUseCase()
    {
        var invoked = false;

        var rootCommand = CommandLineConfiguration.CreateRootCommand(() =>
            new InspectingUseCase(_ => invoked = true));

        var exitCode = await rootCommand.Parse("not-a-valid-url").InvokeAsync();

        Assert.Equal(1, exitCode);
        Assert.False(invoked);
    }

    [Fact]
    public async Task CreateRootCommand_WithoutUrl_Returns1AndShowsHelp()
    {
        var invoked = false;

        var rootCommand = CommandLineConfiguration.CreateRootCommand(() =>
            new InspectingUseCase(_ => invoked = true));

        var exitCode = await rootCommand.Parse("").InvokeAsync();

        Assert.NotEqual(0, exitCode);
        Assert.False(invoked);
    }

    private class InspectingUseCase : IFetchMarkdownUseCase
    {
        private readonly Action<MarkdownFetchOptions> _inspector;

        public InspectingUseCase(Action<MarkdownFetchOptions> inspector)
        {
            _inspector = inspector;
        }

        public Task<int> ExecuteAsync(MarkdownFetchOptions options, CancellationToken cancellationToken = default)
        {
            _inspector(options);
            return Task.FromResult(0);
        }
    }
}
