namespace MarkdownFetch.Core.Models;

/// <summary>
/// Resolved safety configuration policy governing URL whitelisting and safety guard enforcement.
/// </summary>
public sealed record SafetyPolicy
{
    /// <summary>
    /// The list of permitted URL patterns / hosts.
    /// </summary>
    public IReadOnlyList<string> UrlWhitelist { get; init; } = Array.Empty<string>();

    /// <summary>
    /// System-enforced URL patterns from protected configuration that cannot be bypassed or widened by CLI arguments.
    /// </summary>
    public IReadOnlyList<string> SystemUrlWhitelist { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Default state for hidden text filtering.
    /// </summary>
    public bool FilterHiddenText { get; init; } = true;

    /// <summary>
    /// If true, hidden text filtering cannot be disabled by CLI arguments.
    /// </summary>
    public bool ForceFilterHiddenText { get; init; }

    /// <summary>
    /// Default state for prompt injection detection.
    /// </summary>
    public bool DetectPromptInjection { get; init; } = true;

    /// <summary>
    /// If true, prompt injection detection cannot be disabled by CLI arguments.
    /// </summary>
    public bool ForceDetectPromptInjection { get; init; }

    /// <summary>
    /// Action to take when prompt injection is detected (Block, Warn, Sanitize).
    /// </summary>
    public PromptInjectionAction PromptInjectionAction { get; init; } = PromptInjectionAction.Block;

    /// <summary>
    /// The path of the primary configuration file loaded, if any.
    /// </summary>
    public string? LoadedFromPath { get; init; }
}
