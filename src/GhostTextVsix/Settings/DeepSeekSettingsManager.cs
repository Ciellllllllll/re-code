using System;

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

    public bool IsAutoCompletionEnabled() => GetOptions().EnableAutoCompletion;

    public TimeSpan GetDebounceTime() => TimeSpan.FromMilliseconds(Math.Max(100, GetOptions().DebounceMilliseconds));

    public TimeSpan GetCacheTtl() => TimeSpan.FromSeconds(Math.Max(1, GetOptions().CacheTtlSeconds));

    public void SetAutoCompletionEnabled(bool enabled)
    {
        var options = GetOptions();
        options.EnableAutoCompletion = enabled;
        options.SaveSettingsToStorage();
    }
}
