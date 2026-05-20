namespace GhostTextVsix.Editor;

internal sealed class EditorContext
{
    public string FilePath { get; set; }
    public int CaretPosition { get; set; }
    public string Prefix { get; set; }
    public string Suffix { get; set; }
    public string CurrentLinePrefix { get; set; }
    public string CurrentLineIndent { get; set; }
}
