using MarkdownFetch.Core.Models;

namespace MarkdownFetch.Core.Ports;

/// <summary>
/// Port for loading system and explicit safety configurations.
/// </summary>
public interface ISafetyConfigurationLoader
{
    /// <summary>
    /// Discovers and loads safety policies from system/container paths, environment variables,
    /// and any explicitly provided configuration file.
    /// </summary>
    /// <param name="explicitConfigPath">Optional path provided via CLI.</param>
    /// <returns>The combined immutable safety policy.</returns>
    SafetyPolicy LoadPolicy(string? explicitConfigPath = null);
}
