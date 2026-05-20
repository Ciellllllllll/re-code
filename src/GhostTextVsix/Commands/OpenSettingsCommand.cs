using System;
using System.ComponentModel.Design;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;

namespace GhostTextVsix.Commands;

internal sealed class OpenSettingsCommand
{
    private readonly AsyncPackage _package;

    private OpenSettingsCommand(AsyncPackage package, OleMenuCommandService commandService)
    {
        _package = package;
        var commandId = new CommandID(new Guid(Guids.CommandSetString), PackageIds.OpenSettingsCommand);
        var menuItem = new OleMenuCommand((_, _) => Execute(), commandId);
        commandService.AddCommand(menuItem);
    }

    public static Task InitializeAsync(AsyncPackage package, IMenuCommandService commandService)
    {
        new OpenSettingsCommand(package, (OleMenuCommandService)commandService);
        return Task.CompletedTask;
    }

    private void Execute()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        _package.ShowOptionPage(typeof(Settings.DeepSeekOptionsPage));
    }
}
