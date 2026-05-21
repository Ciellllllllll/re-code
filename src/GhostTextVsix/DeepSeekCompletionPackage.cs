using System;
using System.Diagnostics;
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
[InstalledProductRegistration("re:code", "C/C++ GhostText 補完", "0.2")]
[ProvideOptionPage(typeof(DeepSeekOptionsPage), "re:code", "一般", 0, 0, true)]
[ProvideAutoLoad(UIContextGuids80.NoSolution, PackageAutoLoadFlags.BackgroundLoad)]
[Guid(Guids.PackageString)]
public sealed class DeepSeekCompletionPackage : AsyncPackage
{
    private CompletionCoordinator _completionCoordinator;
    private DiagnosticsController _diagnosticsController;
    private EditorEventMonitor _editorEventMonitor;
    private ToolsMenuController _toolsMenuController;
    private DeepSeekOutputLogger _logger;
    private bool _disposed;

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
        _logger = logger;
        var detector = new CppDocumentDetector();
        var securityFilter = new SecurityFilter();
        var contextCollector = new EditorContextCollector(detector, securityFilter, logger);
        var promptBuilder = new PromptBuilder();
        var normalizer = new CompletionIndentNormalizer();
        var editorAdapter = componentModel.GetService<Microsoft.VisualStudio.Editor.IVsEditorAdaptersFactoryService>();
        var viewLocator = new ActiveTextViewLocator(textManager, editorAdapter);

        _completionCoordinator = new CompletionCoordinator(
            settingsManager,
            logger,
            detector,
            contextCollector,
            promptBuilder,
            normalizer,
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
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (disposing)
        {
            SafeLogDisposeInfo("Package dispose started.");
            try
            {
                ThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    await JoinableTaskFactory.SwitchToMainThreadAsync();
                    SafeDisposeComponent("ToolsMenuController", () => _toolsMenuController?.Dispose());
                    SafeDisposeComponent("EditorEventMonitor", () => _editorEventMonitor?.Dispose());
                });
            }
            catch (ObjectDisposedException ex)
            {
                SafeLogDisposeWarning("Package dispose ignored ObjectDisposedException", ex);
            }
            catch (COMException ex)
            {
                SafeLogDisposeWarning("Package dispose ignored COMException", ex);
            }
            catch (InvalidOperationException ex)
            {
                SafeLogDisposeWarning("Package dispose ignored InvalidOperationException", ex);
            }
            catch (Exception ex)
            {
                SafeLogDisposeWarning("Package dispose ignored exception", ex);
            }
            finally
            {
                _toolsMenuController = null;
                _editorEventMonitor = null;
                SafeLogDisposeInfo("Package dispose completed.");
            }
        }

        base.Dispose(disposing);
    }

    private void SafeDisposeComponent(string name, Action disposeAction)
    {
        try
        {
            disposeAction();
        }
        catch (ObjectDisposedException ex)
        {
            SafeLogDisposeWarning($"{name} dispose ignored ObjectDisposedException", ex);
        }
        catch (COMException ex)
        {
            SafeLogDisposeWarning($"{name} dispose ignored COMException", ex);
        }
        catch (InvalidOperationException ex)
        {
            SafeLogDisposeWarning($"{name} dispose ignored InvalidOperationException", ex);
        }
        catch (Exception ex)
        {
            SafeLogDisposeWarning($"{name} dispose ignored exception", ex);
        }
    }

    private void SafeLogDisposeInfo(string message)
    {
        try
        {
            _logger?.Info(message);
        }
        catch
        {
            Debug.WriteLine(message);
        }
    }

    private void SafeLogDisposeWarning(string message, Exception ex)
    {
        var safeMessage = $"{message}. ErrorType={ex.GetType().Name}";
        try
        {
            _logger?.Warning(safeMessage);
        }
        catch
        {
            Debug.WriteLine(safeMessage);
        }
    }
}
