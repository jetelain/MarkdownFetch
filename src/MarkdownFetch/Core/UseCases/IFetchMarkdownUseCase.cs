using MarkdownFetch.Core.Models;

namespace MarkdownFetch.Core.UseCases;

/// <summary>
/// Interface for the fetch markdown use case.
/// </summary>
public interface IFetchMarkdownUseCase
{
    Task<int> ExecuteAsync(MarkdownFetchOptions options, CancellationToken cancellationToken = default);
}
