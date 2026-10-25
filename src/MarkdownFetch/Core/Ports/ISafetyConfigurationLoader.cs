using MarkdownFetch.Core.Models;

namespace MarkdownFetch.Core.Ports;

/// <summary>
/// Port for loading system and environment safety configurations.
/// </summary>
public interface ISafetyConfigurationLoader
{
    /// <summary>
    /// Discovers and loads safety policies from system/container paths or environment variables.
    /// </summary>
    /// <param name="customPath">Optional configuration path (primarily for testing).</param>
    /// <returns>The immutable safety policy.</returns>
    SafetyPolicy LoadPolicy(string? customPath = null);
}
