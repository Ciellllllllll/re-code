using System;
using System.Collections.Generic;
using EnvDTE80;
using GhostTextVsix.Completion;
using GhostTextVsix.Diagnostics;
using GhostTextVsix.Settings;
using Microsoft.VisualStudio.CommandBars;
using Microsoft.VisualStudio.Shell;

namespace GhostTextVsix.Menu;

internal sealed class ToolsMenuController : IDisposable
{
    private const string RootCaption = "re:code";

    private readonly AsyncPackage _package;
    private readonly DTE2 _dte;
    private readonly CompletionCoordinator _completionCoordinator;
    private readonly DeepSeekSettingsManager _settingsManager;
    private readonly DiagnosticsController _diagnosticsController;
    private readonly DeepSeekOutputLogger _logger;
    private readonly List<MenuButtonBinding> _bindings = new();
    private CommandBarPopup _rootPopup;

    public ToolsMenuController(
        AsyncPackage package,
        DTE2 dte,
        CompletionCoordinator completionCoordinator,
        DeepSeekSettingsManager settingsManager,
        DiagnosticsController diagnosticsController,
        DeepSeekOutputLogger logger)
    {
        _package = package;
        _dte = dte;
        _completionCoordinator = completionCoordinator;
        _settingsManager = settingsManager;
        _diagnosticsController = diagnosticsController;
        _logger = logger;
    }

    public void Initialize()
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var commandBars = (CommandBars)_dte.CommandBars;
        var toolsBar = commandBars["Tools"];
        RemoveExistingPopup(toolsBar);

        _rootPopup = (CommandBarPopup)toolsBar.Controls.Add(
            MsoControlType.msoControlPopup,
            Type.Missing,
            Type.Missing,
            1,
            true);
        _rootPopup.Caption = RootCaption;

        AddButton("Generate Completion", async () => await _completionCoordinator.RequestManualCompletionAsync());
        AddButton("Toggle Auto Completion", () =>
        {
            var enabled = !_settingsManager.IsAutoCompletionEnabled();
            _settingsManager.SetAutoCompletionEnabled(enabled);
            _logger.Info($"Auto completion {(enabled ? "enabled" : "disabled")}.");
        });
        AddButton("Open Settings", () =>
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _package.ShowOptionPage(typeof(DeepSeekOptionsPage));
        });
        AddButton("Show Diagnostics", () =>
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _diagnosticsController.ShowDiagnostics();
        });
    }

    public void Dispose()
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        foreach (var binding in _bindings)
        {
            binding.Button.Click -= binding.Handler;
        }

        _bindings.Clear();

        if (_rootPopup != null)
        {
            try
            {
                _rootPopup.Delete(true);
            }
            catch
            {
                // Ignore cleanup failures during shutdown.
            }
        }
    }

    private void AddButton(string caption, Action action)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        AddButton(caption, () =>
        {
            action();
            return System.Threading.Tasks.Task.CompletedTask;
        });
    }

    private void AddButton(string caption, Func<System.Threading.Tasks.Task> action)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var button = (CommandBarButton)_rootPopup.Controls.Add(
            MsoControlType.msoControlButton,
            Type.Missing,
            Type.Missing,
            Type.Missing,
            true);
        button.Caption = caption;
        _CommandBarButtonEvents_ClickEventHandler handler = (CommandBarButton ctrl, ref bool cancelDefault) =>
        {
            cancelDefault = true;
            _ = ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                await action();
            });
        };
        button.Click += handler;
        _bindings.Add(new MenuButtonBinding(button, handler));
    }

    private static void RemoveExistingPopup(CommandBar toolsBar)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        for (var index = toolsBar.Controls.Count; index >= 1; index--)
        {
            if (toolsBar.Controls[index] is CommandBarPopup popup &&
                string.Equals(popup.Caption, RootCaption, StringComparison.Ordinal))
            {
                popup.Delete(true);
            }
        }
    }

    private sealed class MenuButtonBinding
    {
        public MenuButtonBinding(CommandBarButton button, _CommandBarButtonEvents_ClickEventHandler handler)
        {
            Button = button;
            Handler = handler;
        }

        public CommandBarButton Button { get; }

        public _CommandBarButtonEvents_ClickEventHandler Handler { get; }
    }
}
