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

    public string PrefixHash { get; private set; }

    public string SuffixHash { get; private set; }

    public string ModelName { get; private set; }

    public int MaxPrefixLines { get; private set; }

    public int MaxSuffixLines { get; private set; }

    public static CompletionRequestSnapshot Create(EditorContext context, string modelName, int maxPrefixLines, int maxSuffixLines)
    {
        return new CompletionRequestSnapshot
        {
            FilePath = context.FilePath ?? string.Empty,
            CaretPosition = context.CaretPosition,
            PrefixHash = Hash(context.Prefix),
            SuffixHash = Hash(context.Suffix),
            ModelName = modelName,
            MaxPrefixLines = maxPrefixLines,
            MaxSuffixLines = maxSuffixLines
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
        string.Join("|", FilePath.ToUpperInvariant(), CaretPosition, PrefixHash, SuffixHash, ModelName, MaxPrefixLines, MaxSuffixLines);

    private static string Hash(string value)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
        return Convert.ToBase64String(bytes);
    }
}
