using System;
using GhostTextVsix.Completion;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;

namespace GhostTextVsix.Editor;

internal static class GhostTextAcceptanceService
{
    public static bool TryAcceptFromTab(
        IWpfTextView view,
        string inputSource,
        ICompletionBroker legacyCompletionBroker,
        IAsyncCompletionBroker asyncCompletionBroker)
    {
        try
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (!GhostTextBroker.IsSupportedView(view))
            {
                return false;
            }

            if (!GhostTextBroker.TryGetActiveSession(view, out var session))
            {
                GhostTextBroker.LogInfo($"GhostText accept skipped. Reason=NoActiveSession, InputSource={inputSource}, ViewId={GhostTextBroker.GetViewId(view)}");
                return false;
            }

            return TryAcceptSession(view, session, inputSource, legacyCompletionBroker, asyncCompletionBroker);
        }
        catch (Exception ex)
        {
            GhostTextBroker.LogError($"GhostText accept failed. InputSource={inputSource}, ViewId={GhostTextBroker.GetViewId(view)}, Error={ex.GetType().Name}");
            return false;
        }
    }

    public static bool TryAcceptSession(
        IWpfTextView view,
        GhostTextSession session,
        string inputSource,
        ICompletionBroker legacyCompletionBroker,
        IAsyncCompletionBroker asyncCompletionBroker)
    {
        if (session == null || session.IsAccepted || session.IsDismissed)
        {
            GhostTextBroker.LogInfo($"GhostText accept skipped. Reason=SessionInactive, InputSource={inputSource}, ViewId={GhostTextBroker.GetViewId(view)}");
            return false;
        }

        if (!session.TryBeginAccept())
        {
            GhostTextBroker.LogInfo($"GhostText accept skipped. Reason=AlreadyAccepting, InputSource={inputSource}, ViewId={GhostTextBroker.GetViewId(view)}");
            return false;
        }

        try
        {
            var completion = session.Completion;
            var plan = completion?.CommitPlan;
            if (plan == null || string.IsNullOrEmpty(plan.CommitText))
            {
                GhostTextBroker.Dismiss(view, "InvalidCommitPlan");
                GhostTextBroker.LogInfo($"GhostText accept skipped. Reason=InvalidCommitPlan, InputSource={inputSource}, ViewId={GhostTextBroker.GetViewId(view)}");
                return false;
            }

            if (view.TextSnapshot.Version.VersionNumber != plan.ExpectedSnapshotVersion)
            {
                GhostTextBroker.LogInfo($"GhostText accept snapshot version changed. InputSource={inputSource}, ViewId={GhostTextBroker.GetViewId(view)}, SnapshotVersion={view.TextSnapshot.Version.VersionNumber}, ExpectedSnapshotVersion={plan.ExpectedSnapshotVersion}");
            }

            GhostTextInputArbiter.TryDismissAsyncCompletion(view, asyncCompletionBroker, $"Accept:{inputSource}");
            GhostTextInputArbiter.TryDismissLegacyCompletion(view, legacyCompletionBroker, $"Accept:{inputSource}");
            GhostTextBroker.LogInfo($"GhostText accept started. InputSource={inputSource}, ViewId={GhostTextBroker.GetViewId(view)}, InsertMode={completion.InsertMode}, CommitSpanStart={completion.CommitSpanStart}, CommitSpanLength={completion.CommitSpanLength}");
            return GhostTextBroker.Accept(view);
        }
        finally
        {
            session.EndAccept();
        }
    }
}
