using System.ComponentModel.Composition;
using System.Windows.Input;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace GhostTextVsix.Editor;

[Export(typeof(IKeyProcessorProvider))]
[ContentType("text")]
[TextViewRole(PredefinedTextViewRoles.Document)]
[Name("GhostTextTabKeyProcessor")]
[Order(Before = "default")]
internal sealed class GhostTextTabKeyProcessorProvider : IKeyProcessorProvider
{
    [Import]
    internal ICompletionBroker CompletionBroker { get; set; }

    public KeyProcessor GetAssociatedProcessor(IWpfTextView wpfTextView)
    {
        return new GhostTextTabKeyProcessor(wpfTextView, CompletionBroker);
    }
}

internal sealed class GhostTextTabKeyProcessor : KeyProcessor
{
    private readonly IWpfTextView _view;
    private readonly ICompletionBroker _completionBroker;
    private readonly CppDocumentDetector _detector = new();

    public GhostTextTabKeyProcessor(IWpfTextView view, ICompletionBroker completionBroker)
    {
        _view = view;
        _completionBroker = completionBroker;
    }

    public override void PreviewKeyDown(KeyEventArgs args)
    {
        if (args.Key != Key.Tab || args.Handled)
        {
            return;
        }

        if (!_detector.IsSupported(_view.TextBuffer.GetFileName()))
        {
            return;
        }

        var ghostTextActive = GhostTextBroker.IsActive(_view);
        if (!ghostTextActive)
        {
            return;
        }

        var intelliSenseActive = IsIntelliSenseSessionActive();
        GhostTextBroker.LogInfo($"Tab command received. CommandGroup=KeyProcessor, CommandId=Tab, CommandName=PreviewKeyDown.Tab, GhostText active on Tab=True, IntelliSense session active={intelliSenseActive}");
        GhostTextBroker.LogInfo($"GhostText accept started. IntelliSense session active={intelliSenseActive}");
        DismissIntelliSenseSessionsForGhostTextAccept();

        if (GhostTextBroker.Accept(_view))
        {
            args.Handled = true;
            return;
        }

        GhostTextBroker.LogInfo($"Tab passed to next command target. CommandGroup=KeyProcessor, CommandId=Tab, CommandName=PreviewKeyDown.Tab, GhostText active on Tab={GhostTextBroker.IsActive(_view)}, IntelliSense session active={intelliSenseActive}");
    }

    private bool IsIntelliSenseSessionActive()
    {
        return _completionBroker != null && _completionBroker.IsCompletionActive(_view);
    }

    private void DismissIntelliSenseSessionsForGhostTextAccept()
    {
        if (_completionBroker == null || !_completionBroker.IsCompletionActive(_view))
        {
            return;
        }

        foreach (var session in _completionBroker.GetSessions(_view))
        {
            session.Dismiss();
        }

        GhostTextBroker.LogInfo("IntelliSense session dismissed for GhostText accept.");
    }
}
