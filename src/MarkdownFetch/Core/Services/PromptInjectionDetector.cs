using System.Text.RegularExpressions;
using MarkdownFetch.Core.Models;
using MarkdownFetch.Core.Ports;

namespace MarkdownFetch.Core.Services;

/// <summary>
/// Scans content for prompt injection patterns, special control tokens, and adversarial agent directives.
/// </summary>
public sealed class PromptInjectionDetector : IPromptInjectionDetector
{
    private sealed record InjectionRule(string Name, Regex Pattern);

    private static readonly List<InjectionRule> Rules = new()
    {
        // 1. Directive overrides and jailbreaks
        new("Directive Override", new Regex(
            @"\b(?:ignore|disregard|forget|skip|override|bypass)\s+(?:all\s+)?(?:previous|prior|above|former|earlier|initial)\s+(?:instructions|directions|commands|rules|prompts|guidelines|context)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),

        new("Disregard Context", new Regex(
            @"\b(?:disregard|ignore)\s+everything\s+(?:above|previously\s+stated|prior)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),

        new("System Override Directive", new Regex(
            @"\b(?:new\s+instructions?|system\s+override|admin\s+override)\s*:\s*(?:ignore|you\s+are|act\s+as|forget)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),

        new("Persona Hijack / Developer Mode", new Regex(
            @"\b(?:you\s+must\s+now|from\s+now\s+on\s+you\s+(?:must|will)|you\s+are\s+now)\s+(?:act\s+as|roleplay\s+as|operate\s+as|become|in\s+developer\s+mode|unrestricted|jailbroken)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),

        new("DAN Jailbreak", new Regex(
            @"\bact\s+as\s+(?:DAN|an\s+unrestricted|a\s+jailbroken|developer\s+mode)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),

        // 2. Delimiters and LLM control tokens
        new("ChatML Control Token", new Regex(
            @"<\|(?:im_start|im_end|endoftext|fim_prefix|fim_suffix)\|>",
            RegexOptions.Compiled)),

        new("Llama Instruction Delimiter", new Regex(
            @"(?:\[INST\]|\[/INST\]|<<SYS>>|<</SYS>>)",
            RegexOptions.Compiled)),

        new("Turn Delimiter Injection", new Regex(
            @"(?:<turn_start>\s*(?:system|user|model)|<turn_end>)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),

        new("Role Turn Injection", new Regex(
            @"(?:^|\n)\s*(?:System|Human|Assistant)\s*:\s*(?:ignore\s+all|disregard|you\s+are\s+now|override|new\s+instructions?)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),

        // 3. Credential and prompt exfiltration
        new("Secret Exfiltration Directive", new Regex(
            @"\b(?:print|reveal|output|display|show|leak|echo|send|exfiltrate)\s+(?:your\s+)?(?:system\s+(?:prompt|instructions?)|initial\s+(?:prompt|instructions?)|secret\s+key|api\s+key|passwords?|credentials?|environment\s+variables?)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),

        // 4. Adversarial agent instructions
        new("Targeted Agent Directive", new Regex(
            @"\b(?:IMPORTANT|CRITICAL|NOTE\s+TO|ATTENTION)\s*(?::\s*)?(?:AI|LLM|AGENT|ASSISTANT|CHATGPT|CLAUDE|GEMINI)\s*[:,]\s*(?:ignore|disregard|do not|you must|override|forget)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),

        new("Hidden Comment Injection", new Regex(
            @"<!--\s*(?:AI|LLM|AGENT|ASSISTANT)\s*(?:INSTRUCTION|PROMPT)\s*:",
            RegexOptions.IgnoreCase | RegexOptions.Compiled))
    };

    public PromptInjectionResult Detect(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return PromptInjectionResult.Clean(content ?? string.Empty);
        }

        string? firstMatchedRule = null;
        string? firstExcerpt = null;
        var sanitized = content;

        foreach (var rule in Rules)
        {
            var match = rule.Pattern.Match(content);
            if (match.Success)
            {
                if (firstMatchedRule == null)
                {
                    firstMatchedRule = rule.Name;
                    firstExcerpt = ExtractExcerpt(content, match.Index, match.Length);
                }

                sanitized = rule.Pattern.Replace(sanitized, "[PROMPT_INJECTION_DETECTED_AND_REMOVED]");
            }
        }

        if (firstMatchedRule != null)
        {
            return PromptInjectionResult.Detected(firstMatchedRule, firstExcerpt ?? string.Empty, sanitized);
        }

        return PromptInjectionResult.Clean(content);
    }

    private static string ExtractExcerpt(string content, int matchIndex, int matchLength)
    {
        const int contextPadding = 30;
        var start = Math.Max(0, matchIndex - contextPadding);
        var end = Math.Min(content.Length, matchIndex + matchLength + contextPadding);

        var prefix = start > 0 ? "..." : "";
        var suffix = end < content.Length ? "..." : "";

        var snippet = content[start..end].Replace("\r", " ").Replace("\n", " ");
        return $"{prefix}{snippet.Trim()}{suffix}";
    }
}
