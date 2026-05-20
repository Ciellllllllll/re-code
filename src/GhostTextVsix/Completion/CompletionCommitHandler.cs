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
        var session = GhostTextBroker.GetOrCreate(view);
        if (!session.HasSuggestion)
        {
            return false;
        }

        var requestId = session.RequestId;
        var source = session.Source;
        var insertedEnd = session.Accept(view);
        _coordinator.SetState(CompletionState.Accepted);
        _logger.Info($"Caret moved to inserted completion end. RequestId={requestId}, Source={source}, Position={insertedEnd}, State={_coordinator.State}");
        _coordinator.SetState(CompletionState.Idle);
        _logger.Info($"GhostText accepted. RequestId={requestId}, Source={source}, State={_coordinator.State}");
        return true;
    }

    public bool TryDismiss(IWpfTextView view, string reason)
    {
        var session = GhostTextBroker.GetOrCreate(view);
        if (!session.HasSuggestion)
        {
            return false;
        }

        var requestId = session.RequestId;
        var source = session.Source;
        session.Dismiss();
        _coordinator.SetState(CompletionState.Dismissed);
        _coordinator.SetState(CompletionState.Idle);
        _logger.Info($"GhostText dismissed. RequestId={requestId}, Source={source}, Reason={reason}, State={_coordinator.State}");
        return true;
    }

    public void LogSessionActivated(GhostTextSession session)
    {
        _logger.Info($"GhostText session activated. RequestId={session.RequestId}, Source={session.Source}, State={_coordinator.State}");
    }
}
