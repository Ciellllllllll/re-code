using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;

namespace GhostTextVsix.Editor;

internal sealed class ActiveTextViewLocator
{
    private readonly IVsTextManager _textManager;
    private readonly IVsEditorAdaptersFactoryService _editorAdaptersFactoryService;

    public ActiveTextViewLocator(IVsTextManager textManager, IVsEditorAdaptersFactoryService editorAdaptersFactoryService)
    {
        _textManager = textManager;
        _editorAdaptersFactoryService = editorAdaptersFactoryService;
    }

    public IWpfTextView GetActiveView()
    {
        Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
        _textManager.GetActiveView(1, null, out var activeView);
        return activeView == null ? null : _editorAdaptersFactoryService.GetWpfTextView(activeView);
    }
}
