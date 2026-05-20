using System;
using System.ComponentModel.Design;
using System.Threading.Tasks;
using GhostTextVsix.Completion;
using Microsoft.VisualStudio.Shell;

namespace GhostTextVsix.Commands;

internal sealed class GenerateCompletionCommand
{
    private readonly AsyncPackage _package;
    private readonly CompletionCoordinator _coordinator;

    private GenerateCompletionCommand(AsyncPackage package, OleMenuCommandService commandService, CompletionCoordinator coordinator)
    {
        _package = package;
        _coordinator = coordinator;

        var commandId = new CommandID(new Guid(Guids.CommandSetString), PackageIds.GenerateCompletionCommand);
        var menuItem = new OleMenuCommand(async (_, _) => await ExecuteAsync(), commandId);
        commandService.AddCommand(menuItem);
    }

    public static Task InitializeAsync(AsyncPackage package, IMenuCommandService commandService, CompletionCoordinator coordinator)
    {
        new GenerateCompletionCommand(package, (OleMenuCommandService)commandService, coordinator);
        return Task.CompletedTask;
    }

    private async Task ExecuteAsync()
    {
        await _package.JoinableTaskFactory.SwitchToMainThreadAsync();
        await _coordinator.RequestManualCompletionAsync();
    }
}
