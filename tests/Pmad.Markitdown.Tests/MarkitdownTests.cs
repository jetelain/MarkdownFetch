using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Pmad.Markitdown;

namespace Pmad.Markitdown.Tests;

public class MarkitdownTests
{
    private readonly Markitdown _markitdown = new();

    [Fact]
    public async Task ConvertTextAsync_HtmlDocument_ProducesRichMarkdown()
    {
        var html = """
        <!DOCTYPE html>
        <html>
        <head>
            <title>Test Page</title>
            <style>body { font-family: sans-serif; }</style>
            <script>console.log("ignore me");</script>
        </head>
        <body>
            <nav><a href="/">Home</a></nav>
            <main>
                <h1>Main Heading</h1>
                <p>This is a paragraph with <strong>bold text</strong> and <em>italic text</em>.</p>
                <p>Check out <a href="https://example.com">Example</a>.</p>
                <ul>
                    <li>First bullet</li>
                    <li>Second bullet</li>
                </ul>
                <table>
                    <thead>
                        <tr><th>Header A</th><th>Header B</th></tr>
                    </thead>
                    <tbody>
                        <tr><td>Val 1</td><td>Val 2</td></tr>
                    </tbody>
                </table>
            </main>
            <footer>Footer note</footer>
        </body>
        </html>
        """;

        var markdown = await _markitdown.ConvertTextAsync(html, "text/html");

        Assert.NotNull(markdown);
        Assert.Contains("# Main Heading", markdown);
        Assert.Contains("**bold text**", markdown);
        Assert.Contains("*italic text*", markdown);
        Assert.Contains("[Example](https://example.com)", markdown);
        Assert.Contains("First bullet", markdown);
        Assert.Contains("Header A", markdown);
        Assert.Contains("Val 1", markdown);
        Assert.DoesNotContain("ignore me", markdown);
        Assert.DoesNotContain("font-family", markdown);
    }

    [Fact]
    public async Task ConvertAsync_PdfDocument_ExtractsTextCorrectly()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        page.AddText("Sample PDF text generated with PdfPig.", 12, new PdfPoint(50, 700), font);
        var pdfBytes = builder.Build();

        using var pdfStream = new MemoryStream(pdfBytes);
        var markdown = await _markitdown.ConvertAsync(pdfStream, ".pdf");

        Assert.NotNull(markdown);
        Assert.Contains("Sample PDF text generated with PdfPig.", markdown);
    }

    [Fact]
    public async Task ConvertAsync_DummyPdfFile_ExtractsText()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Resources", "dummy.pdf");
        Assert.True(File.Exists(path), $"File not found: {path}");

        var markdown = await _markitdown.ConvertFileAsync(path);
        Assert.NotNull(markdown);
        Assert.Equal("Dummy PDF file", markdown);
    }

    [Fact]
    public async Task ConvertTextAsync_PlainText_NormalizesAndPreservesContent()
    {
        var text = "First line\r\nSecond line\r\nThird line";

        var markdown = await _markitdown.ConvertTextAsync(text, "text/plain");

        Assert.Equal("First line\nSecond line\nThird line", markdown);
    }

    [Fact]
    public async Task ConvertAsync_UnsupportedExtension_ThrowsNotSupportedException()
    {
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            _markitdown.ConvertAsync(stream, ".unknown_xyz"));
    }

    [Fact]
    public async Task ConvertTextAsync_WithBaseUrl_ResolvesRelativeLinksAndImagesToAbsolute()
    {
        var html = """
        <html>
        <head><title>Ollama</title></head>
        <body>
            <p><a href="/signup">Get started</a></p>
            <p><a href="models">Models</a></p>
            <p><a href="../about">About</a></p>
            <p><a href="https://github.com/ollama">External GitHub</a></p>
            <p><img src="/public/logos/apple.svg" alt="Apple" /></p>
            <p><img src="images/nvidia.svg" alt="NVIDIA" /></p>
        </body>
        </html>
        """;

        var options = new MarkitdownOptions
        {
            BaseUrl = new Uri("https://ollama.com/library/")
        };

        var markdown = await _markitdown.ConvertTextAsync(html, "text/html", options);

        Assert.NotNull(markdown);
        Assert.Contains("[Get started](https://ollama.com/signup)", markdown);
        Assert.Contains("[Models](https://ollama.com/library/models)", markdown);
        Assert.Contains("[About](https://ollama.com/about)", markdown);
        Assert.Contains("[External GitHub](https://github.com/ollama)", markdown);
        Assert.Contains("![Apple](https://ollama.com/public/logos/apple.svg)", markdown);
        Assert.Contains("![NVIDIA](https://ollama.com/library/images/nvidia.svg)", markdown);
    }

    [Fact]
    public async Task ConvertTextAsync_WithBaseHrefTag_ResolvesRelativeToBaseTag()
    {
        var html = """
        <html>
        <head>
            <title>Base Tag Page</title>
            <base href="https://cdn.example.com/assets/" />
        </head>
        <body>
            <p><a href="docs/guide.html">Guide</a></p>
            <p><img src="logo.png" alt="Logo" /></p>
        </body>
        </html>
        """;

        var markdown = await _markitdown.ConvertTextAsync(html, "text/html");

        Assert.NotNull(markdown);
        Assert.Contains("[Guide](https://cdn.example.com/assets/docs/guide.html)", markdown);
        Assert.Contains("![Logo](https://cdn.example.com/assets/logo.png)", markdown);
    }

    [Fact]
    public async Task ConvertTextAsync_WithBaseUrl_PreservesAnchorFragmentsAndNonHttpSchemes()
    {
        var html = """
        <html>
        <body>
            <p><a href="#heading-1">Internal Section</a></p>
            <p><a href="mailto:support@example.com">Contact Us</a></p>
            <p><a href="javascript:void(0)">Do Nothing</a></p>
            <p><a href="/page#details">Page with Fragment</a></p>
        </body>
        </html>
        """;

        var options = new MarkitdownOptions(new Uri("https://example.com/app/"));

        var markdown = await _markitdown.ConvertTextAsync(html, "text/html", options);

        Assert.NotNull(markdown);
        Assert.Contains("[Internal Section](#heading-1)", markdown);
        Assert.Contains("[Contact Us](mailto:support@example.com)", markdown);
        Assert.Contains("[Page with Fragment](https://example.com/page#details)", markdown);
    }
}

