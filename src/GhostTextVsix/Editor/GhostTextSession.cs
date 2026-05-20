using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace GhostTextVsix.Editor;

internal sealed class GhostTextSession
{
    private string _text = string.Empty;
    private SnapshotPoint? _anchorPoint;
    private IAdornmentLayer _layer;

    public bool HasSuggestion => !string.IsNullOrEmpty(_text) && _anchorPoint.HasValue;

    public void Show(IWpfTextView view, string text)
    {
        _text = text;
        _anchorPoint = view.Caret.Position.BufferPosition;
        _layer = view.GetAdornmentLayer(GhostTextViewCreationListener.LayerName);
        Render(view);
    }

    public void Render(IWpfTextView view)
    {
        if (!HasSuggestion || _layer == null || !_anchorPoint.HasValue)
        {
            return;
        }

        _layer.RemoveAllAdornments();
        var anchor = _anchorPoint.Value.TranslateTo(view.TextSnapshot, PointTrackingMode.Positive);
        if (!view.TextViewLines.FormattedSpan.Contains(anchor))
        {
            return;
        }

        var line = view.GetTextViewLineContainingBufferPosition(anchor);
        var geometry = view.TextViewLines.GetMarkerGeometry(new SnapshotSpan(anchor, 0));
        if (geometry == null)
        {
            return;
        }

        var block = new TextBlock
        {
            Text = _text,
            Foreground = new SolidColorBrush(Color.FromArgb(120, 140, 140, 140)),
            FontFamily = view.FormattedLineSource.DefaultTextProperties.Typeface.FontFamily,
            FontSize = view.FormattedLineSource.DefaultTextProperties.FontRenderingEmSize,
            IsHitTestVisible = false
        };

        Canvas.SetLeft(block, geometry.Bounds.Left);
        Canvas.SetTop(block, line.Top);
        _layer.AddAdornment(AdornmentPositioningBehavior.TextRelative, new SnapshotSpan(anchor, 0), this, block, null);
    }

    public void Accept(IWpfTextView view)
    {
        if (!HasSuggestion || !_anchorPoint.HasValue)
        {
            return;
        }

        using var edit = view.TextBuffer.CreateEdit();
        var anchor = _anchorPoint.Value.TranslateTo(view.TextSnapshot, PointTrackingMode.Positive);
        edit.Insert(anchor.Position, _text);
        edit.Apply();
        view.Caret.MoveTo(new SnapshotPoint(view.TextSnapshot, anchor.Position + _text.Length));
        Dismiss();
    }

    public void Dismiss()
    {
        _text = string.Empty;
        _anchorPoint = null;
        _layer?.RemoveAllAdornments();
    }
}
