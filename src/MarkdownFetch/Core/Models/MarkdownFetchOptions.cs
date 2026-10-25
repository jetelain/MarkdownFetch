namespace MarkdownFetch.Core.Models;

public enum ConversionEngine
{
    Native,
    Api
}

/// <summary>
/// Execution parameters for fetching and converting a markdown resource.
/// </summary>
public sealed record MarkdownFetchOptions
{
    public required Uri Url { get; init; }
    public string? OutputPath { get; init; }
    public ConversionEngine Engine { get; init; } = ConversionEngine.Native;
    public string ApiUrl { get; init; } = "http://127.0.0.1:8490";
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// If true, filters hidden elements and invisible characters from HTML content (on by default).
    /// </summary>
    public bool FilterHiddenText { get; init; } = true;

    /// <summary>
    /// If true, checks converted markdown for prompt injection patterns (on by default).
    /// </summary>
    public bool DetectPromptInjection { get; init; } = true;

    /// <summary>
    /// Allowed URL patterns / domains. If empty, all URLs are permitted unless restricted by safety policy.
    /// </summary>
    public IReadOnlyList<string> UrlWhitelist { get; init; } = Array.Empty<string>();

    /// <summary>
    /// System-enforced URL patterns from protected configuration that cannot be bypassed or widened by CLI arguments.
    /// </summary>
    public IReadOnlyList<string> SystemUrlWhitelist { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Action to take when prompt injection is detected (default: Block).
    /// </summary>
    public PromptInjectionAction PromptInjectionAction { get; init; } = PromptInjectionAction.Block;

    /// <summary>
    /// Optional path to a safety configuration file.
    /// </summary>
    public string? SafetyConfigPath { get; init; }
}
