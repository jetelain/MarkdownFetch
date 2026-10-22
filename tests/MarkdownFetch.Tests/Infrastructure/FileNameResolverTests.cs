using System.Net.Http.Headers;
using MarkdownFetch.Infrastructure.Common;

namespace MarkdownFetch.Tests.Infrastructure;

public class FileNameResolverTests
{
    [Fact]
    public void ResolveFileName_WithContentDispositionFileName_ReturnsExtractedFileName()
    {
        var content = new StringContent("dummy");
        content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
        {
            FileName = "\"document.pdf\""
        };

        var fileName = FileNameResolver.ResolveFileName(content.Headers);

        Assert.Equal("document.pdf", fileName);
    }

    [Fact]
    public void ResolveFileName_WithRawContentDispositionHeader_ReturnsExtractedFileName()
    {
        var content = new StringContent("dummy");
        content.Headers.TryAddWithoutValidation("Content-Disposition", "attachment; filename=\"report.docx\"");

        var fileName = FileNameResolver.ResolveFileName(content.Headers);

        Assert.Equal("report.docx", fileName);
    }

    [Fact]
    public void ResolveFileName_WithoutDisposition_UsesContentTypeExtension()
    {
        var content = new StringContent("dummy");
        content.Headers.ContentType = new MediaTypeHeaderValue("text/html");

        var fileName = FileNameResolver.ResolveFileName(content.Headers);

        Assert.Equal("file.html", fileName);
    }

    [Fact]
    public void ResolveFileName_WithoutDisposition_WithPdfContentType_ReturnsPdfExtension()
    {
        var content = new StringContent("dummy");
        content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");

        var fileName = FileNameResolver.ResolveFileName(content.Headers);

        Assert.Equal("file.pdf", fileName);
    }

    [Fact]
    public void ResolveFileName_WithoutDispositionAndUnknownContentType_UsesSourceUriExtension()
    {
        var content = new StringContent("dummy");
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        var uri = new Uri("https://example.com/assets/presentation.pptx");
        var fileName = FileNameResolver.ResolveFileName(content.Headers, uri);

        Assert.Equal("presentation.pptx", fileName);
    }
}
