namespace GhostTextVsix.Completion.Providers;

internal sealed class CompletionProviderResponse
{
    public string Text { get; set; }

    public string ProviderName { get; set; }

    public string ModelName { get; set; }

    public string Source { get; set; }

    public long RequestId { get; set; }

    public double LatencyMs { get; set; }

    public bool IsSuccess { get; set; }

    public string ErrorMessage { get; set; }
}
