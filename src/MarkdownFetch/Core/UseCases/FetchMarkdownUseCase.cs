using MarkdownFetch.Core.Models;
using MarkdownFetch.Core.Ports;
using MarkdownFetch.Core.Services;
using Pmad.Markitdown.Filters;

namespace MarkdownFetch.Core.UseCases;

/// <summary>
/// Application use case coordinating the process of fetching a remote resource,
/// validating safety policies (URL whitelist, prompt injection, hidden text filtering),
/// converting to markdown, and writing the resulting output.
/// </summary>
public sealed class FetchMarkdownUseCase : IFetchMarkdownUseCase
{
    private readonly IResourceDownloader _downloader;
    private readonly INativeMarkdownConverter _nativeConverter;
    private readonly IMarkitdownClient _markitdownClient;
    private readonly IOutputWriter _outputWriter;
    private readonly IConsoleView _consoleView;
    private readonly IUrlWhitelistValidator _urlWhitelistValidator;
    private readonly IPromptInjectionDetector _promptInjectionDetector;

    public FetchMarkdownUseCase(
        IResourceDownloader downloader,
        INativeMarkdownConverter nativeConverter,
        IMarkitdownClient markitdownClient,
        IOutputWriter outputWriter,
        IConsoleView consoleView,
        IUrlWhitelistValidator? urlWhitelistValidator = null,
        IPromptInjectionDetector? promptInjectionDetector = null)
    {
        _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
        _nativeConverter = nativeConverter ?? throw new ArgumentNullException(nameof(nativeConverter));
        _markitdownClient = markitdownClient ?? throw new ArgumentNullException(nameof(markitdownClient));
        _outputWriter = outputWriter ?? throw new ArgumentNullException(nameof(outputWriter));
        _consoleView = consoleView ?? throw new ArgumentNullException(nameof(consoleView));
        _urlWhitelistValidator = urlWhitelistValidator ?? new UrlWhitelistValidator();
        _promptInjectionDetector = promptInjectionDetector ?? new PromptInjectionDetector();
    }

    public FetchMarkdownUseCase(
        IResourceDownloader downloader,
        IMarkitdownClient markitdownClient,
        IOutputWriter outputWriter,
        IConsoleView consoleView)
        : this(downloader, new NullNativeMarkdownConverter(), markitdownClient, outputWriter, consoleView)
    {
    }

    private sealed class NullNativeMarkdownConverter : INativeMarkdownConverter
    {
        public Task<ConversionResult> ConvertAsync(DownloadedResource resource, CancellationToken cancellationToken = default) =>
            Task.FromResult(ConversionResult.Failure("Native converter not configured.", 500));

        public Task<ConversionResult> ConvertAsync(DownloadedResource resource, NativeConversionOptions? options, CancellationToken cancellationToken = default) =>
            Task.FromResult(ConversionResult.Failure("Native converter not configured.", 500));
    }

