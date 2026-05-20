using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using GhostTextVsix.Completion;
using GhostTextVsix.Diagnostics;
using GhostTextVsix.Editor;
using GhostTextVsix.Menu;
using GhostTextVsix.Security;
using GhostTextVsix.Settings;
using EnvDTE80;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TextManager.Interop;

namespace GhostTextVsix;

[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[InstalledProductRegistration("re:code", "C/C++ ghost text completion", "0.2")]
[ProvideOptionPage(typeof(DeepSeekOptionsPage), "re:code", "General", 0, 0, true)]
[ProvideAutoLoad(UIContextGuids80.NoSolution, PackageAutoLoadFlags.BackgroundLoad)]
[Guid(Guids.PackageString)]
public sealed class DeepSeekCompletionPackage : AsyncPackage
{
    private CompletionCoordinator _completionCoordinator;
    private DiagnosticsController _diagnosticsController;
    private EditorEventMonitor _editorEventMonitor;
    private ToolsMenuController _toolsMenuController;

    internal static DeepSeekCompletionPackage Instance { get; private set; }

    protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        Instance = this;

        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var componentModel = (IComponentModel)await GetServiceAsync(typeof(SComponentModel));
        var dte = (DTE2)await GetServiceAsync(typeof(SDTE));
        var textManager = (IVsTextManager)await GetServiceAsync(typeof(SVsTextManager));
        var monitorSelection = (IVsMonitorSelection)await GetServiceAsync(typeof(SVsShellMonitorSelection));

        var settingsManager = new DeepSeekSettingsManager(this);
        var logger = await DeepSeekOutputLogger.CreateAsync(this);
        var detector = new CppDocumentDetector();
        var securityFilter = new SecurityFilter();
        var contextCollector = new EditorContextCollector(detector, securityFilter, logger);
        var promptBuilder = new PromptBuilder();
        var formatter = new CompletionResponseFormatter();
        var editorAdapter = componentModel.GetService<Microsoft.VisualStudio.Editor.IVsEditorAdaptersFactoryService>();
        var viewLocator = new ActiveTextViewLocator(textManager, editorAdapter);

        _completionCoordinator = new CompletionCoordinator(
            settingsManager,
            logger,
            detector,
            contextCollector,
            promptBuilder,
            formatter,
            viewLocator);
        var commitHandler = new CompletionCommitHandler(_completionCoordinator, logger);

        _diagnosticsController = new DiagnosticsController(logger, settingsManager, _completionCoordinator);
        GhostTextBroker.Initialize(_completionCoordinator, detector, commitHandler, logger);
        _editorEventMonitor = new EditorEventMonitor(monitorSelection, logger);
        _editorEventMonitor.Start();
        _toolsMenuController = new ToolsMenuController(this, dte, _completionCoordinator, settingsManager, _diagnosticsController, logger);
        _toolsMenuController.Initialize();

        logger.Info("Package initialized.");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await JoinableTaskFactory.SwitchToMainThreadAsync();
                _toolsMenuController?.Dispose();
                _editorEventMonitor?.Dispose();
            });
        }

        base.Dispose(disposing);
    }
}
