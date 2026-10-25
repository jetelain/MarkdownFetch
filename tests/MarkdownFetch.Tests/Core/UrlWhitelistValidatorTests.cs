using MarkdownFetch.Core.Services;

namespace MarkdownFetch.Tests.Core;

public class UrlWhitelistValidatorTests
{
    private readonly UrlWhitelistValidator _validator = new();

    [Fact]
    public void IsAllowed_WhenWhitelistEmptyOrNull_ReturnsTrue()
    {
        var url = new Uri("https://example.com/some/path");

        Assert.True(_validator.IsAllowed(url, null));
        Assert.True(_validator.IsAllowed(url, Array.Empty<string>()));
    }

    [Fact]
    public void IsAllowed_DisallowsNonHttpSchemes()
    {
        var fileUrl = new Uri("file:///etc/passwd");
        var customUrl = new Uri("ftp://example.com");

        Assert.False(_validator.IsAllowed(fileUrl, null));
        Assert.False(_validator.IsAllowed(customUrl, null));
    }

    [Theory]
    [InlineData("https://github.com", "github.com", true)]
    [InlineData("https://github.com/dotnet/runtime", "github.com", true)]
    [InlineData("http://github.com:8080/docs", "github.com:8080", true)]
    [InlineData("https://github.com:443/docs", "github.com:8080", false)]
    [InlineData("https://evil-github.com", "github.com", false)]
    [InlineData("https://github.com.evil.com", "github.com", false)]
    public void IsAllowed_ExactHostMatching(string urlStr, string rule, bool expected)
    {
        var url = new Uri(urlStr);
        var result = _validator.IsAllowed(url, new[] { rule });

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("https://sub.example.com", "*.example.com", true)]
    [InlineData("https://a.b.example.com", "*.example.com", true)]
    [InlineData("https://example.com", "*.example.com", true)]
    [InlineData("https://evil-example.com", "*.example.com", false)]
    [InlineData("https://example.com.evil.com", "*.example.com", false)]
    [InlineData("https://sub.example.com", ".example.com", true)]
    [InlineData("https://example.com", ".example.com", true)]
    public void IsAllowed_WildcardHostMatching(string urlStr, string rule, bool expected)
    {
        var url = new Uri(urlStr);
        var result = _validator.IsAllowed(url, new[] { rule });

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("https://example.com/api/v1/test", "https://example.com/api/*", true)]
    [InlineData("https://example.com/other", "https://example.com/api/*", false)]
    [InlineData("http://example.com/api/v1", "https://example.com/api/*", false)] // Scheme mismatch
    public void IsAllowed_FullUrlPrefixMatching(string urlStr, string rule, bool expected)
    {
        var url = new Uri(urlStr);
        var result = _validator.IsAllowed(url, new[] { rule });

        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsAllowed_WithMultipleRules_AllowsIfAnyMatches()
    {
        var whitelist = new[] { "*.github.com", "learn.microsoft.com", "https://example.com/allowed/*" };

        Assert.True(_validator.IsAllowed(new Uri("https://raw.github.com/file"), whitelist));
        Assert.True(_validator.IsAllowed(new Uri("https://learn.microsoft.com/en-us/dotnet"), whitelist));
        Assert.True(_validator.IsAllowed(new Uri("https://example.com/allowed/doc.html"), whitelist));

        Assert.False(_validator.IsAllowed(new Uri("https://example.com/blocked/doc.html"), whitelist));
        Assert.False(_validator.IsAllowed(new Uri("https://evil.com"), whitelist));
    }
}
