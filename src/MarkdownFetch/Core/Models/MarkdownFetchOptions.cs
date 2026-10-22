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
}
