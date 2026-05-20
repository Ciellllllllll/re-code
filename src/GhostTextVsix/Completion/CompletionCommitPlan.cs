namespace GhostTextVsix.Completion;

internal sealed class CompletionCommitPlan
{
    public int CommitSpanStart { get; set; }

    public int CommitSpanLength { get; set; }

    public string CommitText { get; set; } = string.Empty;

    public int NewCaretPosition => CommitSpanStart + (CommitText?.Length ?? 0);

    public bool UsesTrackingPoint { get; set; }

    public int ExpectedSnapshotVersion { get; set; }
}
