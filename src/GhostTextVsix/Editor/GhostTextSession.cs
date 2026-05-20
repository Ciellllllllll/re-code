using System;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Media;
using GhostTextVsix.Completion;
using GhostTextVsix.Diagnostics;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;

namespace GhostTextVsix.Editor;

internal sealed class GhostTextSession
{
    private string _text = string.Empty;
    private NormalizedCompletion _completion;
    private SnapshotPoint? _anchorPoint;
    private ITrackingPoint _trackingPoint;
    private IAdornmentLayer _layer;
    private DeepSeekOutputLogger _logger;
    private SelectionSnapshot _selectionSnapshot;

    public bool HasSuggestion => !string.IsNullOrEmpty(_text) && _anchorPoint.HasValue;
    public NormalizedCompletion Completion => _completion;
    public bool IsAccepting => _accepting != 0;
    public bool IsAccepted { get; private set; }
    public bool IsDismissed { get; private set; } = true;
    public long RequestId { get; private set; }
    public string Source { get; private set; } = string.Empty;
    private int _accepting;

    public void Show(IWpfTextView view, string text, long requestId, string source, DeepSeekOutputLogger logger)
    {
        var completion = new NormalizedCompletion
        {
            DisplayText = text,
            CommitText = text,
            CommitPlan = new CompletionCommitPlan
            {
                CommitSpanStart = view.Caret.Position.BufferPosition.Position,
                CommitSpanLength = 0,
                CommitText = text,
                UsesTrackingPoint = true,
                ExpectedSnapshotVersion = view.TextSnapshot.Version.VersionNumber
            },
            CommitSpanStart = view.Caret.Position.BufferPosition.Position,
            CommitSpanLength = 0,
            ExpectedSnapshotVersion = view.TextSnapshot.Version.VersionNumber,
            RequestId = requestId,
            Source = source
        };
        Show(view, completion, requestId, source, logger);
    }

    public void Show(IWpfTextView view, NormalizedCompletion completion, long requestId, string source, DeepSeekOutputLogger logger)
    {
        _completion = completion;
        _text = completion?.DisplayText ?? string.Empty;
        _anchorPoint = view.Caret.Position.BufferPosition;
        _trackingPoint = view.TextSnapshot.CreateTrackingPoint(
            completion?.CommitPlan?.CommitSpanStart ?? _anchorPoint.Value.Position,
            PointTrackingMode.Positive);
        _selectionSnapshot = SelectionSnapshot.Create(view);
        RequestId = requestId;
        Source = source;
        _logger = logger;
        IsAccepted = false;
        IsDismissed = false;
        _accepting = 0;
        try
        {
            _layer = view.GetAdornmentLayer(GhostTextViewCreationListener.LayerName);
        }
        catch (Exception ex)
        {
            _layer = null;
            _logger?.Error($"GhostText adornment layer lookup failed. RequestId={RequestId}, Source={Source}, AdornmentLayerName={GhostTextViewCreationListener.LayerName}, Error={ex.GetType().Name}");
        }

        Render(view);
    }

    public bool HasCaretOrSelectionChanged(IWpfTextView view)
    {
        if (!HasSuggestion || !_anchorPoint.HasValue)
        {
            return false;
        }

        var anchor = _anchorPoint.Value.TranslateTo(view.TextSnapshot, PointTrackingMode.Positive);
        var currentCaret = view.Caret.Position.BufferPosition;
        if (currentCaret.Position != anchor.Position)
        {
            return true;
        }

        return !_selectionSnapshot.Matches(view);
    }

