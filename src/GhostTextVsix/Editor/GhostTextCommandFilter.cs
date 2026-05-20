using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;

namespace GhostTextVsix.Editor;

internal sealed class GhostTextCommandFilter : IOleCommandTarget
{
    private readonly IWpfTextView _view;
    private readonly IVsEditorAdaptersFactoryService _editorAdaptersFactoryService;
    private readonly CppDocumentDetector _detector = new();
    private IOleCommandTarget _next;

    public GhostTextCommandFilter(IWpfTextView view, IVsEditorAdaptersFactoryService editorAdaptersFactoryService)
    {
        _view = view;
        _editorAdaptersFactoryService = editorAdaptersFactoryService;
    }

    public void Attach()
    {
        var adapter = _editorAdaptersFactoryService.GetViewAdapter(_view);
        if (adapter is IVsTextView)
        {
            adapter.AddCommandFilter(this, out _next);
        }
    }

    public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
    {
        return _next?.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText) ?? VSConstants.S_OK;
    }

    public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
    {
        if (!_detector.IsSupported(_view.TextBuffer.GetFileName()))
        {
            return _next?.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut) ?? VSConstants.S_OK;
        }

        var shouldScheduleAutoCompletion = false;
        if (pguidCmdGroup == VSConstants.VSStd2K)
        {
            if (nCmdID == (uint)VSConstants.VSStd2KCmdID.TAB && GhostTextBroker.Accept(_view))
            {
                return VSConstants.S_OK;
            }

            if (nCmdID == (uint)VSConstants.VSStd2KCmdID.CANCEL && GhostTextBroker.Dismiss(_view, "Esc"))
            {
                return VSConstants.S_OK;
            }

            switch ((VSConstants.VSStd2KCmdID)nCmdID)
            {
                case VSConstants.VSStd2KCmdID.TYPECHAR:
                    GhostTextBroker.Dismiss(_view, $"Command:{(VSConstants.VSStd2KCmdID)nCmdID}");
                    GhostTextBroker.CancelAutoCompletion("TypeChar");
                    shouldScheduleAutoCompletion = true;
                    break;
                case VSConstants.VSStd2KCmdID.BACKSPACE:
                case VSConstants.VSStd2KCmdID.DELETE:
                case VSConstants.VSStd2KCmdID.LEFT:
                case VSConstants.VSStd2KCmdID.RIGHT:
                case VSConstants.VSStd2KCmdID.UP:
                case VSConstants.VSStd2KCmdID.DOWN:
                case VSConstants.VSStd2KCmdID.HOME:
                case VSConstants.VSStd2KCmdID.END:
                case VSConstants.VSStd2KCmdID.PAGEUP:
                case VSConstants.VSStd2KCmdID.PAGEDN:
                    GhostTextBroker.Dismiss(_view, $"Command:{(VSConstants.VSStd2KCmdID)nCmdID}");
                    GhostTextBroker.CancelAutoCompletion($"Command:{(VSConstants.VSStd2KCmdID)nCmdID}");
                    break;
            }
        }

        var result = _next?.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut) ?? VSConstants.S_OK;
        if (shouldScheduleAutoCompletion && ErrorHandler.Succeeded(result))
        {
            GhostTextBroker.ScheduleAutoCompletion(_view);
        }

        return result;
    }
}
