using System;
using System.Collections.Generic;
using System.Linq;

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
                ModelNames = new[] { "deepseek-v4-flash" },
                AllowCustomModelName = false,
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
                DefaultModelName = "openrouter/auto",
                ModelNames = new[] { "openrouter/auto" },
                AllowCustomModelName = true,
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
            [CompletionProviderType.OpenAICompatible] = Unsupported(
                CompletionProviderType.OpenAICompatible,
                "OpenAICompatible",
                defaultModelName: "gpt-4o-mini",
                modelNames: new[] { "gpt-4o-mini" },
                allowCustomModelName: true),
            [CompletionProviderType.LocalOpenAICompatible] = Unsupported(
                CompletionProviderType.LocalOpenAICompatible,
                "LocalOpenAICompatible",
                isLocal: true,
                requiresApiKey: false,
                defaultModelName: "local-model",
                modelNames: new[] { "local-model" },
                allowCustomModelName: true)
        };

    public static ProviderDefinition Get(CompletionProviderType providerType)
    {
        return Definitions.TryGetValue(providerType, out var definition)
            ? definition
            : Unsupported(providerType, providerType.ToString());
    }

    public static IReadOnlyList<string> GetModelCandidates(CompletionProviderType providerType)
    {
        var definition = Get(providerType);
        if (definition.ModelNames?.Length > 0)
        {
            return definition.ModelNames;
        }

        return string.IsNullOrWhiteSpace(definition.DefaultModelName)
            ? Array.Empty<string>()
            : new[] { definition.DefaultModelName };
    }

    public static string ResolveModelName(CompletionProviderType providerType, string configuredModel)
    {
        var definition = Get(providerType);
        if (providerType == CompletionProviderType.NotConfigured)
        {
            return string.Empty;
        }

        var modelName = (configuredModel ?? string.Empty).Trim();
        var candidates = GetModelCandidates(providerType)
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .ToArray();

        if (!string.IsNullOrWhiteSpace(modelName))
        {
            if (definition.AllowCustomModelName ||
                candidates.Any(candidate => string.Equals(candidate, modelName, StringComparison.OrdinalIgnoreCase)))
            {
                return modelName;
            }
        }

        if (candidates.Length > 0)
        {
            return candidates[0];
        }

        return definition.AllowCustomModelName ? modelName : string.Empty;
    }

    public static string ResolveModelNameForProviderChange(CompletionProviderType providerType, string configuredModel)
    {
        if (providerType == CompletionProviderType.NotConfigured)
        {
            return string.Empty;
        }

        var modelName = (configuredModel ?? string.Empty).Trim();
        var candidates = GetModelCandidates(providerType)
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .ToArray();

        if (!string.IsNullOrWhiteSpace(modelName) &&
            candidates.Any(candidate => string.Equals(candidate, modelName, StringComparison.OrdinalIgnoreCase)))
        {
            return modelName;
        }

        if (candidates.Length > 0)
        {
            return candidates[0];
        }

        return Get(providerType).AllowCustomModelName ? modelName : string.Empty;
    }

    private static ProviderDefinition Unsupported(
        CompletionProviderType providerType,
        string displayName,
        bool isLocal = false,
        bool requiresApiKey = true,
        string defaultModelName = "",
        string[] modelNames = null,
        bool allowCustomModelName = false)
    {
        return new ProviderDefinition
        {
            ProviderType = providerType,
            DisplayName = displayName,
            DefaultModelName = defaultModelName ?? string.Empty,
            ModelNames = modelNames ?? Array.Empty<string>(),
            AllowCustomModelName = allowCustomModelName,
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
