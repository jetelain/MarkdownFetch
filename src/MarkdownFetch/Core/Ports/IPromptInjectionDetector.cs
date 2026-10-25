using MarkdownFetch.Core.Models;

namespace MarkdownFetch.Core.Ports;

/// <summary>
/// Port for scanning content to detect prompt injection attempts.
/// </summary>
public interface IPromptInjectionDetector
{
    /// <summary>
    /// Scans the provided text content for known prompt injection patterns and adversarial directives.
    /// </summary>
    /// <param name="content">The text or markdown content to inspect.</param>
    /// <returns>A result indicating whether prompt injection was found.</returns>
    PromptInjectionResult Detect(string content);
}