    public void Render(IWpfTextView view)
    {
        var textLength = _text?.Length ?? 0;
        var lines = CountLines(_text);
        var caretPosition = -1;
        var snapshotLength = view.TextSnapshot.Length;
        var isCaretInView = false;
        var hasSelection = view.Selection.SelectedSpans.Count > 0;
        var selectionIsEmpty = view.Selection.IsEmpty;
        var textViewClosed = view.IsClosed;
        var renderX = double.NaN;
        var renderY = double.NaN;
        var textBlockCreated = false;
        var addAdornmentCalled = false;
        var addAdornmentSucceeded = false;

        if (!HasSuggestion || !_anchorPoint.HasValue)
        {
            LogRender(
                view,
                textLength,
                lines,
                caretPosition,
                snapshotLength,
                isCaretInView,
                hasSelection,
                selectionIsEmpty,
                textViewClosed,
                renderX,
                renderY,
                textBlockCreated,
                addAdornmentCalled,
                addAdornmentSucceeded);
            return;
        }

        var anchor = _anchorPoint.Value.TranslateTo(view.TextSnapshot, PointTrackingMode.Positive);
        caretPosition = anchor.Position;
        isCaretInView = IsPointInFormattedSpan(view, anchor);

        if (_layer == null)
        {
            _logger?.Error($"GhostText render failed because adornment layer was not found. RequestId={RequestId}, Source={Source}, TextLength={textLength}, Lines={lines}, CaretPosition={caretPosition}, SnapshotLength={snapshotLength}, AdornmentLayerName={GhostTextViewCreationListener.LayerName}, AdornmentLayerFound=False, TextViewClosed={textViewClosed}");
            LogRender(
                view,
                textLength,
                lines,
                caretPosition,
                snapshotLength,
                isCaretInView,
                hasSelection,
                selectionIsEmpty,
                textViewClosed,
                renderX,
                renderY,
                textBlockCreated,
                addAdornmentCalled,
                addAdornmentSucceeded);
            return;
        }

        _layer.RemoveAllAdornments();
        if (!isCaretInView)
        {
            LogRender(
                view,
                textLength,
                lines,
                caretPosition,
                snapshotLength,
                isCaretInView,
                hasSelection,
                selectionIsEmpty,
                textViewClosed,
                renderX,
                renderY,
                textBlockCreated,
                addAdornmentCalled,
                addAdornmentSucceeded);
            return;
        }

        var line = view.GetTextViewLineContainingBufferPosition(anchor);
        try
        {
            var bounds = line.GetCharacterBounds(anchor);
            renderX = bounds.Left;
            renderY = line.Top;
        }
        catch (Exception)
        {
            var geometry = view.TextViewLines.GetMarkerGeometry(new SnapshotSpan(anchor, 0));
            if (geometry != null)
            {
                renderX = geometry.Bounds.Left;
                renderY = line.Top;
            }
        }

        if (double.IsNaN(renderX) || double.IsNaN(renderY))
        {
            LogRender(
                view,
                textLength,
                lines,
                caretPosition,
                snapshotLength,
                isCaretInView,
                hasSelection,
                selectionIsEmpty,
                textViewClosed,
                renderX,
                renderY,
                textBlockCreated,
                addAdornmentCalled,
                addAdornmentSucceeded);
            return;
        }

        var block = new TextBlock
        {
            Text = _text,
            Foreground = Brushes.Gray,
            Opacity = 0.8,
            FontFamily = view.FormattedLineSource.DefaultTextProperties.Typeface.FontFamily,
            FontSize = view.FormattedLineSource.DefaultTextProperties.FontRenderingEmSize,
            IsHitTestVisible = false
        };
        textBlockCreated = true;

        Canvas.SetLeft(block, renderX);
        Canvas.SetTop(block, renderY);
        addAdornmentCalled = true;
        var adornmentSpan = CreateAdornmentSpan(anchor, line, snapshotLength);
        addAdornmentSucceeded = _layer.AddAdornment(AdornmentPositioningBehavior.TextRelative, adornmentSpan, this, block, null);
        if (!addAdornmentSucceeded)
        {
            _logger?.Error($"GhostText AddAdornment failed. RequestId={RequestId}, Source={Source}, TextLength={textLength}, Lines={lines}, CaretPosition={caretPosition}, SnapshotLength={snapshotLength}, AdornmentLayerName={GhostTextViewCreationListener.LayerName}, RenderX={renderX:F1}, RenderY={renderY:F1}");
        }

        LogRender(
            view,
            textLength,
            lines,
            caretPosition,
            snapshotLength,
            isCaretInView,
            hasSelection,
            selectionIsEmpty,
            textViewClosed,
            renderX,
            renderY,
            textBlockCreated,
            addAdornmentCalled,
            addAdornmentSucceeded);
    }

    public bool TryBeginAccept()
    {
        return !IsAccepted &&
               !IsDismissed &&
               Interlocked.CompareExchange(ref _accepting, 1, 0) == 0;
    }

    public void EndAccept()
    {
        Interlocked.Exchange(ref _accepting, 0);
    }

    public int Accept(IWpfTextView view)
    {
        if (!HasSuggestion || !_anchorPoint.HasValue)
        {
            return -1;
        }

        var text = _text;
        var anchor = _anchorPoint.Value.TranslateTo(view.TextSnapshot, PointTrackingMode.Positive);
        Dismiss();

        using var edit = view.TextBuffer.CreateEdit();
        edit.Insert(anchor.Position, text);
        edit.Apply();
        var insertedEnd = anchor.Position + text.Length;
        view.Caret.MoveTo(new SnapshotPoint(view.TextSnapshot, insertedEnd));
        return insertedEnd;
    }

    public int AcceptPlan(IWpfTextView view)
    {
        if (!HasSuggestion || _completion?.CommitPlan == null)
        {
            return -1;
        }

        var plan = _completion.CommitPlan;
        var start = _trackingPoint?.GetPosition(view.TextSnapshot) ?? plan.CommitSpanStart;
        var length = plan.CommitSpanLength;
        if (start < 0 || start > view.TextSnapshot.Length || start + length > view.TextSnapshot.Length)
        {
            return -1;
        }

        var text = plan.CommitText ?? string.Empty;
        using var edit = view.TextBuffer.CreateEdit();
        if (length > 0)
        {
            edit.Replace(start, length, text);
        }
        else
        {
            edit.Insert(start, text);
        }

        edit.Apply();
        var insertedEnd = start + text.Length;
        view.Caret.MoveTo(new SnapshotPoint(view.TextSnapshot, insertedEnd));
        IsAccepted = true;
        Dismiss();
        return insertedEnd;
    }

