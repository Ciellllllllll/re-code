using System;
using System.Collections.Generic;

namespace GhostTextVsix.Completion.Providers;

internal static class ProviderRegistry
{
    private static readonly IReadOnlyDictionary<CompletionProviderType, ProviderDefinition> Definitions =
        new Dictionary<CompletionProviderType, ProviderDefinition>
        {
            [CompletionProviderType.NotConfigured] = new ProviderDefinition
            {
                ProviderType = CompletionProviderType.NotConfigured,
                DisplayName = "NotConfigured",
                DefaultModelName = string.Empty,
                RequestUrl = string.Empty,
                EndpointKind = ProviderEndpointKind.None,
                SupportsChatCompletions = false,
                SupportsFimCompletions = false,
                RequiresApiKey = false,
                IsLocal = false,
                IsImplemented = false
            },
            [CompletionProviderType.DeepSeek] = new ProviderDefinition
            {
                ProviderType = CompletionProviderType.DeepSeek,
                DisplayName = "DeepSeek",
                DefaultModelName = "deepseek-v4-flash",
                RequestUrl = "https://api.deepseek.com/chat/completions",
                EndpointKind = ProviderEndpointKind.ChatCompletions,
                SupportsChatCompletions = true,
                SupportsFimCompletions = false,
                RequiresApiKey = true,
                IsLocal = false,
                IsImplemented = true
            },
            [CompletionProviderType.OpenRouter] = new ProviderDefinition
            {
                ProviderType = CompletionProviderType.OpenRouter,
                DisplayName = "OpenRouter",
                DefaultModelName = string.Empty,
                RequestUrl = "https://openrouter.ai/api/v1/chat/completions",
                EndpointKind = ProviderEndpointKind.ChatCompletions,
                SupportsChatCompletions = true,
                SupportsFimCompletions = false,
                RequiresApiKey = true,
                IsLocal = false,
                IsImplemented = true
            },
            [CompletionProviderType.Codestral] = Unsupported(CompletionProviderType.Codestral, "Codestral"),
            [CompletionProviderType.Gemini] = Unsupported(CompletionProviderType.Gemini, "Gemini"),
            [CompletionProviderType.Groq] = Unsupported(CompletionProviderType.Groq, "Groq"),
            [CompletionProviderType.OpenAICompatible] = Unsupported(CompletionProviderType.OpenAICompatible, "OpenAICompatible"),
            [CompletionProviderType.LocalOpenAICompatible] = Unsupported(CompletionProviderType.LocalOpenAICompatible, "LocalOpenAICompatible", isLocal: true, requiresApiKey: false)
        };

    public static ProviderDefinition Get(CompletionProviderType providerType)
    {
        return Definitions.TryGetValue(providerType, out var definition)
            ? definition
            : Unsupported(providerType, providerType.ToString());
    }

    private static ProviderDefinition Unsupported(
        CompletionProviderType providerType,
        string displayName,
        bool isLocal = false,
        bool requiresApiKey = true)
    {
        return new ProviderDefinition
        {
            ProviderType = providerType,
            DisplayName = displayName,
            DefaultModelName = string.Empty,
            RequestUrl = string.Empty,
            EndpointKind = ProviderEndpointKind.None,
            SupportsChatCompletions = false,
            SupportsFimCompletions = false,
            RequiresApiKey = requiresApiKey,
            IsLocal = isLocal,
            IsImplemented = false
        };
    }
}
