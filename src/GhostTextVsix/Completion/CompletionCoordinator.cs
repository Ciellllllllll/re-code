using System;
using System.Threading;
using System.Threading.Tasks;
using GhostTextVsix.Diagnostics;
using GhostTextVsix.Editor;
using GhostTextVsix.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;

namespace GhostTextVsix.Completion;

internal sealed class CompletionCoordinator
{
    private readonly DeepSeekSettingsManager _settingsManager;
    private readonly DeepSeekOutputLogger _logger;
    private readonly CppDocumentDetector _detector;
    private readonly EditorContextCollector _contextCollector;
    private readonly PromptBuilder _promptBuilder;
    private readonly DeepSeekApiClient _apiClient;
    private readonly CompletionResponseFormatter _formatter;
    private readonly ActiveTextViewLocator _activeTextViewLocator;

    public CompletionCoordinator(
        DeepSeekSettingsManager settingsManager,
        DeepSeekOutputLogger logger,
        CppDocumentDetector detector,
        EditorContextCollector contextCollector,
        PromptBuilder promptBuilder,
        DeepSeekApiClient apiClient,
        CompletionResponseFormatter formatter,
        ActiveTextViewLocator activeTextViewLocator)
    {
        _settingsManager = settingsManager;
        _logger = logger;
        _detector = detector;
        _contextCollector = contextCollector;
        _promptBuilder = promptBuilder;
        _apiClient = apiClient;
        _formatter = formatter;
        _activeTextViewLocator = activeTextViewLocator;
    }

    public CompletionState State { get; private set; } = CompletionState.Idle;

    public void SetState(CompletionState state)
    {
        State = state;
    }

    public async Task RequestManualCompletionAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        IWpfTextView view = _activeTextViewLocator.GetActiveView();
        if (view == null)
        {
            _logger.Warning("No active editor view found.");
            return;
        }

        var filePath = view.TextBuffer.GetFileName();
        if (!_detector.IsSupported(filePath))
        {
            _logger.Info("Generate Completion ignored because the active document is not a supported C/C++ file.");
            return;
        }

        if (GhostTextBroker.Accept(view))
        {
            return;
        }

        var options = _settingsManager.GetOptions();
        if (!_contextCollector.TryCollect(view, options.MaxPrefixLines, options.MaxSuffixLines, out var context))
        {
            _logger.Warning("Context collection was blocked or failed.");
            return;
        }

        try
        {
            SetState(CompletionState.RequestingCompletion);
            using var cts = new CancellationTokenSource();
            var raw = await _apiClient.RequestCompletionAsync(
                _promptBuilder.SystemMessage,
                _promptBuilder.BuildUserPrompt(context),
                cts.Token);

            var formatted = _formatter.Format(context, raw);
            if (string.IsNullOrWhiteSpace(formatted))
            {
                SetState(CompletionState.Dismissed);
                _logger.Warning("Completion response was empty after formatting.");
                return;
            }

            _logger.Info($"Completion formatted. Lines={formatted.Split(new[] { Environment.NewLine }, StringSplitOptions.None).Length}, Chars={formatted.Length}");
            if (GhostTextBroker.TryShow(view, formatted))
            {
                _logger.Info($"GhostText shown with {formatted.Length} chars.");
            }
        }
        catch (OperationCanceledException)
        {
            SetState(CompletionState.Error);
            _logger.Warning("Completion request timed out or was cancelled.");
        }
        catch (Exception ex)
        {
            SetState(CompletionState.Error);
            _logger.Error($"Completion request failed. ErrorType={ex.GetType().Name}");
        }
    }
}
