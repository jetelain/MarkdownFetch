using MarkdownFetch.Core.Models;
using MarkdownFetch.Infrastructure.Safety;

namespace MarkdownFetch.Tests.Infrastructure;

public class SafetyConfigurationLoaderTests
{
    [Fact]
    public void LoadPolicy_WhenNoFilesExist_ReturnsDefaultSafePolicy()
    {
        var loader = new SafetyConfigurationLoader(
            customSearchPaths: new[] { "/etc/mdfetch/safety.json" },
            fileExists: _ => false,
            readFile: _ => throw new FileNotFoundException());

        var policy = loader.LoadPolicy();

        Assert.Empty(policy.UrlWhitelist);
        Assert.True(policy.FilterHiddenText);
        Assert.False(policy.ForceFilterHiddenText);
        Assert.True(policy.DetectPromptInjection);
        Assert.False(policy.ForceDetectPromptInjection);
        Assert.Equal(PromptInjectionAction.Block, policy.PromptInjectionAction);
        Assert.Null(policy.LoadedFromPath);
    }

    [Fact]
    public void LoadPolicy_SystemConfigLoadsAndEnforcesPolicies()
    {
        var json = """
        {
            "urlWhitelist": ["*.github.com", "learn.microsoft.com"],
            "forceFilterHiddenText": true,
            "forceDetectPromptInjection": true,
            "promptInjectionAction": "Block"
        }
        """;

        var loader = new SafetyConfigurationLoader(
            customSearchPaths: new[] { "/etc/mdfetch/safety.json" },
            fileExists: path => path == "/etc/mdfetch/safety.json",
            readFile: _ => json);

        var policy = loader.LoadPolicy();

        Assert.Equal(2, policy.UrlWhitelist.Count);
        Assert.Contains("*.github.com", policy.UrlWhitelist);
        Assert.Contains("learn.microsoft.com", policy.UrlWhitelist);
        Assert.True(policy.FilterHiddenText);
        Assert.True(policy.ForceFilterHiddenText);
        Assert.True(policy.DetectPromptInjection);
        Assert.True(policy.ForceDetectPromptInjection);
        Assert.Equal(PromptInjectionAction.Block, policy.PromptInjectionAction);
        Assert.Equal("/etc/mdfetch/safety.json", policy.LoadedFromPath);
    }

    [Fact]
    public void LoadPolicy_SystemConfigCannotBeTemperedByExplicitConfigToDisableGuards()
    {
        var systemJson = """
        {
            "forceFilterHiddenText": true,
            "forceDetectPromptInjection": true
        }
        """;

        var cliJson = """
        {
            "filterHiddenText": false,
            "detectPromptInjection": false
        }
        """;

        var files = new Dictionary<string, string>
        {
            ["/etc/mdfetch/safety.json"] = systemJson,
            ["/tmp/agent-tamper.json"] = cliJson
        };

        var loader = new SafetyConfigurationLoader(
            customSearchPaths: new[] { "/etc/mdfetch/safety.json" },
            fileExists: path => files.ContainsKey(path),
            readFile: path => files[path]);

        var policy = loader.LoadPolicy("/tmp/agent-tamper.json");

        // The system config forced guards cannot be turned off by the CLI config
        Assert.True(policy.FilterHiddenText);
        Assert.True(policy.ForceFilterHiddenText);
        Assert.True(policy.DetectPromptInjection);
        Assert.True(policy.ForceDetectPromptInjection);
    }

    [Fact]
    public void LoadPolicy_SupportsNestedSafetySection()
    {
        var json = """
        {
            "safety": {
                "urlWhitelist": ["example.com"],
                "filterHiddenText": false,
                "detectPromptInjection": true,
                "promptInjectionAction": "Warn"
            }
        }
        """;

        var loader = new SafetyConfigurationLoader(
            customSearchPaths: new[] { "/etc/mdfetch/safety.json" },
            fileExists: _ => true,
            readFile: _ => json);

        var policy = loader.LoadPolicy();

        Assert.Single(policy.UrlWhitelist);
        Assert.Contains("example.com", policy.UrlWhitelist);
        Assert.False(policy.FilterHiddenText);
        Assert.True(policy.DetectPromptInjection);
        Assert.Equal(PromptInjectionAction.Warn, policy.PromptInjectionAction);
    }

    [Fact]
    public void LoadPolicy_InvalidJson_ThrowsInvalidOperationException()
    {
        var invalidJson = "{ not valid json: true ";

        var loader = new SafetyConfigurationLoader(
            customSearchPaths: new[] { "/etc/mdfetch/safety.json" },
            fileExists: _ => true,
            readFile: _ => invalidJson);

        Assert.Throws<InvalidOperationException>(() => loader.LoadPolicy());
    }

    [Fact]
    public void LoadPolicy_MissingExplicitFile_ThrowsFileNotFoundException()
    {
        var loader = new SafetyConfigurationLoader(
            customSearchPaths: Array.Empty<string>(),
            fileExists: _ => false,
            readFile: _ => throw new FileNotFoundException());

        Assert.Throws<FileNotFoundException>(() => loader.LoadPolicy("/non/existent/path.json"));
    }
}
