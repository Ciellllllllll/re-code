using System;
using System.IO;
using System.Linq;
using GhostTextVsix.Diagnostics;
using GhostTextVsix.Security;
using Microsoft.VisualStudio.Text.Editor;

namespace GhostTextVsix.Editor;

internal sealed class EditorContextCollector
{
    private readonly CppDocumentDetector _detector;
    private readonly SecurityFilter _securityFilter;
    private readonly DeepSeekOutputLogger _logger;

    public EditorContextCollector(CppDocumentDetector detector, SecurityFilter securityFilter, DeepSeekOutputLogger logger)
    {
        _detector = detector;
        _securityFilter = securityFilter;
        _logger = logger;
    }

    public bool TryCollect(IWpfTextView view, int maxPrefixLines, int maxSuffixLines, out EditorContext context)
    {
        context = null;
        var filePath = view.TextBuffer.GetFileName();
        if (!_detector.IsSupported(filePath) || !_securityFilter.IsPathAllowed(filePath))
        {
            return false;
        }

        var snapshot = view.TextSnapshot;
        var caretPosition = view.Caret.Position.BufferPosition.Position;
        var line = snapshot.GetLineFromPosition(caretPosition);

        var startLine = Math.Max(0, line.LineNumber - maxPrefixLines);
        var endLine = Math.Min(snapshot.LineCount - 1, line.LineNumber + maxSuffixLines);

        var prefixStart = snapshot.GetLineFromLineNumber(startLine).Start.Position;
        var suffixEnd = snapshot.GetLineFromLineNumber(endLine).End.Position;

        var prefix = snapshot.GetText(prefixStart, caretPosition - prefixStart);
        var suffix = snapshot.GetText(caretPosition, suffixEnd - caretPosition);
        var currentLineText = line.GetText();
        var currentLinePrefix = snapshot.GetText(line.Start.Position, caretPosition - line.Start.Position);
        var currentLineSuffix = snapshot.GetText(caretPosition, line.End.Position - caretPosition);
        var indent = new string(currentLineText.TakeWhile(char.IsWhiteSpace).ToArray());
        var isCurrentLineIndentOnly = string.IsNullOrWhiteSpace(currentLinePrefix);
        var selectedSpans = view.Selection.SelectedSpans;
        var isSelectionEmpty = view.Selection.IsEmpty || selectedSpans.Count == 0;
        var selectionStart = isSelectionEmpty ? caretPosition : selectedSpans[0].Start.Position;
        var selectionEnd = isSelectionEmpty ? caretPosition : selectedSpans[selectedSpans.Count - 1].End.Position;

        context = new EditorContext
        {
            FilePath = filePath,
            CaretPosition = caretPosition,
            Prefix = _securityFilter.MaskSecrets(prefix),
            Suffix = _securityFilter.MaskSecrets(suffix),
            SnapshotVersion = snapshot.Version.VersionNumber,
            CurrentLineStart = line.Start.Position,
            CurrentLineEnd = line.End.Position,
            CurrentLineText = currentLineText,
            CurrentLinePrefix = _securityFilter.MaskSecrets(currentLinePrefix),
            CurrentLineSuffix = _securityFilter.MaskSecrets(currentLineSuffix),
            CurrentLineIndent = indent,
            IsCurrentLineIndentOnly = isCurrentLineIndentOnly,
            IsCaretAtLineEnd = caretPosition == line.End.Position,
            IsCurrentLineCommentOnly = IsCommentOnly(currentLineText),
            IsCurrentLineEmptyOrWhitespace = string.IsNullOrWhiteSpace(currentLineText),
            IsSelectionEmpty = isSelectionEmpty,
            SelectionStart = selectionStart,
            SelectionEnd = selectionEnd,
            Language = "C/C++"
        };

        _logger.Info($"Collected context for {Path.GetFileName(filePath)} at position {caretPosition}.");
        return true;
    }

    private static bool IsCommentOnly(string line)
    {
        var trimmed = (line ?? string.Empty).Trim();
        return trimmed.StartsWith("//", StringComparison.Ordinal) ||
               (trimmed.StartsWith("/*", StringComparison.Ordinal) && trimmed.EndsWith("*/", StringComparison.Ordinal));
    }
}
