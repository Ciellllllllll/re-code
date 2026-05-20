using System;
using System.ComponentModel.Design;
using System.Threading.Tasks;
using GhostTextVsix.Diagnostics;
using Microsoft.VisualStudio.Shell;

namespace GhostTextVsix.Commands;

internal sealed class ShowDiagnosticsCommand
{
    private readonly DiagnosticsController _controller;

    private ShowDiagnosticsCommand(OleMenuCommandService commandService, DiagnosticsController controller)
    {
        _controller = controller;
        var commandId = new CommandID(new Guid(Guids.CommandSetString), PackageIds.ShowDiagnosticsCommand);
        var menuItem = new OleMenuCommand((_, _) => Execute(), commandId);
        commandService.AddCommand(menuItem);
    }

    public static Task InitializeAsync(AsyncPackage package, IMenuCommandService commandService, DiagnosticsController controller)
    {
        new ShowDiagnosticsCommand((OleMenuCommandService)commandService, controller);
        return Task.CompletedTask;
    }

    private void Execute()
    {
        _controller.ShowDiagnostics();
    }
}
