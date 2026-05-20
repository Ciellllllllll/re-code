using System.ComponentModel.Composition;
using System.Windows.Input;
using GhostTextVsix.Diagnostics;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
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

    [Import(AllowDefault = true)]
    internal IAsyncCompletionBroker AsyncCompletionBroker { get; set; }

    [Export(typeof(AdornmentLayerDefinition))]
    [Name(LayerName)]
    [Order(After = PredefinedAdornmentLayers.Text)]
    public AdornmentLayerDefinition EditorAdornmentLayer;

    public void TextViewCreated(IWpfTextView textView)
    {
        GhostTextInputArbiter.RegisterCompletionBrokers(CompletionBroker, AsyncCompletionBroker);
        var commandFilter = new GhostTextCommandFilter(textView, EditorAdaptersFactoryService, CompletionBroker, AsyncCompletionBroker);
        commandFilter.Attach();
        textView.VisualElement.AddHandler(
            Keyboard.PreviewKeyDownEvent,
            new KeyEventHandler((_, args) => HandlePreviewKeyDownFallback(textView, args)),
            true);

        textView.LayoutChanged += (_, _) =>
        {
            if (!_detector.IsSupported(textView.TextBuffer.GetFileName()))
            {
                return;
            }

            if (GhostTextBroker.TryGetExisting(textView, out var session))
            {
                session.Render(textView);
                GhostTextInputArbiter.SuppressIntelliSenseIfGhostTextActive(textView, "LayoutChanged", CompletionBroker, AsyncCompletionBroker);
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

    private void HandlePreviewKeyDownFallback(IWpfTextView textView, KeyEventArgs args)
    {
        if (args.Key != Key.Tab && args.SystemKey != Key.Tab)
        {
            return;
        }

        GhostTextBroker.LogInfo($"WPF PreviewKeyDown received. ViewId={GhostTextBroker.GetViewId(textView)}, AlreadyHandled={args.Handled}");
        if (!_detector.IsSupported(textView.TextBuffer.GetFileName()))
        {
            return;
        }

        var active = GhostTextInputArbiter.IsGhostTextActive(textView);
        GhostTextBroker.LogInfo($"GhostText active on Tab={active}. Source=WpfPreviewKeyDown, ViewId={GhostTextBroker.GetViewId(textView)}, Tab textView matches active session={GhostTextBroker.HasExactActiveSession(textView)}");
        if (!active)
        {
            return;
        }

        args.Handled = true;
        GhostTextInputArbiter.TryAcceptGhostTextFromTab(textView, "WpfPreviewKeyDown", CompletionBroker, AsyncCompletionBroker);
    }
}
