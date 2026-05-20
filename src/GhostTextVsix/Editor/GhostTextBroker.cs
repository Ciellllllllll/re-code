using System;
using System.Collections.Concurrent;
using System.Linq;
using GhostTextVsix.Completion;
using GhostTextVsix.Diagnostics;
using Microsoft.VisualStudio.Text.Editor;

namespace GhostTextVsix.Editor;

internal static class GhostTextBroker
{
    private static readonly ConcurrentDictionary<IWpfTextView, GhostTextSession> Sessions = new();
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

    public static bool IsActive(IWpfTextView view)
    {
        return TryGetExisting(view, out var session) && session.HasSuggestion;
    }

    public static void LogInfo(string message)
    {
        _logger?.Info(message);
    }

    public static void Remove(IWpfTextView view)
    {
        Sessions.TryRemove(view, out _);
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
        _commitHandler?.LogSessionActivated(session);
        return true;
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
}
