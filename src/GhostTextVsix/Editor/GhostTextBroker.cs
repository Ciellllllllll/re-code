using System;
using System.Collections.Concurrent;
using System.Linq;
using GhostTextVsix.Completion;
using GhostTextVsix.Diagnostics;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;

namespace GhostTextVsix.Editor;

internal static class GhostTextBroker
{
    private static readonly ConcurrentDictionary<IWpfTextView, GhostTextSession> Sessions = new();
    private static readonly object ActiveSessionKey = new();
    private static CompletionCoordinator _coordinator;
    private static CppDocumentDetector _detector;
    private static CompletionCommitHandler _commitHandler;
    private static DeepSeekOutputLogger _logger;

    public static void Initialize(CompletionCoordinator coordinator, CppDocumentDetector detector, CompletionCommitHandler commitHandler, DeepSeekOutputLogger logger)
    {
        _coordinator = coordinator;
        _detector = detector;
        _commitHandler = commitHandler;
        _logger = logger;
    }

    public static GhostTextSession GetOrCreate(IWpfTextView view)
    {
        return Sessions.GetOrAdd(view, static _ => new GhostTextSession());
    }

    public static bool TryGetExisting(IWpfTextView view, out GhostTextSession session)
    {
        return Sessions.TryGetValue(view, out session);
    }

    public static bool TryGetActiveSession(IWpfTextView view, out GhostTextSession session)
    {
        if (view.Properties.TryGetProperty(ActiveSessionKey, out session) && session.HasSuggestion)
        {
            return true;
        }

        if (Sessions.TryGetValue(view, out session) && session.HasSuggestion)
        {
            RegisterActiveSession(view, session);
            return true;
        }

        foreach (var entry in Sessions.ToArray())
        {
            if (!entry.Value.HasSuggestion ||
                entry.Key == view ||
                entry.Key.TextBuffer != view.TextBuffer)
            {
                continue;
            }

            session = entry.Value;
            RegisterActiveSession(view, session);
            _logger?.Warning($"GhostText active session remapped to current view. OriginalViewId={GetViewId(entry.Key)}, ViewId={GetViewId(view)}, RequestId={session.RequestId}, Source={session.Source}");
            return true;
        }

        session = null;
        return false;
    }

    public static bool IsActive(IWpfTextView view)
    {
        return TryGetActiveSession(view, out _);
    }

    public static bool HasActiveSession(IWpfTextView view)
    {
        return TryGetActiveSession(view, out _);
    }

    public static bool HasExactActiveSession(IWpfTextView view)
    {
        return Sessions.TryGetValue(view, out var session) && session.HasSuggestion;
    }

    public static void LogInfo(string message)
    {
        _logger?.Info(message);
    }

    public static void LogWarning(string message)
    {
        _logger?.Warning(message);
    }

    public static void LogError(string message)
    {
        _logger?.Error(message);
    }

    public static bool IsSupportedView(IWpfTextView view)
    {
        return view != null && _detector != null && _detector.IsSupported(view.TextBuffer.GetFileName());
    }

    public static void Remove(IWpfTextView view)
    {
        ClearActiveSession(view, "Remove");
    }

    public static void ClearActiveSession(IWpfTextView view, string reason)
    {
        RemoveViewSessionProperty(view);
        if (Sessions.TryRemove(view, out var session))
        {
            _logger?.Info($"GhostText active session view cleared. ViewId={GetViewId(view)}, RequestId={session.RequestId}, Source={session.Source}, Reason={reason}");
            _logger?.Info($"GhostText exclusive mode exited. ViewId={GetViewId(view)}, Reason={reason}");
        }
    }

    public static void ClearActiveSession(IWpfTextView view, long requestId, string source, string reason)
    {
        RemoveViewSessionProperty(view);
        if (Sessions.TryRemove(view, out _))
        {
            _logger?.Info($"GhostText active session view cleared. ViewId={GetViewId(view)}, RequestId={requestId}, Source={source}, Reason={reason}");
            _logger?.Info($"GhostText exclusive mode exited. ViewId={GetViewId(view)}, Reason={reason}");
        }
    }

    public static void RegisterActiveSession(IWpfTextView view, GhostTextSession session)
    {
        Sessions[view] = session;
        RemoveViewSessionProperty(view);
        view.Properties.AddProperty(ActiveSessionKey, session);
        _logger?.Info($"GhostText active session view registered. RequestId={session.RequestId}, Source={session.Source}, ViewId={GetViewId(view)}");
        _logger?.Info($"GhostText exclusive mode entered. ViewId={GetViewId(view)}, RequestId={session.RequestId}, Source={session.Source}");
    }

    public static void DismissAll(string reason)
    {
        if (_commitHandler == null)
        {
            return;
        }

        foreach (var sessionEntry in Sessions.ToArray())
        {
            _commitHandler.TryDismiss(sessionEntry.Key, reason);
        }
    }

    public static void DismissAllIfCaretOrSelectionChanged(string reason)
    {
        if (_commitHandler == null)
        {
            return;
        }

        foreach (var sessionEntry in Sessions.ToArray())
        {
            if (sessionEntry.Value.HasCaretOrSelectionChanged(sessionEntry.Key))
            {
                _commitHandler.TryDismiss(sessionEntry.Key, reason);
            }
        }
    }

