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
    private readonly CompletionCache _cache = new();
    private readonly object _autoLock = new();
    private CancellationTokenSource _autoCompletionCts;
    private long _lastAutoRequestId;
    private const string ModelName = "deepseek-v4-flash";

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
        CancelAutoCompletion("ManualCompletion");

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

        await ExecuteCompletionAsync(view, isAutoCompletion: false, requestId: 0, CancellationToken.None);
    }

    public void ScheduleAutoCompletion(IWpfTextView view)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var requestId = Interlocked.Increment(ref _lastAutoRequestId);
        CancelAutoCompletion($"NewInput RequestId={requestId}");

        if (!_settingsManager.IsAutoCompletionEnabled())
        {
            _logger.Info($"Auto completion skipped. RequestId={requestId}, Reason=Disabled");
            return;
        }

        if (string.IsNullOrWhiteSpace(_settingsManager.GetApiKey()))
        {
            _logger.Info($"Auto completion skipped. RequestId={requestId}, Reason=ApiKeyMissing");
            return;
        }

        var filePath = view.TextBuffer.GetFileName();
        if (!_detector.IsSupported(filePath))
        {
            _logger.Info($"Auto completion skipped. RequestId={requestId}, Reason=UnsupportedFile");
            return;
        }

        var cts = new CancellationTokenSource();
        lock (_autoLock)
        {
            _autoCompletionCts = cts;
        }

        SetState(CompletionState.WaitingForDebounce);
        _logger.Info($"Auto completion scheduled. RequestId={requestId}, DebounceMs={_settingsManager.GetDebounceTime().TotalMilliseconds:F0}");

        _ = ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
        {
            try
            {
                await Task.Delay(_settingsManager.GetDebounceTime(), cts.Token);
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cts.Token);
                _logger.Info($"Auto completion debounce elapsed. RequestId={requestId}");
                await ExecuteCompletionAsync(view, isAutoCompletion: true, requestId, cts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.Info($"Auto completion canceled. RequestId={requestId}");
                if (State == CompletionState.WaitingForDebounce || State == CompletionState.RequestingCompletion)
                {
                    SetState(CompletionState.Idle);
                }
            }
            catch (Exception ex)
            {
                SetState(CompletionState.Error);
                _logger.Error($"Auto completion request failed. RequestId={requestId}, ErrorType={ex.GetType().Name}");
                SetState(CompletionState.Idle);
            }
        });
    }

    public void CancelAutoCompletion(string reason)
    {
        CancellationTokenSource cts = null;
        lock (_autoLock)
        {
            cts = _autoCompletionCts;
            _autoCompletionCts = null;
        }

        if (cts == null)
        {
            return;
        }

        cts.Cancel();

        _logger.Info($"Auto completion canceled. Reason={reason}");
    }

    private async Task ExecuteCompletionAsync(IWpfTextView view, bool isAutoCompletion, long requestId, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var options = _settingsManager.GetOptions();
        if (!_contextCollector.TryCollect(view, options.MaxPrefixLines, options.MaxSuffixLines, out var context))
        {
            _logger.Warning($"{(isAutoCompletion ? "Auto completion" : "Completion")} skipped. RequestId={requestId}, Reason=ContextCollectionFailed");
            SetState(CompletionState.Idle);
            return;
        }

        var snapshot = CompletionRequestSnapshot.Create(context, ModelName, options.MaxPrefixLines, options.MaxSuffixLines);
        var cacheKey = snapshot.CacheKey;

        try
        {
            if (isAutoCompletion && _cache.TryGet(cacheKey, out var cachedCompletion))
            {
                _logger.Info($"Cache hit. RequestId={requestId}");
                if (ContextStillMatches(view, options.MaxPrefixLines, options.MaxSuffixLines, snapshot, cancellationToken))
                {
                    if (GhostTextBroker.TryShow(view, cachedCompletion))
                    {
                        _logger.Info($"Auto completion request succeeded. RequestId={requestId}, Source=Cache, Lines={CountLines(cachedCompletion)}, Chars={cachedCompletion.Length}");
                    }
                }
                else
                {
                    _logger.Info($"Stale request ignored. RequestId={requestId}");
                    SetState(CompletionState.Idle);
                }

                return;
            }

            if (isAutoCompletion)
            {
                _logger.Info($"Cache miss. RequestId={requestId}");
                _logger.Info($"Auto completion request started. RequestId={requestId}");
            }

            SetState(CompletionState.RequestingCompletion);
            var raw = await _apiClient.RequestCompletionAsync(
                _promptBuilder.SystemMessage,
                _promptBuilder.BuildUserPrompt(context),
                cancellationToken);

            var formatted = _formatter.Format(context, raw);
            if (string.IsNullOrWhiteSpace(formatted))
            {
                SetState(CompletionState.Idle);
                _logger.Warning($"{(isAutoCompletion ? "Auto completion" : "Completion")} response was empty after formatting. RequestId={requestId}");
                return;
            }

            if (isAutoCompletion)
            {
                _cache.Set(cacheKey, formatted, _settingsManager.GetCacheTtl());
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            if (!ContextStillMatches(view, options.MaxPrefixLines, options.MaxSuffixLines, snapshot, cancellationToken))
            {
                _logger.Info($"Context changed, completion discarded. RequestId={requestId}");
                SetState(CompletionState.Idle);
                return;
            }

            _logger.Info($"Completion formatted. RequestId={requestId}, Lines={CountLines(formatted)}, Chars={formatted.Length}");
            if (GhostTextBroker.TryShow(view, formatted))
            {
                if (isAutoCompletion)
                {
                    _logger.Info($"Auto completion request succeeded. RequestId={requestId}, Lines={CountLines(formatted)}, Chars={formatted.Length}");
                }
                else
                {
                    _logger.Info($"GhostText shown with {formatted.Length} chars.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            SetState(CompletionState.Idle);
            _logger.Info($"{(isAutoCompletion ? "Auto completion" : "Completion")} canceled. RequestId={requestId}");
        }
        catch (Exception ex)
        {
            SetState(CompletionState.Error);
            _logger.Error($"{(isAutoCompletion ? "Auto completion request failed" : "Completion request failed")}. RequestId={requestId}, ErrorType={ex.GetType().Name}");
            SetState(CompletionState.Idle);
        }
    }

    private bool ContextStillMatches(
        IWpfTextView view,
        int maxPrefixLines,
        int maxSuffixLines,
        CompletionRequestSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        cancellationToken.ThrowIfCancellationRequested();

        if (!_contextCollector.TryCollect(view, maxPrefixLines, maxSuffixLines, out var currentContext))
        {
            return false;
        }

        return snapshot.Matches(currentContext);
    }

    private static int CountLines(string text)
    {
        return string.IsNullOrEmpty(text)
            ? 0
            : text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).Length;
    }
}
