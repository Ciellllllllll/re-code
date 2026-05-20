using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace GhostTextVsix.Diagnostics;

internal sealed class DeepSeekOutputLogger
{
    private static readonly Guid PaneGuid = new("A983A9D6-533C-4D28-BBE6-5EECE4B25E64");
    private readonly IVsOutputWindowPane _pane;

    private DeepSeekOutputLogger(IVsOutputWindowPane pane)
    {
        _pane = pane;
    }

    public static async Task<DeepSeekOutputLogger> CreateAsync(AsyncPackage package)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var outputWindow = (IVsOutputWindow)await package.GetServiceAsync(typeof(SVsOutputWindow));
        var paneGuid = PaneGuid;
        ErrorHandler.ThrowOnFailure(outputWindow.CreatePane(ref paneGuid, "re:code", 1, 1));
        ErrorHandler.ThrowOnFailure(outputWindow.GetPane(ref paneGuid, out var pane));
        return new DeepSeekOutputLogger(pane);
    }

    public void Activate()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        _pane.Activate();
    }

    public void Info(string message) => Write("INFO", message);

    public void Warning(string message) => Write("WARN", message);

    public void Error(string message) => Write("ERROR", message);

    private void Write(string level, string message)
    {
        ThreadHelper.JoinableTaskFactory.Run(async delegate
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var line = $"[{DateTimeOffset.Now.ToString("u", CultureInfo.InvariantCulture)}] [{level}] {message}{Environment.NewLine}";
            _pane.OutputStringThreadSafe(line);
        });
    }
}
