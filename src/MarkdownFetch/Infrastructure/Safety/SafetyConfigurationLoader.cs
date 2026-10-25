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

    public SafetyPolicy LoadPolicy(string? explicitConfigPath = null)
    {
        SafetyConfiguration? systemConfig = null;
        string? systemConfigPath = null;

        var systemPath = FindSystemConfigPath();
        if (systemPath != null)
        {
            systemConfig = ParseFile(systemPath);
            systemConfigPath = systemPath;
        }

        SafetyConfiguration? explicitConfig = null;
        if (!string.IsNullOrWhiteSpace(explicitConfigPath))
        {
            if (!_fileExists(explicitConfigPath))
            {
                throw new FileNotFoundException($"Safety configuration file not found: '{explicitConfigPath}'", explicitConfigPath);
            }
            explicitConfig = ParseFile(explicitConfigPath);
        }

        return MergePolicies(systemConfig, explicitConfig, systemConfigPath ?? explicitConfigPath);
    }

    private string? FindSystemConfigPath()
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

        // 1. Standard Linux / Unix / Container paths (HIGHEST PRIORITY - cannot be bypassed by agent environment variables)
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

        // 2. Windows ProgramData paths (HIGHEST PRIORITY on Windows)
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

        // 3. Environment variable (FALLBACK only when no system configuration file exists on disk)
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

    private static SafetyPolicy MergePolicies(SafetyConfiguration? system, SafetyConfiguration? explicitConfig, string? loadedPath)
    {
        var systemWhitelist = new List<string>();
        var systemList = system?.UrlWhitelist ?? system?.AllowedUrls;
        if (systemList != null)
        {
            systemWhitelist.AddRange(systemList.Where(s => !string.IsNullOrWhiteSpace(s)));
        }

        var explicitWhitelist = new List<string>();
        var explicitList = explicitConfig?.UrlWhitelist ?? explicitConfig?.AllowedUrls;
        if (explicitList != null)
        {
            explicitWhitelist.AddRange(explicitList.Where(s => !string.IsNullOrWhiteSpace(s)));
        }

        // If system configuration specified a whitelist, system whitelist is strictly enforced and cannot be widened
        var combinedWhitelist = new List<string>(systemWhitelist);
        if (combinedWhitelist.Count == 0 && explicitWhitelist.Count > 0)
        {
            combinedWhitelist.AddRange(explicitWhitelist);
        }

        var forceFilterHidden = (system?.ForceFilterHiddenText ?? false) || (explicitConfig?.ForceFilterHiddenText ?? false);
        var forceDetectPromptInjection = (system?.ForceDetectPromptInjection ?? false) || (explicitConfig?.ForceDetectPromptInjection ?? false);

        var filterHidden = forceFilterHidden
            || (explicitConfig?.FilterHiddenText ?? system?.FilterHiddenText ?? true);

        var detectPromptInjection = forceDetectPromptInjection
            || (explicitConfig?.DetectPromptInjection ?? system?.DetectPromptInjection ?? true);

        var action = explicitConfig?.PromptInjectionAction
            ?? system?.PromptInjectionAction
            ?? PromptInjectionAction.Block;

        return new SafetyPolicy
        {
            UrlWhitelist = combinedWhitelist,
            SystemUrlWhitelist = systemWhitelist,
            FilterHiddenText = filterHidden,
            ForceFilterHiddenText = forceFilterHidden,
            DetectPromptInjection = detectPromptInjection,
            ForceDetectPromptInjection = forceDetectPromptInjection,
            PromptInjectionAction = action,
            LoadedFromPath = loadedPath
        };
    }
}
