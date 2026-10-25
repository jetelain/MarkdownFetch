namespace MarkdownFetch.Core.Models;

/// <summary>
/// Result of evaluating content for prompt injection attacks.
/// </summary>
public sealed record PromptInjectionResult
{
    /// <summary>
    /// Whether any prompt injection pattern was detected.
    /// </summary>
    public bool IsDetected { get; init; }

    /// <summary>
    /// Description of the rule or pattern that matched.
    /// </summary>
    public string? MatchedPattern { get; init; }

    /// <summary>
    /// Brief excerpt of the matched text.
    /// </summary>
    public string? Excerpt { get; init; }

    /// <summary>
    /// The content with detected prompt injection removed or replaced.
    /// </summary>
    public string? SanitizedContent { get; init; }

    public static PromptInjectionResult Clean(string content) =>
        new() { IsDetected = false, SanitizedContent = content };

    public static PromptInjectionResult Detected(string matchedPattern, string excerpt, string sanitizedContent) =>
        new()
        {
            IsDetected = true,
            MatchedPattern = matchedPattern,
            Excerpt = excerpt,
            SanitizedContent = sanitizedContent
        };
}
