using System.CommandLine;
using MarkdownFetch.Core.Models;
using MarkdownFetch.Core.UseCases;
using MarkdownFetch.Infrastructure.Http;

namespace MarkdownFetch.Presentation.CommandLine;

/// <summary>
/// Configures System.CommandLine 2.0.13 commands, arguments, and options for the CLI.
/// </summary>
public static class CommandLineConfiguration
{
    public static RootCommand CreateRootCommand(Func<IFetchMarkdownUseCase> useCaseFactory)
    {
        ArgumentNullException.ThrowIfNull(useCaseFactory);

        var urlArgument = new Argument<string>("url")
        {
            Description = "The URL of the web resource to fetch."
        };

        var outputArgument = new Argument<string?>("output")
        {
            Description = "The path of the output markdown file. If not specified, markdown is written to stdout.",
            Arity = ArgumentArity.ZeroOrOne,
            HelpName = "output file"
        };

        var outputOption = new Option<string?>(
            "--output",
            "-o")
        {
            Description = "The path of the output markdown file (overrides positional argument if both are specified)."
        };

        var defaultApiUrl = Environment.GetEnvironmentVariable("MARKITDOWN_API_URL") ?? MarkitdownApiClient.DefaultApiUrl;
        var apiUrlOption = new Option<string>(
            "--api-url",
            "-a")
        {
            Description = "The markitdown API server URL (automatically selects the API engine).",
            DefaultValueFactory = _ => defaultApiUrl
        };

        var engineOption = new Option<string>(
            "--engine")
        {
            Description = "Conversion engine to use: 'native' (default, pure .NET managed) or 'api' (markitdown-api).",
            DefaultValueFactory = _ => "native"
        };

        var useApiOption = new Option<bool>(
            "--use-api")
        {
            Description = "Use external markitdown-api instead of native .NET conversion (shortcut for --engine api)."
        };

        var timeoutOption = new Option<int>(
            "--timeout",
            "-t")
        {
            Description = "Timeout in seconds for downloading and processing.",
            DefaultValueFactory = _ => 30
        };

        var rootCommand = new RootCommand("Small CLI to download any web resource as a markdown (with native .NET or markitdown-api).")
        {
            urlArgument,
            outputArgument,
            outputOption,
            engineOption,
            useApiOption,
            apiUrlOption,
            timeoutOption
        };

        rootCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            var rawUrl = parseResult.GetValue(urlArgument);
            if (string.IsNullOrWhiteSpace(rawUrl) || !Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri))
            {
                Console.Error.WriteLine($"[ERROR] Invalid URL: '{rawUrl}'");
                return 1;
            }

            var positionalOutput = parseResult.GetValue(outputArgument);
            var optionOutput = parseResult.GetValue(outputOption);
            var outputPath = !string.IsNullOrEmpty(optionOutput) ? optionOutput : positionalOutput;

            var engineRaw = parseResult.GetValue(engineOption)?.Trim().ToLowerInvariant() ?? "native";
            if (engineRaw != "native" && engineRaw != "api")
            {
                Console.Error.WriteLine($"[ERROR] Invalid engine '{engineRaw}'. Must be 'native' or 'api'.");
                return 1;
            }

            var useApi = parseResult.GetValue(useApiOption);
            var apiUrlResult = parseResult.GetResult(apiUrlOption);
            var apiUrlExplicitlyProvided = apiUrlResult is { Implicit: false };

            var engine = (engineRaw == "api" || useApi || apiUrlExplicitlyProvided)
                ? ConversionEngine.Api
                : ConversionEngine.Native;

            var apiUrl = parseResult.GetValue(apiUrlOption) ?? defaultApiUrl;
            var timeoutSeconds = parseResult.GetValue(timeoutOption);
            if (timeoutSeconds <= 0)
            {
                timeoutSeconds = 30;
            }

            var options = new MarkdownFetchOptions
            {
                Url = uri,
                OutputPath = outputPath,
                Engine = engine,
                ApiUrl = apiUrl,
                Timeout = TimeSpan.FromSeconds(timeoutSeconds)
            };

            var useCase = useCaseFactory();
            return await useCase.ExecuteAsync(options, cancellationToken);
        });

        return rootCommand;
    }
}
