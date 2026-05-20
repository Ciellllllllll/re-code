using System;
using System.ComponentModel.Design;
using System.Threading.Tasks;
using GhostTextVsix.Diagnostics;
using GhostTextVsix.Settings;
using Microsoft.VisualStudio.Shell;

namespace GhostTextVsix.Commands;

internal sealed class ToggleAutoCompletionCommand
{
    private readonly DeepSeekSettingsManager _settingsManager;
    private readonly DeepSeekOutputLogger _logger;

    private ToggleAutoCompletionCommand(OleMenuCommandService commandService, DeepSeekSettingsManager settingsManager, DeepSeekOutputLogger logger)
    {
        _settingsManager = settingsManager;
        _logger = logger;

        var commandId = new CommandID(new Guid(Guids.CommandSetString), PackageIds.ToggleAutoCompletionCommand);
        var menuItem = new OleMenuCommand((_, _) => Execute(), commandId);
        commandService.AddCommand(menuItem);
    }

    public static Task InitializeAsync(AsyncPackage package, IMenuCommandService commandService, DeepSeekSettingsManager settingsManager, DeepSeekOutputLogger logger)
    {
        new ToggleAutoCompletionCommand((OleMenuCommandService)commandService, settingsManager, logger);
        return Task.CompletedTask;
    }

    private void Execute()
    {
        var enabled = !_settingsManager.IsAutoCompletionEnabled();
        _settingsManager.SetAutoCompletionEnabled(enabled);
        _logger.Info($"Auto completion {(enabled ? "enabled" : "disabled")}.");
    }
}
