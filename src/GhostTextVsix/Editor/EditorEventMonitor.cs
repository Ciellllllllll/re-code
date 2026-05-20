using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
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
    private bool _disposed;

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
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_cookie != 0)
        {
            try
            {
                _monitorSelection.UnadviseSelectionEvents(_cookie);
            }
            catch (ObjectDisposedException ex)
            {
                SafeLogDisposeWarning("EditorEventMonitor dispose ignored ObjectDisposedException", ex);
            }
            catch (COMException ex)
            {
                SafeLogDisposeWarning("EditorEventMonitor dispose ignored COMException", ex);
            }
            catch (InvalidOperationException ex)
            {
                SafeLogDisposeWarning("EditorEventMonitor dispose ignored InvalidOperationException", ex);
            }
            finally
            {
                _cookie = 0;
            }
        }
    }

    private void SafeLogDisposeWarning(string message, Exception ex)
    {
        var safeMessage = $"{message}. ErrorType={ex.GetType().Name}";
        try
        {
            _logger?.Warning(safeMessage);
        }
        catch
        {
            Debug.WriteLine(safeMessage);
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
        GhostTextBroker.DismissAllIfCaretOrSelectionChanged("SelectionChanged");

        return VSConstants.S_OK;
    }
}
