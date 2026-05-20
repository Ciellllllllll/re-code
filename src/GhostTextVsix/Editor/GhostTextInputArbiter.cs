using System;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Text.Editor;

namespace GhostTextVsix.Editor;

internal static class GhostTextInputArbiter
{
    private static ICompletionBroker _defaultLegacyCompletionBroker;
    private static IAsyncCompletionBroker _defaultAsyncCompletionBroker;

    public static void RegisterCompletionBrokers(
        ICompletionBroker legacyCompletionBroker,
        IAsyncCompletionBroker asyncCompletionBroker)
    {
        _defaultLegacyCompletionBroker ??= legacyCompletionBroker;
        _defaultAsyncCompletionBroker ??= asyncCompletionBroker;
    }

    public static bool IsGhostTextActive(IWpfTextView view)
    {
        return GhostTextBroker.HasActiveSession(view);
    }

    public static bool ShouldHandleTab(IWpfTextView view)
    {
        return IsGhostTextActive(view);
    }

    public static bool TryAcceptGhostTextFromTab(
        IWpfTextView view,
        string inputSource,
        ICompletionBroker legacyCompletionBroker,
        IAsyncCompletionBroker asyncCompletionBroker)
    {
        SuppressIntelliSenseIfGhostTextActive(view, $"BeforeTabAccept:{inputSource}", legacyCompletionBroker, asyncCompletionBroker);
        var accepted = GhostTextAcceptanceService.TryAcceptFromTab(view, inputSource, legacyCompletionBroker, asyncCompletionBroker);
        SuppressIntelliSenseIfGhostTextActive(view, $"AfterTabAccept:{inputSource}", legacyCompletionBroker, asyncCompletionBroker);
        return accepted;
    }

    public static void SuppressIntelliSenseIfGhostTextActive(
        IWpfTextView view,
        string reason,
        ICompletionBroker legacyCompletionBroker,
        IAsyncCompletionBroker asyncCompletionBroker)
    {
        if (!IsGhostTextActive(view))
        {
            return;
        }

        TryDismissAsyncCompletion(view, asyncCompletionBroker, reason);
        TryDismissLegacyCompletion(view, legacyCompletionBroker, reason);
    }

    public static void SuppressIntelliSenseIfGhostTextActive(IWpfTextView view, string reason)
    {
        SuppressIntelliSenseIfGhostTextActive(view, reason, _defaultLegacyCompletionBroker, _defaultAsyncCompletionBroker);
    }

    public static bool TryDismissAsyncCompletion(IWpfTextView view, IAsyncCompletionBroker asyncCompletionBroker, string reason)
    {
        try
        {
            var session = asyncCompletionBroker?.GetSession(view);
            if (session == null || session.IsDismissed)
            {
                return false;
            }

            GhostTextBroker.LogInfo($"AsyncCompletion detected. ViewId={GhostTextBroker.GetViewId(view)}, Reason={reason}");
            session.Dismiss();
            GhostTextBroker.LogInfo($"AsyncCompletion dismissed. ViewId={GhostTextBroker.GetViewId(view)}, Reason={reason}");
            return true;
        }
        catch (Exception ex)
        {
            GhostTextBroker.LogWarning($"AsyncCompletion dismiss failed. ViewId={GhostTextBroker.GetViewId(view)}, Reason={reason}, Error={ex.GetType().Name}");
            return false;
        }
    }

    public static bool TryDismissLegacyCompletion(IWpfTextView view, ICompletionBroker legacyCompletionBroker, string reason)
    {
        try
        {
            if (legacyCompletionBroker == null || !legacyCompletionBroker.IsCompletionActive(view))
            {
                return false;
            }

            GhostTextBroker.LogInfo($"LegacyCompletion detected. ViewId={GhostTextBroker.GetViewId(view)}, Reason={reason}");
            foreach (var session in legacyCompletionBroker.GetSessions(view))
            {
                session.Dismiss();
            }

            GhostTextBroker.LogInfo($"LegacyCompletion dismissed. ViewId={GhostTextBroker.GetViewId(view)}, Reason={reason}");
            return true;
        }
        catch (Exception ex)
        {
            GhostTextBroker.LogWarning($"LegacyCompletion dismiss failed. ViewId={GhostTextBroker.GetViewId(view)}, Reason={reason}, Error={ex.GetType().Name}");
            return false;
        }
    }
}
