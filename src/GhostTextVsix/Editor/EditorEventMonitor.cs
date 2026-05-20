using System;
using GhostTextVsix.Completion;
using GhostTextVsix.Diagnostics;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace GhostTextVsix.Editor;

internal sealed class EditorEventMonitor : IVsSelectionEvents, IDisposable
{
    private readonly IVsMonitorSelection _monitorSelection;
    private readonly DeepSeekOutputLogger _logger;
    private uint _cookie;

    public EditorEventMonitor(
        IVsMonitorSelection monitorSelection,
        DeepSeekOutputLogger logger)
    {
        _monitorSelection = monitorSelection;
        _logger = logger;
    }

    public void Start()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        _monitorSelection.AdviseSelectionEvents(this, out _cookie);
    }

    public void Dispose()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (_cookie != 0)
        {
            _monitorSelection.UnadviseSelectionEvents(_cookie);
            _cookie = 0;
        }
    }

    public int OnCmdUIContextChanged(uint dwCmdUICookie, int fActive)
    {
        return VSConstants.S_OK;
    }

    public int OnElementValueChanged(uint elementid, object varValueOld, object varValueNew)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (elementid == (uint)VSConstants.VSSELELEMID.SEID_DocumentFrame)
        {
            GhostTextBroker.DismissAll("DocumentFrameChanged");
            _logger.Info("GhostText dismissed because the active document frame changed.");
        }

        return VSConstants.S_OK;
    }

    public int OnSelectionChanged(
        IVsHierarchy pHierOld,
        uint itemidOld,
        IVsMultiItemSelect pMISOld,
        ISelectionContainer pSCOld,
        IVsHierarchy pHierNew,
        uint itemidNew,
        IVsMultiItemSelect pMISNew,
        ISelectionContainer pSCNew)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        GhostTextBroker.DismissAll("SelectionChanged");

        return VSConstants.S_OK;
    }
}
