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
        _logger.Info($"State={_coordinator.State}, AutoCompletion={_settingsManager.IsAutoCompletionEnabled()}, AutoProvider={autoProvider.ProviderName}, AutoModel={autoProvider.ModelName}, AutoApiKeyConfigured={autoProvider.IsLocal || !string.IsNullOrWhiteSpace(autoProvider.ApiKey)}, ManualProvider={manualProvider.ProviderName}, ManualModel={manualProvider.ModelName}, ManualApiKeyConfigured={manualProvider.IsLocal || !string.IsNullOrWhiteSpace(manualProvider.ApiKey)}");
    }
}
