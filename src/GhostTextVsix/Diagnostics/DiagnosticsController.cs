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
        var autoProvider = _settingsManager.GetAutoProviderConfig();
        var manualProvider = _settingsManager.GetManualProviderConfig();
        _logger.Info($"AutoProvider={autoProvider.ProviderName}, AutoProviderConfigured={autoProvider.IsConfigured}, AutoApiKeyConfigured={IsApiKeyConfigured(autoProvider)}, AutoModel={autoProvider.ModelName}, ManualProvider={manualProvider.ProviderName}, ManualProviderConfigured={manualProvider.IsConfigured}, ManualApiKeyConfigured={IsApiKeyConfigured(manualProvider)}, ManualModel={manualProvider.ModelName}, EnableAutoCompletion={_settingsManager.IsAutoCompletionEnabled()}, State={_coordinator.State}");
    }

    private static bool IsApiKeyConfigured(Completion.Providers.CompletionProviderConfig config)
    {
        return !config.RequiresApiKey || !string.IsNullOrWhiteSpace(config.ApiKey);
    }
}
