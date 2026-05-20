using System;
using System.Linq;
using System.Text.RegularExpressions;
using GhostTextVsix.Editor;

namespace GhostTextVsix.Completion;

internal sealed class CompletionResponseFormatter
{
    private static readonly Regex FenceLineRegex = new(@"^\s*```(?:\w+|\+\+)?\s*$", RegexOptions.Compiled);
    private static readonly Regex CommentOnlyRegex = new(@"^\s*(//.*|/\*[\s\S]*\*/)\s*$", RegexOptions.Compiled);
    private static readonly Regex ExplanationPrefixRegex = new(@"^\s*(here|this|explanation|note|suggestion|completion)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex BulletPrefixRegex = new(@"^\s*[-*]\s+", RegexOptions.Compiled);

    public string Format(EditorContext context, string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var text = raw.Replace("\r\n", "\n").Trim();
        text = RemoveCodeFences(text);
        text = RemoveLeadingExplanation(text);
        text = RemovePrefixOverlap(context.Prefix, text);
        text = ApplyIndentation(context.CurrentLineIndent, text);
        text = TrimLength(text);

        if (string.IsNullOrWhiteSpace(text) || CommentOnlyRegex.IsMatch(text))
        {
            return string.Empty;
        }

        return text;
    }

    private static string RemoveCodeFences(string text)
    {
        var lines = text.Split('\n');
        var filtered = lines.Where(line => !FenceLineRegex.IsMatch(line));
        return string.Join("\n", filtered).Trim();
    }

    private static string RemoveLeadingExplanation(string text)
    {
        var lines = text.Split('\n').ToList();
        while (lines.Count > 0)
        {
            var candidate = lines[0].Trim();
            if (string.IsNullOrWhiteSpace(candidate))
            {
                lines.RemoveAt(0);
                continue;
            }

            if (ExplanationPrefixRegex.IsMatch(candidate) || BulletPrefixRegex.IsMatch(candidate))
            {
                lines.RemoveAt(0);
                continue;
            }

            if (LooksLikeCode(candidate))
            {
                break;
            }

            break;
        }

        return string.Join("\n", lines).Trim();
    }

    private static string RemovePrefixOverlap(string prefix, string completion)
    {
        if (string.IsNullOrEmpty(prefix) || string.IsNullOrEmpty(completion))
        {
            return completion;
        }

        var max = Math.Min(prefix.Length, completion.Length);
        for (var length = max; length > 0; length--)
        {
            if (prefix.EndsWith(completion.Substring(0, length), StringComparison.Ordinal))
            {
                return completion.Substring(length);
            }
        }

        return completion;
    }

    private static string ApplyIndentation(string indent, string text)
    {
        var lines = text.Split('\n');
        for (var i = 1; i < lines.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]))
            {
                lines[i] = indent + lines[i].TrimStart();
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string TrimLength(string text)
    {
        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).Take(12).ToArray();
        var truncated = string.Join(Environment.NewLine, lines);
        return truncated.Length > 1200 ? truncated.Substring(0, 1200) : truncated;
    }

    private static bool LooksLikeCode(string line)
    {
        return line.Contains(';') ||
               line.Contains('{') ||
               line.Contains('}') ||
               line.Contains('(') ||
               line.StartsWith("#", StringComparison.Ordinal) ||
               line.StartsWith("//", StringComparison.Ordinal) ||
               Regex.IsMatch(line, @"^\s*(if|for|while|switch|return|class|struct|template|auto|const|static)\b");
    }
}
