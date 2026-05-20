using System;
using GhostTextVsix.Completion.Providers;

namespace GhostTextVsix.Settings;

internal sealed class DeepSeekSettingsManager
{
    private readonly DeepSeekCompletionPackage _package;

    public DeepSeekSettingsManager(DeepSeekCompletionPackage package)
    {
        _package = package;
    }

    public DeepSeekOptionsPage GetOptions()
    {
        return (DeepSeekOptionsPage)_package.GetDialogPage(typeof(DeepSeekOptionsPage));
    }

    public string GetApiKey()
    {
        var options = GetOptions();
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return options.ApiKey.Trim();
        }

        return (Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY") ?? string.Empty).Trim();
    }

    public string GetEndpoint() => GetOptions().Endpoint?.Trim() ?? string.Empty;

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

    public double GetManualTemperature() => ClampTemperature(GetOptions().ManualTemperature);

    public int GetManualMaxTokens() => Math.Max(0, Math.Min(4096, GetOptions().ManualMaxTokens));

    public CompletionProviderConfig GetAutoProviderConfig()
    {
        var options = GetOptions();
        var providerType = options.AutoCompletionProvider;
        return BuildProviderConfig(
            providerType,
            options.AutoCompletionModel,
            options.AutoCompletionBaseUrl,
            options.AutoCompletionApiKey,
            GetAutoMaxTokens(),
            GetAutoTemperature(),
            GetAutoTimeout());
    }

    public CompletionProviderConfig GetManualProviderConfig()
    {
        var options = GetOptions();
        var providerType = options.ManualCompletionProvider;
        return BuildProviderConfig(
            providerType,
            options.ManualCompletionModel,
            options.ManualCompletionBaseUrl,
            options.ManualCompletionApiKey,
            GetManualMaxTokens(),
            GetManualTemperature(),
            GetTimeout());
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
        string baseUrl,
        string apiKey,
        int maxTokens,
        double temperature,
        TimeSpan timeout)
    {
        var providerName = GetProviderName(providerType);
        return new CompletionProviderConfig
        {
            ProviderType = providerType,
            ProviderName = providerName,
            BaseUrl = ResolveBaseUrl(providerType, baseUrl),
            ApiKey = ResolveApiKey(providerType, apiKey),
            ModelName = ResolveModelName(providerType, modelName),
            MaxTokens = maxTokens,
            Temperature = temperature,
            Timeout = timeout,
            IsLocal = providerType == CompletionProviderType.LocalOpenAICompatible,
            SupportsChatCompletions = true,
            SupportsFimCompletions = false,
            SupportsStreaming = false
        };
    }

    private string ResolveBaseUrl(CompletionProviderType providerType, string configuredBaseUrl)
    {
        if (!string.IsNullOrWhiteSpace(configuredBaseUrl))
        {
            return configuredBaseUrl.Trim();
        }

        switch (providerType)
        {
            case CompletionProviderType.DeepSeek:
                return GetEndpoint();
            case CompletionProviderType.OpenRouter:
                return "https://openrouter.ai/api/v1/chat/completions";
            case CompletionProviderType.LocalOpenAICompatible:
                return GetOptions().LocalBaseUrl?.Trim() ?? string.Empty;
            case CompletionProviderType.OpenAICompatible:
            default:
                return string.Empty;
        }
    }

    private string ResolveApiKey(CompletionProviderType providerType, string configuredApiKey)
    {
        if (!string.IsNullOrWhiteSpace(configuredApiKey))
        {
            return configuredApiKey.Trim();
        }

        var options = GetOptions();
        switch (providerType)
        {
            case CompletionProviderType.DeepSeek:
                return GetApiKey();
            case CompletionProviderType.OpenRouter:
                if (!string.IsNullOrWhiteSpace(options.OpenRouterApiKey))
                {
                    return options.OpenRouterApiKey.Trim();
                }

                return (Environment.GetEnvironmentVariable("OPENROUTER_API_KEY") ?? string.Empty).Trim();
            case CompletionProviderType.OpenAICompatible:
                if (!string.IsNullOrWhiteSpace(options.OpenAICompatibleApiKey))
                {
                    return options.OpenAICompatibleApiKey.Trim();
                }

                return (Environment.GetEnvironmentVariable("OPENAI_COMPATIBLE_API_KEY") ?? string.Empty).Trim();
            case CompletionProviderType.LocalOpenAICompatible:
            default:
                return string.Empty;
        }
    }

    private static string ResolveModelName(CompletionProviderType providerType, string configuredModel)
    {
        if (!string.IsNullOrWhiteSpace(configuredModel))
        {
            return configuredModel.Trim();
        }

        return providerType == CompletionProviderType.DeepSeek ? "deepseek-v4-flash" : string.Empty;
    }

    private static string GetProviderName(CompletionProviderType providerType)
    {
        switch (providerType)
        {
            case CompletionProviderType.DeepSeek:
                return "DeepSeek";
            case CompletionProviderType.OpenRouter:
                return "OpenRouter";
            case CompletionProviderType.LocalOpenAICompatible:
                return "LocalOpenAICompatible";
            case CompletionProviderType.OpenAICompatible:
            default:
                return "OpenAICompatible";
        }
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
