namespace GhostTextVsix.Completion;

internal enum CompletionState
{
    Idle,
    WaitingForDebounce,
    RequestingCompletion,
    ShowingGhostText,
    Accepted,
    Dismissed,
    Error
}
