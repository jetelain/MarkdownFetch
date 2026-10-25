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
    public void LoadPolicy_SystemConfigTakesPrecedenceOverEnvironmentVariable()
    {
        var prevEnv = Environment.GetEnvironmentVariable("MDFETCH_SAFETY_CONFIG");
        try
        {
            Environment.SetEnvironmentVariable("MDFETCH_SAFETY_CONFIG", "/tmp/agent-env-config.json");

            var systemJson = """
            {
                "urlWhitelist": ["system.example.com"],
                "forceFilterHiddenText": true
            }
            """;

            var agentJson = """
            {
                "urlWhitelist": ["evil.example.com"],
                "filterHiddenText": false
            }
            """;

            var files = new Dictionary<string, string>
            {
                ["/etc/mdfetch/safety.json"] = systemJson,
                ["/tmp/agent-env-config.json"] = agentJson
            };

            // Custom search paths is null to test standard lookup
            var loader = new SafetyConfigurationLoader(
                customSearchPaths: null,
                fileExists: path => files.ContainsKey(path),
                readFile: path => files[path]);

            var policy = loader.LoadPolicy();

            // Must have loaded the system config, NOT the agent's environment variable!
            Assert.Equal("/etc/mdfetch/safety.json", policy.LoadedFromPath);
            Assert.Contains("system.example.com", policy.SystemUrlWhitelist);
            Assert.DoesNotContain("evil.example.com", policy.SystemUrlWhitelist);
            Assert.True(policy.ForceFilterHiddenText);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MDFETCH_SAFETY_CONFIG", prevEnv);
        }
    }

    [Fact]
    public void LoadPolicy_UsesEnvironmentVariableWhenNoSystemConfigFileExists()
    {
        var prevEnv = Environment.GetEnvironmentVariable("MDFETCH_SAFETY_CONFIG");
        try
        {
            Environment.SetEnvironmentVariable("MDFETCH_SAFETY_CONFIG", "/custom/env-safety.json");

            var envJson = """
            {
                "urlWhitelist": ["env.example.com"],
                "forceFilterHiddenText": true
            }
            """;

            var files = new Dictionary<string, string>
            {
                ["/custom/env-safety.json"] = envJson
            };

            // No /etc/... files exist
            var loader = new SafetyConfigurationLoader(
                customSearchPaths: null,
                fileExists: path => files.ContainsKey(path),
                readFile: path => files[path]);

            var policy = loader.LoadPolicy();

            Assert.Equal("/custom/env-safety.json", policy.LoadedFromPath);
            Assert.Contains("env.example.com", policy.SystemUrlWhitelist);
            Assert.True(policy.ForceFilterHiddenText);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MDFETCH_SAFETY_CONFIG", prevEnv);
        }
    }

    [Fact]
    public void LoadPolicy_SystemWhitelistCannotBeWidenedByExplicitConfig()
    {
        var systemJson = """
        {
            "urlWhitelist": ["*.github.com"]
        }
        """;

        var cliJson = """
        {
            "urlWhitelist": ["evil.com"]
        }
        """;

        var files = new Dictionary<string, string>
        {
            ["/etc/mdfetch/safety.json"] = systemJson,
            ["/tmp/user.json"] = cliJson
        };

        var loader = new SafetyConfigurationLoader(
            customSearchPaths: new[] { "/etc/mdfetch/safety.json" },
            fileExists: path => files.ContainsKey(path),
            readFile: path => files[path]);

        var policy = loader.LoadPolicy("/tmp/user.json");

        // System whitelist is isolated and contains only *.github.com
        Assert.Single(policy.SystemUrlWhitelist);
        Assert.Contains("*.github.com", policy.SystemUrlWhitelist);
        Assert.DoesNotContain("evil.com", policy.SystemUrlWhitelist);
    }
}
