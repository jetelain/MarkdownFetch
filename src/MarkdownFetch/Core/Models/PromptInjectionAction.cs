namespace MarkdownFetch.Core.Models;

/// <summary>
/// Defines the action to take when prompt injection is detected in converted content.
/// </summary>
public enum PromptInjectionAction
{
    /// <summary>
    /// Abort execution with exit code 1 and error message (default).
    /// </summary>
    Block,

    /// <summary>
    /// Log a security warning to stderr but allow output to be produced.
    /// </summary>
    Warn,

    /// <summary>
    /// Log a warning and replace the detected injection with a sanitized placeholder.
    /// </summary>
    Sanitize
}
