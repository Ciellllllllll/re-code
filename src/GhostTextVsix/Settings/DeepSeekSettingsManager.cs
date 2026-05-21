using System;
using GhostTextVsix.Completion.Providers;

namespace GhostTextVsix.Settings;

internal sealed class DeepSeekSettingsManager
{
    private readonly DeepSeekCompletionPackage _package;
    private bool _legacyApiKeyMigrationAttempted;

    public DeepSeekSettingsManager(DeepSeekCompletionPackage package)
    {
        _package = package;
    }

    public DeepSeekOptionsPage GetOptions()
    {
        var options = (DeepSeekOptionsPage)_package.GetDialogPage(typeof(DeepSeekOptionsPage));
        MigrateLegacyApiKeys(options);
        return options;
    }

    public TimeSpan GetTimeout() => TimeSpan.FromSeconds(Math.Max(5, GetOptions().TimeoutSeconds));

    public TimeSpan GetAutoTimeout() => TimeSpan.FromMilliseconds(Math.Max(500, GetOptions().AutoTimeoutMilliseconds));

    public bool IsAutoCompletionEnabled() => GetOptions().EnableAutoCompletion;

    public TimeSpan GetDebounceTime() => TimeSpan.FromMilliseconds(Math.Max(100, GetOptions().DebounceMilliseconds));

    public TimeSpan GetCacheTtl() => TimeSpan.FromSeconds(Math.Max(1, GetOptions().CacheTtlSeconds));

    public int GetAutoMaxTokens() => Math.Max(16, Math.Min(512, GetOptions().AutoMaxTokens));

    public double GetAutoTemperature()
    {
        var temperature = GetOptions().AutoTemperature;
        return ClampTemperature(temperature);
    }

    public CompletionProviderConfig GetAutoProviderConfig()
    {
        return GetProviderConfig();
    }

    public CompletionProviderConfig GetProviderConfig()
    {
        var options = GetOptions();
        var providerType = options.AutoCompletionProvider;
        var modelName = ProviderRegistry.ResolveModelName(providerType, options.AutoCompletionModel);
        return BuildProviderConfig(
            providerType,
            modelName,
            options.AutoCompletionApiKey,
            GetAutoMaxTokens(),
            GetAutoTemperature(),
            GetAutoTimeout());
    }

    public void SetAutoCompletionEnabled(bool enabled)
    {
        var options = GetOptions();
        options.EnableAutoCompletion = enabled;
        options.SaveSettingsToStorage();
    }

    private CompletionProviderConfig BuildProviderConfig(
        CompletionProviderType providerType,
        string modelName,
        string apiKey,
        int maxTokens,
        double temperature,
        TimeSpan timeout)
    {
        var definition = ProviderRegistry.Get(providerType);
        return new CompletionProviderConfig
        {
            ProviderType = providerType,
            ProviderName = definition.DisplayName,
            BaseUrl = definition.RequestUrl,
            ApiKey = ResolveApiKey(providerType, apiKey),
            ModelName = ProviderRegistry.ResolveModelName(providerType, modelName),
            MaxTokens = maxTokens,
            Temperature = temperature,
            Timeout = timeout,
            IsLocal = definition.IsLocal,
            RequiresApiKey = definition.RequiresApiKey,
            IsImplemented = definition.IsImplemented,
            SupportsChatCompletions = definition.SupportsChatCompletions,
            SupportsFimCompletions = definition.SupportsFimCompletions,
            SupportsStreaming = false
        };
    }

    private string ResolveApiKey(CompletionProviderType providerType, string configuredApiKey)
    {
        if (!string.IsNullOrWhiteSpace(configuredApiKey))
        {
            return configuredApiKey.Trim();
        }

        switch (providerType)
        {
            case CompletionProviderType.DeepSeek:
                return (Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY") ?? string.Empty).Trim();
            case CompletionProviderType.OpenRouter:
                return (Environment.GetEnvironmentVariable("OPENROUTER_API_KEY") ?? string.Empty).Trim();
            case CompletionProviderType.OpenAICompatible:
                return (Environment.GetEnvironmentVariable("OPENAI_COMPATIBLE_API_KEY") ?? string.Empty).Trim();
            case CompletionProviderType.LocalOpenAICompatible:
            default:
                return string.Empty;
        }
    }

    private void MigrateLegacyApiKeys(DeepSeekOptionsPage options)
    {
        if (_legacyApiKeyMigrationAttempted)
        {
            return;
        }

        _legacyApiKeyMigrationAttempted = true;
        var legacyApiKey = FirstConfiguredKey(
            options.ApiKey,
            options.OpenRouterApiKey,
            options.OpenAICompatibleApiKey,
            options.ManualCompletionApiKey);
        if (string.IsNullOrWhiteSpace(legacyApiKey))
        {
            return;
        }

        var changed = false;
        if (string.IsNullOrWhiteSpace(options.AutoCompletionApiKey))
        {
            options.AutoCompletionApiKey = legacyApiKey;
            changed = true;
        }

        if (changed)
        {
            options.SaveSettingsToStorage();
        }
    }

    private static string FirstConfiguredKey(params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                return key.Trim();
            }
        }

        return string.Empty;
    }

    private static double ClampTemperature(double temperature)
    {
        if (temperature < 0)
        {
            return 0;
        }

        return temperature > 2 ? 2 : temperature;
    }
}