    public async Task<int> ExecuteAsync(MarkdownFetchOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        // 1. Safety Guard: Validate initial URL against whitelist policy
        if (!_urlWhitelistValidator.IsAllowed(options.Url, options.UrlWhitelist))
        {
            _consoleView.WriteError($"URL '{options.Url}' is not permitted by URL whitelist policy.");
            return 1;
        }

        var apiUrl = (options.ApiUrl ?? string.Empty).TrimEnd('/');

        if (options.Engine == ConversionEngine.Api)
        {
            if (Uri.TryCreate(apiUrl, UriKind.Absolute, out var parsedApiUri))
            {
                if (options.UrlWhitelist != null && options.UrlWhitelist.Count > 0)
                {
                    if (!parsedApiUri.IsLoopback && !_urlWhitelistValidator.IsAllowed(parsedApiUri, options.UrlWhitelist))
                    {
                        _consoleView.WriteError($"API URL '{apiUrl}' is not permitted by URL whitelist policy.");
                        return 1;
                    }
                }
            }
            else
            {
                _consoleView.WriteError($"Invalid API URL: '{apiUrl}'");
                return 1;
            }

            if (!await _markitdownClient.IsReadyAsync(apiUrl, cancellationToken))
            {
                var port = ResolvePort(apiUrl);
                _consoleView.WriteError($"Markitdown API is not reachable at '{apiUrl}'");
                _consoleView.WriteError($"Run 'docker run -d --name markitdown-api -p {port}:8490 ghcr.io/dezoito/markitdown-api:latest'");
                return 1;
            }
        }

        DownloadedResource resource;
        try
        {
            resource = await _downloader.DownloadAsync(options.Url, options.Timeout, options.UrlWhitelist, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            if (ex.StatusCode != null)
            {
                _consoleView.WriteError($"Unable to download '{options.Url}': status code {(int)ex.StatusCode} ({ex.StatusCode})");
            }
            else
            {
                _consoleView.WriteError($"Unable to download '{options.Url}': {ex.Message}");
            }
            return 1;
        }
        catch (Exception ex)
        {
            _consoleView.WriteError(ex.Message);
            return 1;
        }

        await using (resource)
        {
            // 2. Safety Guard: Validate redirected URL against whitelist policy
            if (resource.Url != null)
            {
                if (!_urlWhitelistValidator.IsAllowed(resource.Url, options.UrlWhitelist))
                {
                    _consoleView.WriteError($"Redirected URL '{resource.Url}' is not permitted by URL whitelist policy.");
                    return 1;
                }
            }

            ConversionResult conversionResult;
            try
            {
                if (options.Engine == ConversionEngine.Api)
                {
                    if (options.FilterHiddenText && IsHtml(resource))
                    {
                        var filteredStream = await HtmlHiddenTextFilter.FilterHtmlStreamAsync(resource.Content, cancellationToken: cancellationToken);
                        var filteredResource = new DownloadedResource(filteredStream, resource.FileName, resource.ContentType, resource.Url);
                        await using (filteredResource)
                        {
                            conversionResult = await _markitdownClient.ConvertAsync(apiUrl, filteredResource, options.Timeout, cancellationToken);
                        }
                    }
                    else
                    {
                        conversionResult = await _markitdownClient.ConvertAsync(apiUrl, resource, options.Timeout, cancellationToken);
                    }
                }
                else
                {
                    var nativeOptions = new NativeConversionOptions
                    {
                        FilterHiddenText = options.FilterHiddenText
                    };
                    conversionResult = await _nativeConverter.ConvertAsync(resource, nativeOptions, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _consoleView.WriteError(ex.Message);
                return 1;
            }

            if (!conversionResult.IsSuccess)
            {
                if (conversionResult.StatusCode == 422)
                {
                    _consoleView.WriteError(options.Engine == ConversionEngine.Native
                        ? "File type is not supported. Try using --use-api or curl instead."
                        : "File type is not supported. Try curl instead.");
                }
                else if (!string.IsNullOrWhiteSpace(conversionResult.ErrorMessage))
                {
                    _consoleView.WriteError(conversionResult.ErrorMessage);
                }
                return 1;
            }

            var markdown = conversionResult.Markdown ?? string.Empty;
            var effectiveUrl = resource.Url ?? options.Url;
            if (effectiveUrl != null && !string.IsNullOrEmpty(markdown))
            {
                markdown = MarkdownLinkResolver.ResolveRelativeLinks(markdown, effectiveUrl);
            }

            // 3. Safety Guard: Prompt Injection Detection
            if (options.DetectPromptInjection)
            {
                var injectionResult = _promptInjectionDetector.Detect(markdown);
                if (injectionResult.IsDetected)
                {
                    if (options.PromptInjectionAction == PromptInjectionAction.Block)
                    {
                        _consoleView.WriteError($"Prompt injection detected in converted content: {injectionResult.MatchedPattern}. Fetch aborted for security.");
                        return 1;
                    }
                    else if (options.PromptInjectionAction == PromptInjectionAction.Warn)
                    {
                        _consoleView.WriteWarning($"Prompt injection detected: {injectionResult.MatchedPattern}.");
                    }
                    else if (options.PromptInjectionAction == PromptInjectionAction.Sanitize)
                    {
                        _consoleView.WriteWarning($"Prompt injection sanitized: {injectionResult.MatchedPattern}.");
                        markdown = injectionResult.SanitizedContent ?? markdown;
                    }
                }
            }

            try
            {
                await _outputWriter.WriteAsync(markdown, options.OutputPath, cancellationToken);
            }
            catch (Exception ex)
            {
                _consoleView.WriteError($"Failed to write output: {ex.Message}");
                return 1;
            }

            if (!string.IsNullOrEmpty(options.OutputPath))
            {
                _consoleView.WriteSuccess($"Saved to: {options.OutputPath}");
            }
        }

        return 0;
    }

    private static bool IsHtml(DownloadedResource resource)
    {
        if (!string.IsNullOrWhiteSpace(resource.ContentType))
        {
            var mime = resource.ContentType.Split(';')[0].Trim().ToLowerInvariant();
            if (mime is "text/html" or "application/xhtml+xml") return true;
        }

        var ext = Path.GetExtension(resource.FileName)?.ToLowerInvariant();
        return ext is ".html" or ".htm" or ".xhtml";
    }

    private static int ResolvePort(string apiUrl)
    {
        if (Uri.TryCreate(apiUrl, UriKind.Absolute, out var uri))
        {
            return uri.Port;
        }
        return 8490;
    }
}
