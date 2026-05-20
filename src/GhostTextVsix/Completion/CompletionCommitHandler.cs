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

        session.Accept(view);
        _coordinator.SetState(CompletionState.Accepted);
        _logger.Info("GhostText accepted.");
        return true;
    }

    public bool TryDismiss(IWpfTextView view, string reason)
    {
        var session = GhostTextBroker.GetOrCreate(view);
        if (!session.HasSuggestion)
        {
            return false;
        }

        session.Dismiss();
        _coordinator.SetState(CompletionState.Dismissed);
        _logger.Info($"GhostText dismissed. Reason={reason}");
        return true;
    }
}
