using System.CommandLine;
using MarkdownFetch.Core.Models;
using MarkdownFetch.Core.Ports;
using MarkdownFetch.Core.UseCases;
using MarkdownFetch.Infrastructure.Safety;
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
        public void WriteWarning(string message) { }
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
        Assert.Contains("--filter-hidden-text", options);
        Assert.Contains("--no-filter-hidden-text", options);
        Assert.Contains("--detect-prompt-injection", options);
        Assert.Contains("--no-detect-prompt-injection", options);
        Assert.Contains("--url-whitelist", options);
        Assert.Contains("--safety-config", options);
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

    [Fact]
    public async Task CreateRootCommand_SafetyDefaults_AreEnabled()
    {
        MarkdownFetchOptions? capturedOptions = null;

        var rootCommand = CommandLineConfiguration.CreateRootCommand(() =>
            new InspectingUseCase(opt => capturedOptions = opt));

        var exitCode = await rootCommand.Parse("https://example.com/page").InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.NotNull(capturedOptions);
        Assert.True(capturedOptions.FilterHiddenText);
        Assert.True(capturedOptions.DetectPromptInjection);
        Assert.Empty(capturedOptions.UrlWhitelist);
        Assert.Equal(PromptInjectionAction.Block, capturedOptions.PromptInjectionAction);
    }

    [Fact]
    public async Task CreateRootCommand_WithNoFilterHiddenText_SetsFilterHiddenTextFalse()
    {
        MarkdownFetchOptions? capturedOptions = null;

        var rootCommand = CommandLineConfiguration.CreateRootCommand(() =>
            new InspectingUseCase(opt => capturedOptions = opt));

        var exitCode = await rootCommand.Parse("https://example.com/page --no-filter-hidden-text").InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.NotNull(capturedOptions);
        Assert.False(capturedOptions.FilterHiddenText);
    }

    [Fact]
    public async Task CreateRootCommand_WithFilterHiddenTextFalse_SetsFilterHiddenTextFalse()
    {
        MarkdownFetchOptions? capturedOptions = null;

        var rootCommand = CommandLineConfiguration.CreateRootCommand(() =>
            new InspectingUseCase(opt => capturedOptions = opt));

        var exitCode = await rootCommand.Parse("https://example.com/page --filter-hidden-text false").InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.NotNull(capturedOptions);
        Assert.False(capturedOptions.FilterHiddenText);
    }

    [Fact]
    public async Task CreateRootCommand_WithNoDetectPromptInjection_SetsDetectPromptInjectionFalse()
    {
        MarkdownFetchOptions? capturedOptions = null;

        var rootCommand = CommandLineConfiguration.CreateRootCommand(() =>
            new InspectingUseCase(opt => capturedOptions = opt));

        var exitCode = await rootCommand.Parse("https://example.com/page --no-detect-prompt-injection").InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.NotNull(capturedOptions);
        Assert.False(capturedOptions.DetectPromptInjection);
    }

    [Fact]
    public async Task CreateRootCommand_WithDetectPromptInjectionFalse_SetsDetectPromptInjectionFalse()
    {
        MarkdownFetchOptions? capturedOptions = null;

        var rootCommand = CommandLineConfiguration.CreateRootCommand(() =>
            new InspectingUseCase(opt => capturedOptions = opt));

        var exitCode = await rootCommand.Parse("https://example.com/page --detect-prompt-injection false").InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.NotNull(capturedOptions);
        Assert.False(capturedOptions.DetectPromptInjection);
    }

    [Fact]
    public async Task CreateRootCommand_WithUrlWhitelist_ParsesWhitelistEntries()
    {
        MarkdownFetchOptions? capturedOptions = null;

        var rootCommand = CommandLineConfiguration.CreateRootCommand(() =>
            new InspectingUseCase(opt => capturedOptions = opt));

        var exitCode = await rootCommand.Parse("https://example.com/page --url-whitelist *.github.com learn.microsoft.com").InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.NotNull(capturedOptions);
        Assert.Equal(2, capturedOptions.UrlWhitelist.Count);
        Assert.Contains("*.github.com", capturedOptions.UrlWhitelist);
        Assert.Contains("learn.microsoft.com", capturedOptions.UrlWhitelist);
    }

    [Fact]
    public async Task CreateRootCommand_WhenSafetyConfigForcesGuards_CannotBeDisabledByCli()
    {
        MarkdownFetchOptions? capturedOptions = null;

        var mockLoader = new FakeSafetyConfigurationLoader(new SafetyPolicy
        {
            UrlWhitelist = new[] { "*.github.com" },
            FilterHiddenText = true,
            ForceFilterHiddenText = true,
            DetectPromptInjection = true,
            ForceDetectPromptInjection = true
        });

        var rootCommand = CommandLineConfiguration.CreateRootCommand(
            () => new InspectingUseCase(opt => capturedOptions = opt),
            mockLoader);

        // Attempt to turn off both guards via CLI
        var exitCode = await rootCommand.Parse("https://example.com/page --no-filter-hidden-text --no-detect-prompt-injection").InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.NotNull(capturedOptions);
        // Both guards must still be TRUE because the safety policy forces them!
        Assert.True(capturedOptions.FilterHiddenText);
        Assert.True(capturedOptions.DetectPromptInjection);
        Assert.Single(capturedOptions.SystemUrlWhitelist);
        Assert.Contains("*.github.com", capturedOptions.SystemUrlWhitelist);
    }

    private class FakeSafetyConfigurationLoader : ISafetyConfigurationLoader
    {
        private readonly SafetyPolicy _policy;

        public FakeSafetyConfigurationLoader(SafetyPolicy policy)
        {
            _policy = policy;
        }

        public SafetyPolicy LoadPolicy(string? explicitConfigPath = null) => _policy;
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
