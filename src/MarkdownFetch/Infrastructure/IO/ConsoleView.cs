using MarkdownFetch.Core.Ports;

namespace MarkdownFetch.Infrastructure.IO;

/// <summary>
/// Console presentation adapter writing formatted messages to standard output and standard error.
/// </summary>
public sealed class ConsoleView : IConsoleView
{
    public void WriteMessage(string message) => Console.WriteLine(message);

    public void WriteSuccess(string message) => Console.WriteLine($"[SUCCESS] {message}");

    public void WriteError(string message) => Console.Error.WriteLine($"[ERROR] {message}");
}
