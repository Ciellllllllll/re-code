using System;

namespace GhostTextVsix.Completion.Providers;

internal sealed class CompletionProviderRequest
{
    public string Language { get; set; }

    public string FilePath { get; set; }

    public string Prefix { get; set; }

    public string Suffix { get; set; }

    public string CurrentLinePrefix { get; set; }

    public string CurrentLineSuffix { get; set; }

    public string SystemPrompt { get; set; }

    public string UserPrompt { get; set; }

    public string ModelName { get; set; }

    public string BaseUrl { get; set; }

    public int MaxTokens { get; set; }

    public double Temperature { get; set; }

    public TimeSpan Timeout { get; set; }

    public long RequestId { get; set; }

    public string Source { get; set; }

    public CompletionMode CompletionMode { get; set; }

    public CompletionProviderConfig Config { get; set; }
}
