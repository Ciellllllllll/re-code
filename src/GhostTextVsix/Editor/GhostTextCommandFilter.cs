using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;

namespace GhostTextVsix.Editor;

internal sealed class GhostTextCommandFilter : IOleCommandTarget
{
    private readonly IWpfTextView _view;
    private readonly IVsEditorAdaptersFactoryService _editorAdaptersFactoryService;
    private readonly ICompletionBroker _completionBroker;
    private readonly CppDocumentDetector _detector = new();
    private IOleCommandTarget _next;

    public GhostTextCommandFilter(
        IWpfTextView view,
        IVsEditorAdaptersFactoryService editorAdaptersFactoryService,
        ICompletionBroker completionBroker)
    {
        _view = view;
        _editorAdaptersFactoryService = editorAdaptersFactoryService;
        _completionBroker = completionBroker;
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
        var commandName = GetCommandName(pguidCmdGroup, nCmdID);
        if (!_detector.IsSupported(_view.TextBuffer.GetFileName()))
        {
            if (IsTabCommand(pguidCmdGroup, nCmdID))
            {
                GhostTextBroker.LogInfo($"Tab passed to next command target. Reason=UnsupportedFile, CommandGroup={pguidCmdGroup}, CommandId={nCmdID}, CommandName={commandName}");
            }

            return _next?.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut) ?? VSConstants.S_OK;
        }

        var shouldScheduleAutoCompletion = false;
        if (IsTabCommand(pguidCmdGroup, nCmdID))
        {
            var ghostTextActive = GhostTextBroker.IsActive(_view);
            var intelliSenseActive = IsIntelliSenseSessionActive();
            GhostTextBroker.LogInfo($"Tab command received. CommandGroup={pguidCmdGroup}, CommandId={nCmdID}, CommandName={commandName}, GhostText active on Tab={ghostTextActive}, IntelliSense session active={intelliSenseActive}");

            if (ghostTextActive)
            {
                GhostTextBroker.LogInfo($"GhostText accept started. IntelliSense session active={intelliSenseActive}");
                DismissIntelliSenseSessionsForGhostTextAccept();
                if (GhostTextBroker.Accept(_view))
                {
                    return VSConstants.S_OK;
                }
            }

            GhostTextBroker.LogInfo($"Tab passed to next command target. CommandGroup={pguidCmdGroup}, CommandId={nCmdID}, CommandName={commandName}, GhostText active on Tab={ghostTextActive}, IntelliSense session active={intelliSenseActive}");
            return _next?.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut) ?? VSConstants.S_OK;
        }

        if (pguidCmdGroup == VSConstants.VSStd2K)
        {
            if (nCmdID == (uint)VSConstants.VSStd2KCmdID.CANCEL && GhostTextBroker.Dismiss(_view, "Esc"))
            {
                return VSConstants.S_OK;
            }

            switch (GetVsStd2KCommandId(nCmdID))
            {
                case VSConstants.VSStd2KCmdID.TYPECHAR:
                    GhostTextBroker.Dismiss(_view, $"Command:{commandName}");
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
                    GhostTextBroker.Dismiss(_view, $"Command:{commandName}");
                    GhostTextBroker.CancelAutoCompletion($"Command:{commandName}");
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

    private static bool IsTabCommand(Guid commandGroup, uint commandId)
    {
        return commandGroup == VSConstants.VSStd2K
            && commandId == (uint)VSConstants.VSStd2KCmdID.TAB;
    }

    private static string GetCommandName(Guid commandGroup, uint commandId)
    {
        try
        {
            if (commandGroup == VSConstants.VSStd2K)
            {
                var converted = GetVsStd2KCommandId(commandId);
                return converted.HasValue
                    ? converted.Value.ToString()
                    : $"UnknownCommand({commandId})";
            }
        }
        catch
        {
            return $"UnknownCommand({commandId})";
        }

        return $"UnknownCommand({commandId})";
    }

    private static VSConstants.VSStd2KCmdID? GetVsStd2KCommandId(uint commandId)
    {
        if (commandId > int.MaxValue)
        {
            return null;
        }

        var intCommandId = unchecked((int)commandId);
        try
        {
            return Enum.IsDefined(typeof(VSConstants.VSStd2KCmdID), intCommandId)
                ? (VSConstants.VSStd2KCmdID)intCommandId
                : null;
        }
        catch
        {
            return null;
        }
    }
}