    public void Dismiss()
    {
        _text = string.Empty;
        _completion = null;
        _anchorPoint = null;
        _trackingPoint = null;
        _selectionSnapshot = default;
        IsDismissed = true;
        RequestId = 0;
        Source = string.Empty;
        _logger = null;
        _layer?.RemoveAllAdornments();
    }

    private void LogRender(
        IWpfTextView view,
        int textLength,
        int lines,
        int caretPosition,
        int snapshotLength,
        bool isCaretInView,
        bool hasSelection,
        bool selectionIsEmpty,
        bool textViewClosed,
        double renderX,
        double renderY,
        bool textBlockCreated,
        bool addAdornmentCalled,
        bool addAdornmentSucceeded)
    {
        _logger?.Info($"GhostText render diagnostics. RequestId={RequestId}, Source={Source}, TextLength={textLength}, Lines={lines}, CaretPosition={caretPosition}, SnapshotLength={snapshotLength}, IsCaretInView={isCaretInView}, HasSelection={hasSelection}, SelectionIsEmpty={selectionIsEmpty}, AdornmentLayerName={GhostTextViewCreationListener.LayerName}, AdornmentLayerFound={_layer != null}, TextViewClosed={textViewClosed}, ViewportLeft={GetDouble(view.ViewportLeft):F1}, ViewportTop={GetDouble(view.ViewportTop):F1}, ViewportRight={GetDouble(view.ViewportRight):F1}, ViewportBottom={GetDouble(view.ViewportBottom):F1}, RenderX={renderX:F1}, RenderY={renderY:F1}, TextBlockCreated={textBlockCreated}, AddAdornmentCalled={addAdornmentCalled}, AddAdornmentSucceeded={addAdornmentSucceeded}");
    }

    private static bool IsPointInFormattedSpan(IWpfTextView view, SnapshotPoint point)
    {
        var formattedSpan = view.TextViewLines.FormattedSpan;
        return point.Position >= formattedSpan.Start.Position
            && point.Position <= formattedSpan.End.Position;
    }

    private static SnapshotSpan CreateAdornmentSpan(SnapshotPoint anchor, ITextViewLine line, int snapshotLength)
    {
        if (anchor.Position < line.Extent.End.Position)
        {
            return new SnapshotSpan(anchor, 1);
        }

        if (line.Extent.Length > 0)
        {
            return new SnapshotSpan(new SnapshotPoint(anchor.Snapshot, line.Extent.End.Position - 1), 1);
        }

        if (anchor.Position < snapshotLength)
        {
            return new SnapshotSpan(anchor, 1);
        }

        if (snapshotLength > 0)
        {
            return new SnapshotSpan(new SnapshotPoint(anchor.Snapshot, snapshotLength - 1), 1);
        }

        return new SnapshotSpan(anchor, 0);
    }

    private static int CountLines(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var count = 1;
        foreach (var ch in text)
        {
            if (ch == '\n')
            {
                count++;
            }
        }

        return count;
    }

    private static double GetDouble(double value)
    {
        return double.IsInfinity(value) || double.IsNaN(value) ? 0 : value;
    }

    private readonly struct SelectionSnapshot
    {
        private readonly bool _isEmpty;
        private readonly int _spanCount;
        private readonly int _start;
        private readonly int _end;

        private SelectionSnapshot(bool isEmpty, int spanCount, int start, int end)
        {
            _isEmpty = isEmpty;
            _spanCount = spanCount;
            _start = start;
            _end = end;
        }

        public static SelectionSnapshot Create(IWpfTextView view)
        {
            var spans = view.Selection.SelectedSpans;
            if (spans.Count == 0)
            {
                var caret = view.Caret.Position.BufferPosition.Position;
                return new SelectionSnapshot(true, 0, caret, caret);
            }

            return new SelectionSnapshot(
                view.Selection.IsEmpty,
                spans.Count,
                spans[0].Start.Position,
                spans[spans.Count - 1].End.Position);
        }

        public bool Matches(IWpfTextView view)
        {
            var spans = view.Selection.SelectedSpans;
            if (spans.Count == 0)
            {
                var caret = view.Caret.Position.BufferPosition.Position;
                return _isEmpty && _spanCount == 0 && _start == caret && _end == caret;
            }

            return _isEmpty == view.Selection.IsEmpty
                && _spanCount == spans.Count
                && _start == spans[0].Start.Position
                && _end == spans[spans.Count - 1].End.Position;
        }
    }
}
