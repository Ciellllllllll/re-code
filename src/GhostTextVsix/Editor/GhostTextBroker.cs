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

    public static void Initialize(CompletionCoordinator coordinator, CppDocumentDetector detector, CompletionCommitHandler commitHandler)
    {
        _coordinator = coordinator;
        _detector = detector;
        _commitHandler = commitHandler;
    }

    public static GhostTextSession GetOrCreate(IWpfTextView view)
    {
        return Sessions.GetOrAdd(view, static _ => new GhostTextSession());
    }

    public static bool TryGetExisting(IWpfTextView view, out GhostTextSession session)
    {
        return Sessions.TryGetValue(view, out session);
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

    public static bool TryShow(IWpfTextView view, string text)
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
        session.Show(view, text);
        _coordinator.SetState(CompletionState.ShowingGhostText);
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
