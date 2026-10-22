using System.Text;
using Pmad.Markitdown.Converters;

namespace Pmad.Markitdown;

/// <summary>
/// Primary service for converting HTML, PDF, DOCX, and text documents into clean Markdown.
/// </summary>
public sealed class Markitdown : IMarkitdown
{
    private readonly List<IFormatConverter> _converters = new();
    private readonly MarkitdownOptions _defaultOptions;

    public Markitdown(MarkitdownOptions? defaultOptions = null)
    {
        _defaultOptions = defaultOptions ?? new MarkitdownOptions();

        var htmlConverter = new HtmlToMarkdownConverter();
        _converters.Add(htmlConverter);
        _converters.Add(new PdfToMarkdownConverter());
        _converters.Add(new DocxToMarkdownConverter(htmlConverter));
        _converters.Add(new PlainTextConverter());
    }

    public Markitdown(IEnumerable<IFormatConverter> customConverters, MarkitdownOptions? defaultOptions = null)
    {
        _defaultOptions = defaultOptions ?? new MarkitdownOptions();
        _converters.AddRange(customConverters ?? throw new ArgumentNullException(nameof(customConverters)));
    }

    public async Task<string> ConvertAsync(
        Stream stream,
        string? extensionOrMediaType = null,
        MarkitdownOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var opts = options ?? _defaultOptions;
        string? extension = null;
        string? mediaType = null;

        if (!string.IsNullOrWhiteSpace(extensionOrMediaType))
        {
            if (extensionOrMediaType.Contains('/'))
            {
                mediaType = extensionOrMediaType;
            }
            else
            {
                extension = extensionOrMediaType.StartsWith('.') ? extensionOrMediaType : "." + extensionOrMediaType;
            }
        }

        foreach (var converter in _converters)
        {
            if (converter.CanHandle(extension, mediaType))
            {
                return await converter.ConvertAsync(stream, opts, cancellationToken);
            }
        }

        throw new NotSupportedException($"No converter found for extension '{extension}' or media type '{mediaType}'.");
    }

    public async Task<string> ConvertFileAsync(
        string filePath,
        MarkitdownOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        }

        var extension = Path.GetExtension(filePath);
        await using var stream = File.OpenRead(filePath);
        return await ConvertAsync(stream, extension, options, cancellationToken);
    }

    public async Task<string> ConvertTextAsync(
        string text,
        string? extensionOrMediaType = "html",
        MarkitdownOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text ?? string.Empty));
        return await ConvertAsync(stream, extensionOrMediaType, options, cancellationToken);
    }
}
