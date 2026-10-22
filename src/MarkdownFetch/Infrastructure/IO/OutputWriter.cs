using MarkdownFetch.Core.Ports;

namespace MarkdownFetch.Infrastructure.IO;

/// <summary>
/// Output writer writing markdown text either to file or standard output.
/// </summary>
public sealed class OutputWriter : IOutputWriter
{
    public async Task WriteAsync(string markdown, string? outputPath, CancellationToken cancellationToken = default)
    {
        if (outputPath != null)
        {
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(outputPath, markdown, cancellationToken);
        }
        else
        {
            Console.WriteLine(markdown);
        }
    }
}