    public static bool DismissIfCaretOrSelectionChanged(IWpfTextView view, string reason)
    {
        return _commitHandler != null
            && TryGetExisting(view, out var session)
            && session.HasCaretOrSelectionChanged(view)
            && _commitHandler.TryDismiss(view, reason);
    }

    public static bool TryShow(IWpfTextView view, string text, long requestId, string source)
    {
        if (_coordinator == null || _detector == null)
        {
            return false;
        }

        var filePath = view.TextBuffer.GetFileName();
        if (!_detector.IsSupported(filePath))
        {
            return false;
        }

        var session = GetOrCreate(view);
        session.Show(view, text, requestId, source, _logger);
        _coordinator.SetState(CompletionState.ShowingGhostText);
        RegisterActiveSession(view, session);
        GhostTextInputArbiter.SuppressIntelliSenseIfGhostTextActive(view, "GhostTextShown");
        _commitHandler?.LogSessionActivated(session);
        return true;
    }

    public static bool TryShow(IWpfTextView view, NormalizedCompletion completion, long requestId, string source)
    {
        if (_coordinator == null || _detector == null || completion == null)
        {
            return false;
        }

        if (!IsSupportedView(view))
        {
            return false;
        }

        var session = GetOrCreate(view);
        session.Show(view, completion, requestId, source, _logger);
        _coordinator.SetState(CompletionState.ShowingGhostText);
        RegisterActiveSession(view, session);
        GhostTextInputArbiter.SuppressIntelliSenseIfGhostTextActive(view, "GhostTextShown");
        _commitHandler?.LogSessionActivated(session);
        return true;
    }

    public static bool AcceptActiveGhostTextFromTab(
        IWpfTextView view,
        string source,
        ICompletionBroker legacyCompletionBroker,
        IAsyncCompletionBroker asyncCompletionBroker)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return GhostTextInputArbiter.TryAcceptGhostTextFromTab(view, source, legacyCompletionBroker, asyncCompletionBroker);
    }

    public static bool Accept(IWpfTextView view)
    {
        return _commitHandler != null && _commitHandler.TryAccept(view);
    }

    public static bool Dismiss(IWpfTextView view, string reason)
    {
        return _commitHandler != null && _commitHandler.TryDismiss(view, reason);
    }

    public static void ScheduleAutoCompletion(IWpfTextView view)
    {
        _coordinator?.ScheduleAutoCompletion(view);
    }

    public static void CancelAutoCompletion(string reason)
    {
        _coordinator?.CancelAutoCompletion(reason);
    }

    public static int GetViewId(IWpfTextView view)
    {
        return view?.GetHashCode() ?? 0;
    }

    private static void RemoveViewSessionProperty(IWpfTextView view)
    {
        if (view != null && view.Properties.ContainsProperty(ActiveSessionKey))
        {
            view.Properties.RemoveProperty(ActiveSessionKey);
        }
    }

    private static bool IsAsyncCompletionActive(IWpfTextView view, IAsyncCompletionBroker asyncCompletionBroker)
    {
        try
        {
            return asyncCompletionBroker != null && asyncCompletionBroker.IsCompletionActive(view);
        }
        catch (Exception ex)
        {
            _logger?.Warning($"AsyncCompletion active check failed. ViewId={GetViewId(view)}, Error={ex.GetType().Name}");
            return false;
        }
    }

    private static bool IsLegacyCompletionActive(IWpfTextView view, ICompletionBroker legacyCompletionBroker)
    {
        try
        {
            return legacyCompletionBroker != null && legacyCompletionBroker.IsCompletionActive(view);
        }
        catch (Exception ex)
        {
            _logger?.Warning($"Legacy completion active check failed. ViewId={GetViewId(view)}, Error={ex.GetType().Name}");
            return false;
        }
    }

    private static void DismissAsyncCompletionForGhostTextAccept(IWpfTextView view, IAsyncCompletionBroker asyncCompletionBroker)
    {
        try
        {
            var session = asyncCompletionBroker?.GetSession(view);
            if (session == null)
            {
                return;
            }

            session.Dismiss();
            _logger?.Info($"AsyncCompletion session dismissed for GhostText accept. ViewId={GetViewId(view)}");
        }
        catch (Exception ex)
        {
            _logger?.Warning($"AsyncCompletion session dismiss failed for GhostText accept. ViewId={GetViewId(view)}, Error={ex.GetType().Name}");
        }
    }

    private static void DismissLegacyCompletionForGhostTextAccept(IWpfTextView view, ICompletionBroker legacyCompletionBroker)
    {
        try
        {
            if (legacyCompletionBroker == null || !legacyCompletionBroker.IsCompletionActive(view))
            {
                return;
            }

            foreach (var session in legacyCompletionBroker.GetSessions(view))
            {
                session.Dismiss();
            }

            _logger?.Info($"Legacy completion session dismissed for GhostText accept. ViewId={GetViewId(view)}");
        }
        catch (Exception ex)
        {
            _logger?.Warning($"Legacy completion session dismiss failed for GhostText accept. ViewId={GetViewId(view)}, Error={ex.GetType().Name}");
        }
    }
}
