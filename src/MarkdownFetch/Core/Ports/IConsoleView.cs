namespace MarkdownFetch.Core.Ports;

/// <summary>
/// Port for presenting messages and diagnostic feedback to the user.
/// </summary>
public interface IConsoleView
{
    void WriteMessage(string message);

    void WriteSuccess(string message);

    void WriteError(string message);
}
