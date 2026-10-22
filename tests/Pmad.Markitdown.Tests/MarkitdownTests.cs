using System.Text;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
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
        using var pdfDoc = new PdfDocument();
        var page = pdfDoc.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
        {
            var options = new XPdfFontOptions(PdfFontEncoding.WinAnsi);
            var font = new XFont("Helvetica", 14, XFontStyle.Regular, options);
            gfx.DrawString("Sample PDF text generated with PdfSharpCore.", font, XBrushes.Black, new XPoint(50, 100));
        }

        using var pdfStream = new MemoryStream();
        pdfDoc.Save(pdfStream, closeStream: false);
        pdfStream.Position = 0;

        var markdown = await _markitdown.ConvertAsync(pdfStream, ".pdf");

        Assert.NotNull(markdown);
        Assert.Contains("Sample PDF text generated with PdfSharpCore.", markdown);
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
}
