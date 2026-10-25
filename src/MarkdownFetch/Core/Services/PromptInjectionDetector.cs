using System.Text.RegularExpressions;
using MarkdownFetch.Core.Models;
using MarkdownFetch.Core.Ports;

namespace MarkdownFetch.Core.Services;

/// <summary>
/// Scans content for prompt injection patterns, special control tokens, and adversarial agent directives.
/// </summary>
public sealed partial class PromptInjectionDetector : IPromptInjectionDetector
{
    private sealed record InjectionRule(string Name, Regex Pattern);

    [GeneratedRegex(@"\b(?:ignore|disregard|forget|skip|override|bypass)\s+(?:all\s+)?(?:previous|prior|above|former|earlier|initial)\s+(?:instructions|directions|commands|rules|prompts|guidelines|context)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DirectiveOverrideRegex();

    [GeneratedRegex(@"\b(?:disregard|ignore)\s+everything\s+(?:above|previously\s+stated|prior)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DisregardContextRegex();

    [GeneratedRegex(@"\b(?:new\s+instructions?|system\s+override|admin\s+override)\s*:\s*(?:ignore|you\s+are|act\s+as|forget)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SystemOverrideDirectiveRegex();

    [GeneratedRegex(@"\b(?:you\s+must\s+now|from\s+now\s+on\s+you\s+(?:must|will)|you\s+are\s+now)\s+(?:act\s+as|roleplay\s+as|operate\s+as|become|in\s+developer\s+mode|unrestricted|jailbroken)\b", RegexOptions.IgnoreCase)]
    private static partial Regex PersonaHijackRegex();

    [GeneratedRegex(@"\bact\s+as\s+(?:DAN|an\s+unrestricted|a\s+jailbroken|developer\s+mode)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DanJailbreakRegex();

    [GeneratedRegex(@"<\|(?:im_start|im_end|endoftext|fim_prefix|fim_suffix)\|>")]
    private static partial Regex ChatMlControlTokenRegex();

    [GeneratedRegex(@"(?:\[INST\]|\[/INST\]|<<SYS>>|<</SYS>>)")]
    private static partial Regex LlamaInstructionDelimiterRegex();

    [GeneratedRegex(@"(?:<turn_start>\s*(?:system|user|model)|<turn_end>)", RegexOptions.IgnoreCase)]
    private static partial Regex TurnDelimiterInjectionRegex();

    [GeneratedRegex(@"(?:^|\n)\s*(?:System|Human|Assistant)\s*:\s*(?:ignore\s+all|disregard|you\s+are\s+now|override|new\s+instructions?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex RoleTurnInjectionRegex();

    [GeneratedRegex(@"\b(?:print|reveal|output|display|show|leak|echo|send|exfiltrate)\s+(?:your\s+)?(?:system\s+(?:prompt|instructions?)|initial\s+(?:prompt|instructions?)|secret\s+key|api\s+key|passwords?|credentials?|environment\s+variables?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SecretExfiltrationDirectiveRegex();

    [GeneratedRegex(@"\b(?:IMPORTANT|CRITICAL|NOTE\s+TO|ATTENTION)\s*(?::\s*)?(?:AI|LLM|AGENT|ASSISTANT|CHATGPT|CLAUDE|GEMINI)\s*[:,]\s*(?:ignore|disregard|do not|you must|override|forget)\b", RegexOptions.IgnoreCase)]
    private static partial Regex TargetedAgentDirectiveRegex();

    [GeneratedRegex(@"<!--\s*(?:AI|LLM|AGENT|ASSISTANT)\s*(?:INSTRUCTION|PROMPT)\s*:", RegexOptions.IgnoreCase)]
    private static partial Regex HiddenCommentInjectionRegex();

    private static readonly InjectionRule[] Rules =
    [
        new("Directive Override", DirectiveOverrideRegex()),
        new("Disregard Context", DisregardContextRegex()),
        new("System Override Directive", SystemOverrideDirectiveRegex()),
        new("Persona Hijack / Developer Mode", PersonaHijackRegex()),
        new("DAN Jailbreak", DanJailbreakRegex()),
        new("ChatML Control Token", ChatMlControlTokenRegex()),
        new("Llama Instruction Delimiter", LlamaInstructionDelimiterRegex()),
        new("Turn Delimiter Injection", TurnDelimiterInjectionRegex()),
        new("Role Turn Injection", RoleTurnInjectionRegex()),
        new("Secret Exfiltration Directive", SecretExfiltrationDirectiveRegex()),
        new("Targeted Agent Directive", TargetedAgentDirectiveRegex()),
        new("Hidden Comment Injection", HiddenCommentInjectionRegex())
    ];

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
