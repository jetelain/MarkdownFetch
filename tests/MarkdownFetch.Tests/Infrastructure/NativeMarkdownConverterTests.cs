using System.Text;
using MarkdownFetch.Core.Models;
using MarkdownFetch.Infrastructure.Converters;

namespace MarkdownFetch.Tests.Infrastructure;

public class NativeMarkdownConverterTests
{
    private readonly NativeMarkdownConverter _converter = new();

    [Fact]
    public async Task ConvertAsync_HtmlContent_ConvertsToMarkdown()
    {
        var html = "<h1>Title</h1><p>This is <strong>bold</strong> and <em>italic</em>.</p>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(html));
        var resource = new DownloadedResource(stream, "page.html", "text/html");

        var result = await _converter.ConvertAsync(resource);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Markdown);
        Assert.Contains("# Title", result.Markdown);
        Assert.Contains("**bold**", result.Markdown);
    }

    [Fact]
    public async Task ConvertAsync_JsonContent_ConvertsToMarkdown()
    {
        var json = "{\"name\": \"Alice\", \"age\": 30}";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var resource = new DownloadedResource(stream, "data.json", "application/json");

        var result = await _converter.ConvertAsync(resource);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Markdown);
        Assert.Contains("Alice", result.Markdown);
    }

    [Fact]
    public async Task ConvertAsync_PlainText_ConvertsToMarkdown()
    {
        var text = "Hello world\nLine 2";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        var resource = new DownloadedResource(stream, "note.txt", "text/plain");

        var result = await _converter.ConvertAsync(resource);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Markdown);
        Assert.Contains("Hello world", result.Markdown);
    }

    [Theory]
    [InlineData("test.html", "text/html", ".html")]
    [InlineData("test.pdf", null, ".pdf")]
    [InlineData("", "application/pdf", ".pdf")]
    [InlineData("file.docx", null, ".docx")]
    [InlineData("", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ".docx")]
    public void ResolveExtension_MapsCorrectly(string fileName, string? contentType, string expectedExtension)
    {
        using var stream = new MemoryStream();
        var resource = new DownloadedResource(stream, fileName, contentType);

        var extension = NativeMarkdownConverter.ResolveExtension(resource);

        Assert.Equal(expectedExtension, extension);
    }
}
