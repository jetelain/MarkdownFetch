using MarkdownFetch.Core.Services;

namespace MarkdownFetch.Tests.Core;

public class MarkdownLinkResolverTests
{
    private readonly Uri _baseUri = new("https://example.com/sub/page");

    [Fact]
    public void ResolveRelativeLinks_ResolvesRelativeLinksAndImages()
    {
        var input = """
        # Title

        [Get started](/signup)
        [Next page](next)
        [Parent directory](../overview)
        ![Logo](/assets/logo.png)
        """;

        var result = MarkdownLinkResolver.ResolveRelativeLinks(input, _baseUri);

        Assert.Contains("[Get started](https://example.com/signup)", result);
        Assert.Contains("[Next page](https://example.com/sub/next)", result);
        Assert.Contains("[Parent directory](https://example.com/overview)", result);
        Assert.Contains("![Logo](https://example.com/assets/logo.png)", result);
    }

    [Fact]
    public void ResolveRelativeLinks_PreservesAlreadyAbsoluteUrls()
    {
        var input = """
        [External](https://github.com/ollama)
        [HTTP](http://test.com/path)
        ![External Image](https://cdn.example.com/img.png)
        """;

        var result = MarkdownLinkResolver.ResolveRelativeLinks(input, _baseUri);

        Assert.Contains("[External](https://github.com/ollama)", result);
        Assert.Contains("[HTTP](http://test.com/path)", result);
        Assert.Contains("![External Image](https://cdn.example.com/img.png)", result);
    }

    [Fact]
    public void ResolveRelativeLinks_PreservesAnchorFragmentsAndNonHttpSchemes()
    {
        var input = """
        [Heading](#overview)
        [Contact](mailto:info@example.com)
        [Call](tel:+123456789)
        [Action](javascript:void(0))
        """;

        var result = MarkdownLinkResolver.ResolveRelativeLinks(input, _baseUri);

        Assert.Contains("[Heading](#overview)", result);
        Assert.Contains("[Contact](mailto:info@example.com)", result);
        Assert.Contains("[Call](tel:+123456789)", result);
        Assert.Contains("[Action](javascript:void(0))", result);
    }

    [Fact]
    public void ResolveRelativeLinks_PreservesFencedCodeBlocks()
    {
        var input = """
        Here is some documentation:

        ```markdown
        [Do not touch](/relative/path)
        ![Do not touch](/img.png)
        ```

        Outside code block:
        [Touch me](/relative/path)
        """;

        var result = MarkdownLinkResolver.ResolveRelativeLinks(input, _baseUri);

        Assert.Contains("```markdown\n[Do not touch](/relative/path)\n![Do not touch](/img.png)\n```", result);
        Assert.Contains("[Touch me](https://example.com/relative/path)", result);
    }

    [Fact]
    public void ResolveRelativeLinks_PreservesInlineCodeSpans()
    {
        var input = "See `[Do not touch](/foo)` or click [here](/foo).";

        var result = MarkdownLinkResolver.ResolveRelativeLinks(input, _baseUri);

        Assert.Contains("`[Do not touch](/foo)`", result);
        Assert.Contains("[here](https://example.com/foo)", result);
    }

    [Fact]
    public void ResolveRelativeLinks_HandlesLinksWithTitles()
    {
        var input = "[Link](/signup \"Create an account\") and ![Logo](/logo.png 'Site Logo')";

        var result = MarkdownLinkResolver.ResolveRelativeLinks(input, _baseUri);

        Assert.Contains("[Link](https://example.com/signup \"Create an account\")", result);
        Assert.Contains("![Logo](https://example.com/logo.png 'Site Logo')", result);
    }

    [Fact]
    public void ResolveRelativeLinks_HandlesCommonMarkAngleBracketedUrls()
    {
        var input = "[Link](</signup>)";

        var result = MarkdownLinkResolver.ResolveRelativeLinks(input, _baseUri);

        Assert.Contains("[Link](<https://example.com/signup>)", result);
    }
}
