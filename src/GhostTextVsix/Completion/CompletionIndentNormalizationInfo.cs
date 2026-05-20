namespace GhostTextVsix.Completion;

internal sealed class CompletionIndentNormalizationInfo
{
    public int BaseIndentLength { get; set; }

    public string BaseIndentKind { get; set; } = "None";

    public int LinesBefore { get; set; }

    public int LinesAfter { get; set; }

    public bool FirstLineInline { get; set; }

    public int RemovedCommonIndentLength { get; set; }

    public static CompletionIndentNormalizationInfo Empty(string baseIndent)
    {
        return new CompletionIndentNormalizationInfo
        {
            BaseIndentLength = baseIndent?.Length ?? 0,
            BaseIndentKind = string.IsNullOrEmpty(baseIndent)
                ? "None"
                : baseIndent.IndexOf('\t') >= 0 && baseIndent.IndexOf(' ') >= 0
                    ? "Mixed"
                    : baseIndent.IndexOf('\t') >= 0
                        ? "Tabs"
                        : "Spaces",
            LinesBefore = 0,
            LinesAfter = 0,
            FirstLineInline = false,
            RemovedCommonIndentLength = 0
        };
    }
}
