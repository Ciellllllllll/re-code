namespace GhostTextVsix.Completion;

internal enum InsertMode
{
    InlineSuffix,
    CommentExpansion,
    EmptyLineInsertion,
    BlockInsertion,
    ReplaceSelection,
    ReplaceCurrentLineTail
}
