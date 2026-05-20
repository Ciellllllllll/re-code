namespace GhostTextVsix.Completion.Providers;

internal sealed class ProviderDefinition
{
    public CompletionProviderType ProviderType { get; set; }

    public string DisplayName { get; set; }

    public string DefaultModelName { get; set; }

    public string RequestUrl { get; set; }

    public ProviderEndpointKind EndpointKind { get; set; }

    public bool SupportsChatCompletions { get; set; }

    public bool SupportsFimCompletions { get; set; }

    public bool RequiresApiKey { get; set; }

    public bool IsLocal { get; set; }

    public bool IsImplemented { get; set; }
}
