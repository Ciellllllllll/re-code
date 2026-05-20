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

    public string Format(
        EditorContext context,
        string raw,
        int maxLines,
        int maxCharacters,
        out CompletionIndentNormalizationInfo indentInfo)
    {
        indentInfo = CompletionIndentNormalizationInfo.Empty(context?.CurrentLineIndent ?? string.Empty);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var text = TrimBlankLines(raw.Replace("\r\n", "\n"));
        text = RemoveCodeFences(text);
        text = RemoveLeadingExplanation(text);
        text = RemoveAlreadyTypedPrefix(context.CurrentLinePrefix, text);
        text = RemoveAlreadyTypedPrefix(context.Prefix, text);
        text = NormalizeIndentation(context.CurrentLineIndent, context.IsCurrentLineIndentOnly, text, out indentInfo);
        text = TrimLength(text, maxLines, maxCharacters);

        if (string.IsNullOrWhiteSpace(text) || CommentOnlyRegex.IsMatch(text))
        {
            return string.Empty;
        }

        return text;
    }

    public string Format(EditorContext context, string raw, int maxLines = 12, int maxCharacters = 1200)
    {
        return Format(context, raw, maxLines, maxCharacters, out _);
    }

    private static string RemoveCodeFences(string text)
    {
        var lines = text.Split('\n');
        var filtered = lines.Where(line => !FenceLineRegex.IsMatch(line));
        return TrimBlankLines(string.Join("\n", filtered));
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

        return TrimBlankLines(string.Join("\n", lines));
    }

    private static string RemoveAlreadyTypedPrefix(string prefix, string completion)
    {
        if (string.IsNullOrEmpty(prefix) || string.IsNullOrEmpty(completion))
        {
            return completion;
        }

        var linePrefix = GetLastLine(prefix);
        if (!string.IsNullOrEmpty(linePrefix) && completion.StartsWith(linePrefix, StringComparison.Ordinal))
        {
            return completion.Substring(linePrefix.Length);
        }

        var trimmedLinePrefix = linePrefix.TrimStart();
        if (!string.IsNullOrEmpty(trimmedLinePrefix) && completion.StartsWith(trimmedLinePrefix, StringComparison.Ordinal))
        {
            return completion.Substring(trimmedLinePrefix.Length);
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

    private static string GetLastLine(string text)
    {
        var normalized = text.Replace("\r\n", "\n");
        var index = normalized.LastIndexOf('\n');
        return index < 0 ? normalized : normalized.Substring(index + 1);
    }

    private static string TrimBlankLines(string text)
    {
        var lines = text.Split('\n').ToList();
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
        {
            lines.RemoveAt(0);
        }

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[lines.Count - 1]))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return string.Join("\n", lines);
    }

    private static string NormalizeIndentation(
        string baseIndent,
        bool firstLineIndentOnly,
        string text,
        out CompletionIndentNormalizationInfo info)
    {
        baseIndent ??= string.Empty;
        var lines = text.Split('\n');
        var firstLineInline = !firstLineIndentOnly;
        var commonIndent = GetCommonLeadingWhitespace(lines, firstLineInline ? 1 : 0);
        var linesBefore = lines.Length;

        for (var i = 0; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                lines[i] = string.Empty;
                continue;
            }

            var withoutCommonIndent = RemoveCommonIndent(lines[i], commonIndent);
            lines[i] = i == 0 && firstLineInline
                ? withoutCommonIndent
                : baseIndent + withoutCommonIndent;
        }

        info = new CompletionIndentNormalizationInfo
        {
            BaseIndentLength = baseIndent.Length,
            BaseIndentKind = GetIndentKind(baseIndent),
            LinesBefore = linesBefore,
            LinesAfter = lines.Length,
            FirstLineInline = firstLineInline,
            RemovedCommonIndentLength = commonIndent.Length
        };
        return string.Join(Environment.NewLine, lines);
    }

    private static string GetCommonLeadingWhitespace(string[] lines, int startIndex)
    {
        string common = null;
        for (var i = Math.Max(0, startIndex); i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var whitespace = new string(line.TakeWhile(char.IsWhiteSpace).ToArray());
            if (common == null)
            {
                common = whitespace;
                continue;
            }

            common = GetSharedPrefix(common, whitespace);
            if (common.Length == 0)
            {
                break;
            }
        }

        return common ?? string.Empty;
    }

    private static string GetSharedPrefix(string left, string right)
    {
        var length = Math.Min(left.Length, right.Length);
        var index = 0;
        while (index < length && left[index] == right[index])
        {
            index++;
        }

        return left.Substring(0, index);
    }

    private static string RemoveCommonIndent(string line, string commonIndent)
    {
        return !string.IsNullOrEmpty(commonIndent) && line.StartsWith(commonIndent, StringComparison.Ordinal)
            ? line.Substring(commonIndent.Length)
            : line.TrimStart();
    }

    private static string GetIndentKind(string indent)
    {
        if (string.IsNullOrEmpty(indent))
        {
            return "None";
        }

        var hasTabs = indent.IndexOf('\t') >= 0;
        var hasSpaces = indent.IndexOf(' ') >= 0;
        if (hasTabs && hasSpaces)
        {
            return "Mixed";
        }

        return hasTabs ? "Tabs" : "Spaces";
    }

    private static string TrimLength(string text, int maxLines, int maxCharacters)
    {
        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).Take(Math.Max(1, maxLines)).ToArray();
        var truncated = string.Join(Environment.NewLine, lines);
        var characterLimit = Math.Max(1, maxCharacters);
        return truncated.Length > characterLimit ? truncated.Substring(0, characterLimit) : truncated;
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
