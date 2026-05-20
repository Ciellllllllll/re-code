using System;
using System.Linq;
using System.Text.RegularExpressions;
using GhostTextVsix.Editor;

namespace GhostTextVsix.Completion;

internal sealed class CompletionIndentNormalizer
{
    private static readonly Regex FenceLineRegex = new(@"^\s*```(?:\w+|\+\+)?\s*$", RegexOptions.Compiled);
    private static readonly Regex ExplanationPrefixRegex = new(@"^\s*(here|this|explanation|note|suggestion|completion)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex BulletPrefixRegex = new(@"^\s*[-*]\s+", RegexOptions.Compiled);

    public NormalizedCompletion Normalize(
        string rawText,
        CompletionRequestSnapshot snapshot,
        long requestId,
        string source,
        string completionMode,
        int maxLines,
        int maxCharacters)
    {
        rawText ??= string.Empty;
        var insertMode = DetermineInsertMode(snapshot);
        var cleaned = CleanRawText(rawText);
        cleaned = RemoveAlreadyTypedPrefix(snapshot.CurrentLinePrefix, cleaned);
        var normalizedBody = NormalizeIndent(cleaned, snapshot.CurrentLineIndent, insertMode, out var indentInfo);
        var commitText = BuildCommitText(normalizedBody, snapshot, insertMode);
        commitText = TrimLength(commitText, maxLines, maxCharacters);
        var displayText = commitText;
        var plan = BuildCommitPlan(commitText, snapshot, insertMode);

        return new NormalizedCompletion
        {
            RawText = rawText,
            DisplayText = displayText,
            CommitText = commitText,
            LineCount = CountLines(commitText),
            InsertMode = insertMode,
            BaseIndent = snapshot.CurrentLineIndent ?? string.Empty,
            BaseIndentKind = indentInfo.BaseIndentKind,
            FirstLineInline = indentInfo.FirstLineInline,
            RemovedCommonIndentLength = indentInfo.RemovedCommonIndentLength,
            LinesBefore = indentInfo.LinesBefore,
            LinesAfter = CountLines(commitText),
            CommitSpanStart = plan.CommitSpanStart,
            CommitSpanLength = plan.CommitSpanLength,
            ExpectedSnapshotVersion = snapshot.SnapshotVersion,
            RequestId = requestId,
            Source = source,
            CompletionMode = completionMode,
            IsCurrentLineCommentOnly = snapshot.IsCurrentLineCommentOnly,
            CommitPlan = plan
        };
    }

    public NormalizedCompletion Normalize(
        string rawText,
        EditorContext context,
        long requestId,
        string source,
        string completionMode,
        int maxLines,
        int maxCharacters)
    {
        var snapshot = CompletionRequestSnapshot.Create(
            context,
            string.Empty,
            string.Empty,
            string.Empty,
            0,
            0,
            0,
            maxLines,
            maxCharacters);
        return Normalize(rawText, snapshot, requestId, source, completionMode, maxLines, maxCharacters);
    }

    private static InsertMode DetermineInsertMode(CompletionRequestSnapshot snapshot)
    {
        if (!snapshot.IsSelectionEmpty && snapshot.SelectionEnd > snapshot.SelectionStart)
        {
            return InsertMode.ReplaceSelection;
        }

        if (snapshot.IsCurrentLineCommentOnly && StartsWithCode(CleanRawText(snapshot.CurrentLineText)) == false)
        {
            return InsertMode.CommentExpansion;
        }

        if (snapshot.IsCurrentLineCommentOnly)
        {
            return InsertMode.CommentExpansion;
        }

        if (snapshot.IsCurrentLineEmptyOrWhitespace || snapshot.IsCaretAtIndentOnly)
        {
            return InsertMode.EmptyLineInsertion;
        }

        return InsertMode.InlineSuffix;
    }

    private static string CleanRawText(string rawText)
    {
        var text = TrimBlankLines((rawText ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n'));
        text = RemoveCodeFences(text);
        text = RemoveLeadingExplanation(text);
        return TrimBlankLines(text);
    }

    private static string NormalizeIndent(
        string text,
        string baseIndent,
        InsertMode insertMode,
        out CompletionIndentNormalizationInfo info)
    {
        baseIndent ??= string.Empty;
        var lines = (text ?? string.Empty).Split('\n');
        var firstLineInline = insertMode == InsertMode.InlineSuffix;
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
        return string.Join("\n", lines);
    }

    private static string BuildCommitText(string normalizedBody, CompletionRequestSnapshot snapshot, InsertMode insertMode)
    {
        switch (insertMode)
        {
            case InsertMode.CommentExpansion:
                return Environment.NewLine + normalizedBody.Replace("\n", Environment.NewLine);
            case InsertMode.EmptyLineInsertion:
            case InsertMode.BlockInsertion:
            case InsertMode.ReplaceSelection:
            case InsertMode.ReplaceCurrentLineTail:
            case InsertMode.InlineSuffix:
            default:
                return normalizedBody.Replace("\n", Environment.NewLine);
        }
    }

    private static CompletionCommitPlan BuildCommitPlan(string commitText, CompletionRequestSnapshot snapshot, InsertMode insertMode)
    {
        var start = snapshot.CaretPosition;
        var length = 0;
        if (insertMode == InsertMode.CommentExpansion)
        {
            start = snapshot.CurrentLineEnd;
        }
        else if (insertMode == InsertMode.EmptyLineInsertion || insertMode == InsertMode.BlockInsertion)
        {
            start = snapshot.CurrentLineStart;
            length = Math.Max(0, snapshot.CaretPosition - snapshot.CurrentLineStart);
        }
        else if (insertMode == InsertMode.ReplaceSelection)
        {
            start = snapshot.SelectionStart;
            length = Math.Max(0, snapshot.SelectionEnd - snapshot.SelectionStart);
        }

        return new CompletionCommitPlan
        {
            CommitSpanStart = start,
            CommitSpanLength = length,
            CommitText = commitText,
            UsesTrackingPoint = true,
            ExpectedSnapshotVersion = snapshot.SnapshotVersion
        };
    }

    private static string RemoveCodeFences(string text)
    {
        var lines = text.Split('\n');
        var filtered = lines.Where(line => !FenceLineRegex.IsMatch(line));
        return TrimBlankLines(string.Join("\n", filtered));
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

        var max = Math.Min(linePrefix.Length, completion.Length);
        for (var length = max; length > 0; length--)
        {
            if (linePrefix.EndsWith(completion.Substring(0, length), StringComparison.Ordinal))
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

    private static string RemoveLeadingExplanation(string text)
    {
        var lines = text.Split('\n').ToList();
        while (lines.Count > 0)
        {
            var candidate = lines[0].Trim();
            if (string.IsNullOrWhiteSpace(candidate) ||
                ExplanationPrefixRegex.IsMatch(candidate) ||
                BulletPrefixRegex.IsMatch(candidate))
            {
                lines.RemoveAt(0);
                continue;
            }

            break;
        }

        return TrimBlankLines(string.Join("\n", lines));
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
            common = common == null ? whitespace : GetSharedPrefix(common, whitespace);
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

    private static int CountLines(string text)
    {
        return string.IsNullOrEmpty(text)
            ? 0
            : text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).Length;
    }

    private static bool StartsWithCode(string text)
    {
        var first = (text ?? string.Empty).TrimStart();
        return first.Contains(";") ||
               first.StartsWith("if", StringComparison.Ordinal) ||
               first.StartsWith("for", StringComparison.Ordinal) ||
               first.StartsWith("while", StringComparison.Ordinal) ||
               first.StartsWith("auto", StringComparison.Ordinal) ||
               first.StartsWith("int", StringComparison.Ordinal) ||
               first.StartsWith("std::", StringComparison.Ordinal);
    }
}
