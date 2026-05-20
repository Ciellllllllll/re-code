using System;

namespace GhostTextVsix.Completion.Providers;

internal sealed class CompletionProviderConfig
{
    public CompletionProviderType ProviderType { get; set; }

    public string ProviderName { get; set; }

    public string BaseUrl { get; set; }

    public string ApiKey { get; set; }

    public string ModelName { get; set; }

    public int MaxTokens { get; set; }

    public double Temperature { get; set; }

    public TimeSpan Timeout { get; set; }

    public bool IsLocal { get; set; }

    public bool RequiresApiKey { get; set; }

    public bool IsImplemented { get; set; }

    public bool IsConfigured => ProviderType != CompletionProviderType.NotConfigured;

    public bool SupportsChatCompletions { get; set; } = true;

    public bool SupportsFimCompletions { get; set; }

    public bool SupportsStreaming { get; set; }
}
