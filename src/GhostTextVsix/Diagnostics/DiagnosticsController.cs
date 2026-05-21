using GhostTextVsix.Completion;
using GhostTextVsix.Settings;
using Microsoft.VisualStudio.Shell;

namespace GhostTextVsix.Diagnostics;

internal sealed class DiagnosticsController
{
    private readonly DeepSeekOutputLogger _logger;
    private readonly DeepSeekSettingsManager _settingsManager;
    private readonly CompletionCoordinator _coordinator;

    public DiagnosticsController(DeepSeekOutputLogger logger, DeepSeekSettingsManager settingsManager, CompletionCoordinator coordinator)
    {
        _logger = logger;
        _settingsManager = settingsManager;
        _coordinator = coordinator;
    }

    public void ShowDiagnostics()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        _logger.Activate();
        var provider = _settingsManager.GetProviderConfig();
        _logger.Info($"Provider={provider.ProviderName}, ProviderConfigured={provider.IsConfigured}, ApiKeyConfigured={IsApiKeyConfigured(provider)}, Model={provider.ModelName}, EnableAutoCompletion={_settingsManager.IsAutoCompletionEnabled()}, State={_coordinator.State}");
    }

    private static bool IsApiKeyConfigured(Completion.Providers.CompletionProviderConfig config)
    {
        return !config.RequiresApiKey || !string.IsNullOrWhiteSpace(config.ApiKey);
    }
}
