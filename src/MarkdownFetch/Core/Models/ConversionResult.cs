namespace MarkdownFetch.Core.Models;

/// <summary>
/// Represents the result of converting a resource to markdown.
/// </summary>
public sealed class ConversionResult
{
    public bool IsSuccess { get; }
    public string? Markdown { get; }
    public string? ErrorMessage { get; }
    public int? StatusCode { get; }

    private ConversionResult(bool isSuccess, string? markdown, string? errorMessage, int? statusCode)
    {
        IsSuccess = isSuccess;
        Markdown = markdown;
        ErrorMessage = errorMessage;
        StatusCode = statusCode;
    }

    public static ConversionResult Success(string markdown) =>
        new(true, markdown, null, 200);

    public static ConversionResult Failure(string errorMessage, int? statusCode = null) =>
        new(false, null, errorMessage, statusCode);
}
