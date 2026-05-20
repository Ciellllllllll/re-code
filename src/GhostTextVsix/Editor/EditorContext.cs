namespace GhostTextVsix.Editor;

internal sealed class EditorContext
{
    public string FilePath { get; set; }
    public int CaretPosition { get; set; }
    public string Prefix { get; set; }
    public string Suffix { get; set; }
    public int SnapshotVersion { get; set; }
    public int CurrentLineStart { get; set; }
    public int CurrentLineEnd { get; set; }
    public string CurrentLineText { get; set; }
    public string CurrentLinePrefix { get; set; }
    public string CurrentLineSuffix { get; set; }
    public string CurrentLineIndent { get; set; }
    public bool IsCurrentLineIndentOnly { get; set; }
    public bool IsCaretAtLineEnd { get; set; }
    public bool IsCurrentLineCommentOnly { get; set; }
    public bool IsCurrentLineEmptyOrWhitespace { get; set; }
    public bool IsSelectionEmpty { get; set; }
    public int SelectionStart { get; set; }
    public int SelectionEnd { get; set; }
    public string Language { get; set; }
}
