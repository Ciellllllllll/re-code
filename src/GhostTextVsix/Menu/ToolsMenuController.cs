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
    private bool _disposed;

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

        AddButton("補完を生成", async () => await _completionCoordinator.RequestManualCompletionAsync());
        AddButton("自動補完を切り替え", () =>
        {
            var enabled = !_settingsManager.IsAutoCompletionEnabled();
            _settingsManager.SetAutoCompletionEnabled(enabled);
            _logger.Info($"Auto completion {(enabled ? "enabled" : "disabled")}.");
        });
        AddButton("設定を開く", () =>
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _package.ShowOptionPage(typeof(DeepSeekOptionsPage));
        });
        AddButton("診断を表示", () =>
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _diagnosticsController.ShowDiagnostics();
        });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            SafeLog.Info(_logger, "ToolsMenuController dispose skipped because already disposed.");
            return;
        }

        _disposed = true;
        SafeLog.Info(_logger, "ToolsMenuController dispose started.");
        try
        {
            foreach (var binding in _bindings.ToArray())
            {
                SafeUnsubscribe(binding);
            }
        }
        catch (Exception ex) when (SafeLog.IsExpectedDisposeException(ex))
        {
            SafeLog.Warning(_logger, $"ToolsMenuController dispose ignored {ex.GetType().Name}", ex);
        }
        finally
        {
            _bindings.Clear();
            _rootPopup = null;
        }

        SafeLog.Info(_logger, "ToolsMenuController dispose completed.");
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
            try
            {
                if (toolsBar.Controls[index] is CommandBarPopup popup &&
                    string.Equals(popup.Caption, RootCaption, StringComparison.Ordinal))
                {
                    popup.Delete(true);
                }
            }
            catch (Exception ex) when (SafeLog.IsExpectedDisposeException(ex))
            {
                return;
            }
        }
    }

    private void SafeUnsubscribe(MenuButtonBinding binding)
    {
        try
        {
            binding.Button.Click -= binding.Handler;
        }
        catch (Exception ex) when (SafeLog.IsExpectedDisposeException(ex))
        {
            SafeLog.Warning(_logger, $"ToolsMenuController dispose ignored {ex.GetType().Name}", ex);
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
