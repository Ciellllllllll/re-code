namespace GhostTextVsix.Completion.Providers;

internal sealed class ChatCompletionParseResult
{
    public string Text { get; set; }

    public bool HasChoices { get; set; }

    public int ChoicesCount { get; set; }

    public bool HasMessage { get; set; }

    public bool HasContent { get; set; }

    public int ContentLength { get; set; }

    public bool HasReasoningContent { get; set; }

    public int ReasoningContentLength { get; set; }

    public string FinishReason { get; set; } = string.Empty;

    public bool HasErrorObject { get; set; }

    public string ErrorType { get; set; } = string.Empty;

    public string ErrorCode { get; set; } = string.Empty;

    public string ErrorMessageSummary { get; set; } = string.Empty;
}
