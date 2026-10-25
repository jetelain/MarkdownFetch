using System.Text.Json;
using System.Text.Json.Serialization;
using MarkdownFetch.Core.Models;

namespace MarkdownFetch.Infrastructure.Safety;

/// <summary>
/// Data transfer object representing the contents of a safety configuration JSON file.
/// </summary>
public sealed class SafetyConfiguration
{
    [JsonPropertyName("urlWhitelist")]
    public List<string>? UrlWhitelist { get; set; }

    [JsonPropertyName("allowedUrls")]
    public List<string>? AllowedUrls { get; set; }

    [JsonPropertyName("filterHiddenText")]
    public bool? FilterHiddenText { get; set; }

    [JsonPropertyName("forceFilterHiddenText")]
    public bool? ForceFilterHiddenText { get; set; }

    [JsonPropertyName("detectPromptInjection")]
    public bool? DetectPromptInjection { get; set; }

    [JsonPropertyName("forceDetectPromptInjection")]
    public bool? ForceDetectPromptInjection { get; set; }

    [JsonPropertyName("promptInjectionAction")]
    public PromptInjectionAction? PromptInjectionAction { get; set; }

    [JsonPropertyName("safety")]
    public SafetyConfiguration? Safety { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    AllowTrailingCommas = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(SafetyConfiguration))]
internal sealed partial class SafetyConfigurationJsonContext : JsonSerializerContext
{
}
