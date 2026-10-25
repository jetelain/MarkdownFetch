using System.CommandLine;
using MarkdownFetch.Core.Models;
using MarkdownFetch.Core.Ports;
using MarkdownFetch.Core.UseCases;
using MarkdownFetch.Infrastructure.Http;
using MarkdownFetch.Infrastructure.Safety;

namespace MarkdownFetch.Presentation.CommandLine;

/// <summary>
/// Configures System.CommandLine 2.0.13 commands, arguments, and options for the CLI,
/// including safety guards and policy enforcement.
/// </summary>
public static class CommandLineConfiguration
{
    public static RootCommand CreateRootCommand(Func<IFetchMarkdownUseCase> useCaseFactory) =>
        CreateRootCommand(useCaseFactory, null);

    public static RootCommand CreateRootCommand(
        Func<IFetchMarkdownUseCase> useCaseFactory,
        ISafetyConfigurationLoader? safetyLoader)
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

        // Safety Guard Options
        var filterHiddenTextOption = new Option<bool?>(
            "--filter-hidden-text")
        {
            Description = "Filter hidden text and invisible elements from HTML content (default: true).",
            Arity = ArgumentArity.ZeroOrOne
        };

        var noFilterHiddenTextOption = new Option<bool>(
            "--no-filter-hidden-text")
        {
            Description = "Disable filtering of hidden text and elements in HTML."
        };

        var detectPromptInjectionOption = new Option<bool?>(
            "--detect-prompt-injection")
        {
            Description = "Detect prompt injection attacks in fetched content (default: true).",
            Arity = ArgumentArity.ZeroOrOne
        };

        var noDetectPromptInjectionOption = new Option<bool>(
            "--no-detect-prompt-injection")
        {
            Description = "Disable prompt injection detection in fetched content."
        };

        var urlWhitelistOption = new Option<string[]>(
            "--url-whitelist")
        {
            Description = "Permitted URL pattern(s) or domain(s). Can be specified multiple times.",
            Arity = ArgumentArity.ZeroOrMore,
            AllowMultipleArgumentsPerToken = true
        };

        var safetyConfigOption = new Option<string?>(
            "--safety-config")
        {
            Description = "Path to a safety configuration file."
        };

        var rootCommand = new RootCommand("Small CLI to download any web resource as a markdown (with native .NET or markitdown-api).")
        {
            urlArgument,
            outputArgument,
            outputOption,
            engineOption,
            useApiOption,
            apiUrlOption,
            timeoutOption,
            filterHiddenTextOption,
            noFilterHiddenTextOption,
            detectPromptInjectionOption,
            noDetectPromptInjectionOption,
            urlWhitelistOption,
            safetyConfigOption
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

            // Load safety configuration from system/container paths and/or explicit path
            var loader = safetyLoader ?? SafetyConfigurationLoader.Instance;
            var explicitConfigPath = parseResult.GetValue(safetyConfigOption);
            SafetyPolicy safetyPolicy;
            try
            {
                safetyPolicy = loader.LoadPolicy(explicitConfigPath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ERROR] Failed to load safety configuration: {ex.Message}");
                return 1;
            }

            // Resolve FilterHiddenText (on by default; cannot be disabled if forced by safety config)
            bool filterHiddenText;
            var noFilterFlag = parseResult.GetValue(noFilterHiddenTextOption);
            var filterValue = parseResult.GetValue(filterHiddenTextOption);

            if (safetyPolicy.ForceFilterHiddenText)
            {
                if (noFilterFlag || (filterValue.HasValue && !filterValue.Value))
                {
                    Console.Error.WriteLine("[WARNING] Hidden text filtering is enforced by safety configuration and cannot be disabled.");
                }
                filterHiddenText = true;
            }
            else
            {
                if (noFilterFlag)
                {
                    filterHiddenText = false;
                }
                else if (filterValue.HasValue)
                {
                    filterHiddenText = filterValue.Value;
                }
                else
                {
                    filterHiddenText = safetyPolicy.FilterHiddenText;
                }
            }

            // Resolve DetectPromptInjection (on by default; cannot be disabled if forced by safety config)
            bool detectPromptInjection;
            var noDetectFlag = parseResult.GetValue(noDetectPromptInjectionOption);
            var detectValue = parseResult.GetValue(detectPromptInjectionOption);

            if (safetyPolicy.ForceDetectPromptInjection)
            {
                if (noDetectFlag || (detectValue.HasValue && !detectValue.Value))
                {
                    Console.Error.WriteLine("[WARNING] Prompt injection detection is enforced by safety configuration and cannot be disabled.");
                }
                detectPromptInjection = true;
            }
            else
            {
                if (noDetectFlag)
                {
                    detectPromptInjection = false;
                }
                else if (detectValue.HasValue)
                {
                    detectPromptInjection = detectValue.Value;
                }
                else
                {
                    detectPromptInjection = safetyPolicy.DetectPromptInjection;
                }
            }

            // Resolve URL Whitelist
            var cliWhitelistEntries = new List<string>();
            var cliWhitelist = parseResult.GetValue(urlWhitelistOption);
            if (cliWhitelist != null && cliWhitelist.Length > 0)
            {
                foreach (var item in cliWhitelist)
                {
                    if (!string.IsNullOrWhiteSpace(item))
                    {
                        var parts = item.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        foreach (var part in parts)
                        {
                            if (!string.IsNullOrWhiteSpace(part))
                            {
                                cliWhitelistEntries.Add(part);
                            }
                        }
                    }
                }
            }

            var options = new MarkdownFetchOptions
            {
                Url = uri,
                OutputPath = outputPath,
                Engine = engine,
                ApiUrl = apiUrl,
                Timeout = TimeSpan.FromSeconds(timeoutSeconds),
                FilterHiddenText = filterHiddenText,
                DetectPromptInjection = detectPromptInjection,
                UrlWhitelist = cliWhitelistEntries.Count > 0 ? cliWhitelistEntries : safetyPolicy.UrlWhitelist,
                SystemUrlWhitelist = safetyPolicy.SystemUrlWhitelist.Count > 0 ? safetyPolicy.SystemUrlWhitelist : safetyPolicy.UrlWhitelist,
                PromptInjectionAction = safetyPolicy.PromptInjectionAction,
                SafetyConfigPath = explicitConfigPath ?? safetyPolicy.LoadedFromPath
            };

            var useCase = useCaseFactory();
            return await useCase.ExecuteAsync(options, cancellationToken);
        });

        return rootCommand;
    }
}
