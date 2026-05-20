using System.ComponentModel.Composition;
using GhostTextVsix.Diagnostics;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace GhostTextVsix.Editor;

[Export(typeof(IWpfTextViewCreationListener))]
[ContentType("text")]
[TextViewRole(PredefinedTextViewRoles.Document)]
internal sealed class GhostTextViewCreationListener : IWpfTextViewCreationListener
{
    public const string LayerName = "GhostTextAdornmentLayer";
    private readonly CppDocumentDetector _detector = new();

    [Import]
    internal IVsEditorAdaptersFactoryService EditorAdaptersFactoryService { get; set; }

    [Import]
    internal ICompletionBroker CompletionBroker { get; set; }

    [Export(typeof(AdornmentLayerDefinition))]
    [Name(LayerName)]
    [Order(After = PredefinedAdornmentLayers.Text)]
    public AdornmentLayerDefinition EditorAdornmentLayer;

    public void TextViewCreated(IWpfTextView textView)
    {
        var commandFilter = new GhostTextCommandFilter(textView, EditorAdaptersFactoryService, CompletionBroker);
        commandFilter.Attach();

        textView.LayoutChanged += (_, _) =>
        {
            if (!_detector.IsSupported(textView.TextBuffer.GetFileName()))
            {
                return;
            }

            if (GhostTextBroker.TryGetExisting(textView, out var session))
            {
                session.Render(textView);
            }
        };
        textView.Caret.PositionChanged += (_, _) =>
        {
            if (_detector.IsSupported(textView.TextBuffer.GetFileName()))
            {
                GhostTextBroker.DismissIfCaretOrSelectionChanged(textView, "CaretMoved");
            }
        };
        textView.TextBuffer.Changed += (_, _) =>
        {
            if (_detector.IsSupported(textView.TextBuffer.GetFileName()))
            {
                GhostTextBroker.Dismiss(textView, "BufferChanged");
            }
        };
        textView.Closed += (_, _) =>
        {
            GhostTextBroker.Dismiss(textView, "ViewClosed");
            GhostTextBroker.Remove(textView);
        };
    }
}
