using System.Text.Json;
using MarkdownFetch.Core.Models;
using MarkdownFetch.Core.Ports;

namespace MarkdownFetch.Infrastructure.Safety;

/// <summary>
/// Discovers and loads safety configuration files from system/container locations,
/// environment variables, and explicit CLI paths.
/// </summary>
public sealed class SafetyConfigurationLoader : ISafetyConfigurationLoader
{
    public static readonly SafetyConfigurationLoader Instance = new();

    private readonly IEnumerable<string>? _customSearchPaths;
    private readonly Func<string, bool> _fileExists;
    private readonly Func<string, string> _readFile;

    public SafetyConfigurationLoader(
        IEnumerable<string>? customSearchPaths = null,
        Func<string, bool>? fileExists = null,
        Func<string, string>? readFile = null)
    {
        _customSearchPaths = customSearchPaths;
        _fileExists = fileExists ?? File.Exists;
        _readFile = readFile ?? File.ReadAllText;
    }

    public SafetyPolicy LoadPolicy(string? customPath = null)
    {
        string? configPath = customPath;

        if (string.IsNullOrWhiteSpace(configPath))
        {
            configPath = FindConfigPath();
        }
        else if (!_fileExists(configPath))
        {
            throw new FileNotFoundException($"Safety configuration file not found: '{configPath}'", configPath);
        }

        if (configPath == null)
        {
            return new SafetyPolicy();
        }

        var config = ParseFile(configPath);
        return CreatePolicy(config, configPath);
    }

    private string? FindConfigPath()
    {
        if (_customSearchPaths != null)
        {
            foreach (var path in _customSearchPaths)
            {
                if (!string.IsNullOrWhiteSpace(path) && _fileExists(path))
                {
                    return path;
                }
            }
            return null;
        }

        string? CheckWindowsProgramData()
        {
            try
            {
                var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                if (!string.IsNullOrWhiteSpace(programData))
                {
                    var winPath1 = Path.Combine(programData, "MarkdownFetch", "safety.json");
                    if (_fileExists(winPath1)) return winPath1;

                    var winPath2 = Path.Combine(programData, "mdfetch", "safety.json");
                    if (_fileExists(winPath2)) return winPath2;
                }
            }
            catch
            {
                // Ignore environment path lookup failures
            }
            return null;
        }

        string? CheckLinuxPaths()
        {
            var linuxPaths = new[]
            {
                "/etc/markdownfetch/safety.json",
                "/etc/mdfetch/safety.json",
                "/etc/markdownfetch.json",
                "/etc/mdfetch.json"
            };

            foreach (var path in linuxPaths)
            {
                if (_fileExists(path))
                {
                    return path;
                }
            }
            return null;
        }

        // 1. System paths: Prioritize native OS configuration path
        var systemPath = OperatingSystem.IsWindows()
            ? (CheckWindowsProgramData() ?? CheckLinuxPaths())
            : (CheckLinuxPaths() ?? CheckWindowsProgramData());

        if (systemPath != null)
        {
            return systemPath;
        }

        // 2. Environment variable (FALLBACK only when no system configuration file exists on disk)
        var envVar = Environment.GetEnvironmentVariable("MDFETCH_SAFETY_CONFIG")
                     ?? Environment.GetEnvironmentVariable("MARKDOWNFETCH_SAFETY_CONFIG")
                     ?? Environment.GetEnvironmentVariable("MDFETCH_CONFIG_PATH");

        if (!string.IsNullOrWhiteSpace(envVar) && _fileExists(envVar))
        {
            return envVar;
        }

        return null;
    }

    private SafetyConfiguration ParseFile(string path)
    {
        try
        {
            var json = _readFile(path);
            var config = JsonSerializer.Deserialize(json, SafetyConfigurationJsonContext.Default.SafetyConfiguration);
            if (config == null)
            {
                throw new InvalidOperationException($"Safety configuration file at '{path}' is empty or invalid.");
            }

            // If nested under "safety" key, merge into primary
            if (config.Safety != null)
            {
                var s = config.Safety;
                return new SafetyConfiguration
                {
                    UrlWhitelist = s.UrlWhitelist ?? config.UrlWhitelist,
                    AllowedUrls = s.AllowedUrls ?? config.AllowedUrls,
                    FilterHiddenText = s.FilterHiddenText ?? config.FilterHiddenText,
                    ForceFilterHiddenText = s.ForceFilterHiddenText ?? config.ForceFilterHiddenText,
                    DetectPromptInjection = s.DetectPromptInjection ?? config.DetectPromptInjection,
                    ForceDetectPromptInjection = s.ForceDetectPromptInjection ?? config.ForceDetectPromptInjection,
                    PromptInjectionAction = s.PromptInjectionAction ?? config.PromptInjectionAction
                };
            }

            return config;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Invalid JSON in safety configuration file '{path}': {ex.Message}", ex);
        }
    }

    private static SafetyPolicy CreatePolicy(SafetyConfiguration config, string? loadedPath)
    {
        var whitelist = new List<string>();
        var list = config.UrlWhitelist ?? config.AllowedUrls;
        if (list != null)
        {
            whitelist.AddRange(list.Where(s => !string.IsNullOrWhiteSpace(s)));
        }

        var forceFilterHidden = config.ForceFilterHiddenText ?? false;
        var forceDetectPromptInjection = config.ForceDetectPromptInjection ?? false;

        var filterHidden = forceFilterHidden
            || (config.FilterHiddenText ?? true);

        var detectPromptInjection = forceDetectPromptInjection
            || (config.DetectPromptInjection ?? true);

        var action = config.PromptInjectionAction ?? PromptInjectionAction.Block;

        return new SafetyPolicy
        {
            UrlWhitelist = whitelist,
            FilterHiddenText = filterHidden,
            ForceFilterHiddenText = forceFilterHidden,
            DetectPromptInjection = detectPromptInjection,
            ForceDetectPromptInjection = forceDetectPromptInjection,
            PromptInjectionAction = action,
            LoadedFromPath = loadedPath
        };
    }
}
