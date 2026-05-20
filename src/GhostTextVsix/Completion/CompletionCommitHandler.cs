using GhostTextVsix.Diagnostics;
using GhostTextVsix.Editor;
using Microsoft.VisualStudio.Text.Editor;

namespace GhostTextVsix.Completion;

internal sealed class CompletionCommitHandler
{
    private readonly CompletionCoordinator _coordinator;
    private readonly DeepSeekOutputLogger _logger;

    public CompletionCommitHandler(CompletionCoordinator coordinator, DeepSeekOutputLogger logger)
    {
        _coordinator = coordinator;
        _logger = logger;
    }

    public bool TryAccept(IWpfTextView view)
    {
        if (!GhostTextBroker.TryGetActiveSession(view, out var session))
        {
            return false;
        }

        var requestId = session.RequestId;
        var source = session.Source;
        var insertedEnd = session.AcceptPlan(view);
        if (insertedEnd < 0)
        {
            _logger.Warning($"GhostText accept skipped. Reason=CommitPlanFailed, RequestId={requestId}, Source={source}");
            GhostTextBroker.ClearActiveSession(view, requestId, source, "CommitPlanFailed");
            _coordinator.SetState(CompletionState.Idle);
            return false;
        }

        _coordinator.SetState(CompletionState.Accepted);
        _logger.Info($"Caret moved to inserted completion end. RequestId={requestId}, Source={source}, Position={insertedEnd}, State={_coordinator.State}");
        _coordinator.SetState(CompletionState.Idle);
        _logger.Info($"GhostText accepted. RequestId={requestId}, Source={source}, State={_coordinator.State}");
        GhostTextBroker.ClearActiveSession(view, requestId, source, "Accepted");
        return true;
    }

    public bool TryDismiss(IWpfTextView view, string reason)
    {
        if (!GhostTextBroker.TryGetActiveSession(view, out var session))
        {
            return false;
        }

        var requestId = session.RequestId;
        var source = session.Source;
        session.Dismiss();
        _coordinator.SetState(CompletionState.Dismissed);
        _coordinator.SetState(CompletionState.Idle);
        _logger.Info($"GhostText dismissed. RequestId={requestId}, Source={source}, Reason={reason}, State={_coordinator.State}");
        GhostTextBroker.ClearActiveSession(view, requestId, source, reason);
        return true;
    }

    public void LogSessionActivated(GhostTextSession session)
    {
        _logger.Info($"GhostText session activated. RequestId={session.RequestId}, Source={session.Source}, State={_coordinator.State}");
    }
}
