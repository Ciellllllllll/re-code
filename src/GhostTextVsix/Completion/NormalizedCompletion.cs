namespace GhostTextVsix.Completion;

internal sealed class NormalizedCompletion
{
    public string RawText { get; set; } = string.Empty;

    public string DisplayText { get; set; } = string.Empty;

    public string CommitText { get; set; } = string.Empty;

    public int LineCount { get; set; }

    public int CommitTextLength => CommitText?.Length ?? 0;

    public InsertMode InsertMode { get; set; }

    public string BaseIndent { get; set; } = string.Empty;

    public string BaseIndentKind { get; set; } = "None";

    public bool FirstLineInline { get; set; }

    public int RemovedCommonIndentLength { get; set; }

    public int LinesBefore { get; set; }

    public int LinesAfter { get; set; }

    public int CommitSpanStart { get; set; }

    public int CommitSpanLength { get; set; }

    public int ExpectedSnapshotVersion { get; set; }

    public long RequestId { get; set; }

    public string Source { get; set; } = string.Empty;

    public string CompletionMode { get; set; } = string.Empty;

    public bool IsCurrentLineCommentOnly { get; set; }

    public CompletionCommitPlan CommitPlan { get; set; }
}
