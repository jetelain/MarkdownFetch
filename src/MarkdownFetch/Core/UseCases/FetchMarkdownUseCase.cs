using MarkdownFetch.Core.Models;
using MarkdownFetch.Core.Ports;

namespace MarkdownFetch.Core.UseCases;

/// <summary>
/// Application use case coordinating the process of fetching a remote resource,
/// sending it to markitdown-api for conversion, and writing the resulting markdown.
/// </summary>
public sealed class FetchMarkdownUseCase : IFetchMarkdownUseCase
{
    private readonly IResourceDownloader _downloader;
    private readonly INativeMarkdownConverter _nativeConverter;
    private readonly IMarkitdownClient _markitdownClient;
    private readonly IOutputWriter _outputWriter;
    private readonly IConsoleView _consoleView;

    public FetchMarkdownUseCase(
        IResourceDownloader downloader,
        INativeMarkdownConverter nativeConverter,
        IMarkitdownClient markitdownClient,
        IOutputWriter outputWriter,
        IConsoleView consoleView)
    {
        _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
        _nativeConverter = nativeConverter ?? throw new ArgumentNullException(nameof(nativeConverter));
        _markitdownClient = markitdownClient ?? throw new ArgumentNullException(nameof(markitdownClient));
        _outputWriter = outputWriter ?? throw new ArgumentNullException(nameof(outputWriter));
        _consoleView = consoleView ?? throw new ArgumentNullException(nameof(consoleView));
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
    }

    public async Task<int> ExecuteAsync(MarkdownFetchOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var apiUrl = (options.ApiUrl ?? string.Empty).TrimEnd('/');

        if (options.Engine == ConversionEngine.Api)
        {
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
            resource = await _downloader.DownloadAsync(options.Url, options.Timeout, cancellationToken);
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
            ConversionResult conversionResult;
            try
            {
                if (options.Engine == ConversionEngine.Api)
                {
                    conversionResult = await _markitdownClient.ConvertAsync(apiUrl, resource, options.Timeout, cancellationToken);
                }
                else
                {
                    conversionResult = await _nativeConverter.ConvertAsync(resource, cancellationToken);
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

    private static int ResolvePort(string apiUrl)
    {
        if (Uri.TryCreate(apiUrl, UriKind.Absolute, out var uri))
        {
            return uri.Port;
        }
        return 8490;
    }
}
