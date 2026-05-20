using System.ComponentModel.Composition;
using System.Windows.Input;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
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

    [Import(AllowDefault = true)]
    internal IAsyncCompletionBroker AsyncCompletionBroker { get; set; }

    public KeyProcessor GetAssociatedProcessor(IWpfTextView wpfTextView)
    {
        return new GhostTextTabKeyProcessor(wpfTextView, CompletionBroker, AsyncCompletionBroker);
    }
}

internal sealed class GhostTextTabKeyProcessor : KeyProcessor
{
    private readonly IWpfTextView _view;
    private readonly ICompletionBroker _completionBroker;
    private readonly IAsyncCompletionBroker _asyncCompletionBroker;
    private readonly CppDocumentDetector _detector = new();

    public GhostTextTabKeyProcessor(
        IWpfTextView view,
        ICompletionBroker completionBroker,
        IAsyncCompletionBroker asyncCompletionBroker)
    {
        _view = view;
        _completionBroker = completionBroker;
        _asyncCompletionBroker = asyncCompletionBroker;
    }

    public override bool IsInterestedInHandledEvents => true;

    public override void PreviewKeyDown(KeyEventArgs args)
    {
        TryHandleTab(args, "PreviewKeyDown");
    }

    public override void KeyDown(KeyEventArgs args)
    {
        TryHandleTab(args, "KeyDown");
    }

    private void TryHandleTab(KeyEventArgs args, string eventName)
    {
        if (!IsTabKey(args))
        {
            return;
        }

        GhostTextBroker.LogInfo($"Tab key processor {eventName} received. ViewId={GhostTextBroker.GetViewId(_view)}, IsInterestedInHandledEvents={IsInterestedInHandledEvents}, AlreadyHandled={args.Handled}");
        if (!_detector.IsSupported(_view.TextBuffer.GetFileName()))
        {
            GhostTextBroker.LogInfo($"Tab ignored because non C/C++ view. Source=KeyProcessor.{eventName}, ViewId={GhostTextBroker.GetViewId(_view)}");
            return;
        }

        var textViewMatchesActiveSession = GhostTextBroker.HasExactActiveSession(_view);
        var ghostTextActive = GhostTextBroker.IsActive(_view);
        var asyncCompletionActive = IsAsyncCompletionSessionActive();
        var legacyCompletionActive = IsLegacyCompletionSessionActive();
        GhostTextBroker.LogInfo($"GhostText active on Tab={ghostTextActive}. Source=KeyProcessor.{eventName}, ViewId={GhostTextBroker.GetViewId(_view)}, Tab textView matches active session={textViewMatchesActiveSession}, AsyncCompletion active={asyncCompletionActive}, LegacyCompletion active={legacyCompletionActive}");
        if (!ghostTextActive)
        {
            GhostTextBroker.LogInfo($"Tab ignored because GhostText inactive. Source=KeyProcessor.{eventName}, ViewId={GhostTextBroker.GetViewId(_view)}");
            return;
        }

        args.Handled = true;
        if (GhostTextBroker.AcceptActiveGhostTextFromTab(_view, $"KeyProcessor.{eventName}", _completionBroker, _asyncCompletionBroker))
        {
            return;
        }

        GhostTextBroker.LogInfo($"Tab passed to next command target. CommandGroup=KeyProcessor, CommandId=Tab, CommandName={eventName}.Tab, ViewId={GhostTextBroker.GetViewId(_view)}, GhostText active on Tab={GhostTextBroker.IsActive(_view)}, AsyncCompletion active={asyncCompletionActive}, LegacyCompletion active={legacyCompletionActive}");
    }

    private static bool IsTabKey(KeyEventArgs args)
    {
        return args.Key == Key.Tab || args.SystemKey == Key.Tab;
    }

    private bool IsLegacyCompletionSessionActive()
    {
        return _completionBroker != null && _completionBroker.IsCompletionActive(_view);
    }

    private bool IsAsyncCompletionSessionActive()
    {
        return _asyncCompletionBroker != null && _asyncCompletionBroker.IsCompletionActive(_view);
    }
}
