using System;
using System.Security.Cryptography;
using System.Text;
using GhostTextVsix.Editor;

namespace GhostTextVsix.Completion;

internal sealed class CompletionRequestSnapshot
{
    private CompletionRequestSnapshot()
    {
    }

    public string FilePath { get; private set; }

    public int CaretPosition { get; private set; }

    public int SnapshotVersion { get; private set; }

    public int CurrentLineStart { get; private set; }

    public int CurrentLineEnd { get; private set; }

    public string CurrentLineText { get; private set; }

    public string CurrentLinePrefix { get; private set; }

    public string CurrentLineSuffix { get; private set; }

    public string CurrentLineIndent { get; private set; }

    public bool IsCaretAtIndentOnly { get; private set; }

    public bool IsCaretAtLineEnd { get; private set; }

    public bool IsCurrentLineCommentOnly { get; private set; }

    public bool IsCurrentLineEmptyOrWhitespace { get; private set; }

    public bool IsSelectionEmpty { get; private set; }

    public int SelectionStart { get; private set; }

    public int SelectionEnd { get; private set; }

    public string Language { get; private set; }

    public string PrefixHash { get; private set; }

    public string SuffixHash { get; private set; }

    public string ModelName { get; private set; }

    public string ProviderName { get; private set; }

    public string BaseUrlHash { get; private set; }

    public int MaxTokens { get; private set; }

    public int MaxPrefixLines { get; private set; }

    public int MaxSuffixLines { get; private set; }

    public int MaxCompletionLines { get; private set; }

    public int MaxCompletionCharacters { get; private set; }

    public static CompletionRequestSnapshot Create(
        EditorContext context,
        string modelName,
        string providerName,
        string baseUrl,
        int maxTokens,
        int maxPrefixLines,
        int maxSuffixLines,
        int maxCompletionLines,
        int maxCompletionCharacters)
    {
        return new CompletionRequestSnapshot
        {
            FilePath = context.FilePath ?? string.Empty,
            CaretPosition = context.CaretPosition,
            SnapshotVersion = context.SnapshotVersion,
            CurrentLineStart = context.CurrentLineStart,
            CurrentLineEnd = context.CurrentLineEnd,
            CurrentLineText = context.CurrentLineText ?? string.Empty,
            CurrentLinePrefix = context.CurrentLinePrefix ?? string.Empty,
            CurrentLineSuffix = context.CurrentLineSuffix ?? string.Empty,
            CurrentLineIndent = context.CurrentLineIndent ?? string.Empty,
            IsCaretAtIndentOnly = context.IsCurrentLineIndentOnly,
            IsCaretAtLineEnd = context.IsCaretAtLineEnd,
            IsCurrentLineCommentOnly = context.IsCurrentLineCommentOnly,
            IsCurrentLineEmptyOrWhitespace = context.IsCurrentLineEmptyOrWhitespace,
            IsSelectionEmpty = context.IsSelectionEmpty,
            SelectionStart = context.SelectionStart,
            SelectionEnd = context.SelectionEnd,
            Language = context.Language ?? "C/C++",
            PrefixHash = Hash(context.Prefix),
            SuffixHash = Hash(context.Suffix),
            ModelName = modelName,
            ProviderName = providerName,
            BaseUrlHash = Hash(baseUrl),
            MaxTokens = maxTokens,
            MaxPrefixLines = maxPrefixLines,
            MaxSuffixLines = maxSuffixLines,
            MaxCompletionLines = maxCompletionLines,
            MaxCompletionCharacters = maxCompletionCharacters
        };
    }

    public bool Matches(EditorContext context)
    {
        return string.Equals(FilePath, context.FilePath ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
               CaretPosition == context.CaretPosition &&
               string.Equals(PrefixHash, Hash(context.Prefix), StringComparison.Ordinal) &&
               string.Equals(SuffixHash, Hash(context.Suffix), StringComparison.Ordinal);
    }

    public string CacheKey =>
        string.Join(
            "|",
            FilePath.ToUpperInvariant(),
            CaretPosition,
            PrefixHash,
            SuffixHash,
            ProviderName,
            ModelName,
            BaseUrlHash,
            MaxTokens,
            MaxPrefixLines,
            MaxSuffixLines,
            MaxCompletionLines,
            MaxCompletionCharacters);

    private static string Hash(string value)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
        return Convert.ToBase64String(bytes);
    }
}
