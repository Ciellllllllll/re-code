using System;
using System.Threading;
using System.Threading.Tasks;
using GhostTextVsix.Completion.Providers;
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
    private readonly CompletionResponseFormatter _formatter;
    private readonly ActiveTextViewLocator _activeTextViewLocator;
    private readonly CompletionProviderFactory _providerFactory;
    private readonly CompletionCache _cache = new();
    private readonly object _autoLock = new();
    private CancellationTokenSource _autoCompletionCts;
    private long _lastAutoRequestId;

    public CompletionCoordinator(
        DeepSeekSettingsManager settingsManager,
        DeepSeekOutputLogger logger,
        CppDocumentDetector detector,
        EditorContextCollector contextCollector,
        PromptBuilder promptBuilder,
        CompletionResponseFormatter formatter,
        ActiveTextViewLocator activeTextViewLocator)
    {
        _settingsManager = settingsManager;
        _logger = logger;
        _detector = detector;
        _contextCollector = contextCollector;
        _promptBuilder = promptBuilder;
        _formatter = formatter;
        _activeTextViewLocator = activeTextViewLocator;
        _providerFactory = new CompletionProviderFactory(logger);
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

        var filePath = view.TextBuffer.GetFileName();
        if (!_detector.IsSupported(filePath))
        {
            _logger.Info($"Auto completion skipped. RequestId={requestId}, Reason=UnsupportedFile");
            return;
        }

        var providerConfig = _settingsManager.GetAutoProviderConfig();
        if (ShouldSkipProvider(providerConfig, CompletionMode.Auto, requestId, "Api"))
        {
            return;
        }

        var cts = new CancellationTokenSource();
        lock (_autoLock)
        {
            _autoCompletionCts = cts;
        }

        SetState(CompletionState.WaitingForDebounce);
        _logger.Info($"Auto completion scheduled. RequestId={requestId}, DebounceMs={_settingsManager.GetDebounceTime().TotalMilliseconds:F0}, State={State}");

        _ = ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
        {
            try
            {
                await Task.Delay(_settingsManager.GetDebounceTime(), cts.Token);
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cts.Token);
                _logger.Info($"Auto completion debounce elapsed. RequestId={requestId}, State={State}");
                await ExecuteCompletionAsync(view, isAutoCompletion: true, requestId, cts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.Info($"Auto completion canceled. RequestId={requestId}, State={State}");
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

        _logger.Info($"Auto completion canceled. Reason={reason}, State={State}");
    }

    private async Task ExecuteCompletionAsync(IWpfTextView view, bool isAutoCompletion, long requestId, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var options = _settingsManager.GetOptions();
        var requestOptions = CreateRequestOptions(options, isAutoCompletion);
        var started = DateTimeOffset.UtcNow;
        if (ShouldSkipProvider(
            requestOptions.ProviderConfig,
            isAutoCompletion ? CompletionMode.Auto : CompletionMode.Manual,
            requestId,
            requestOptions.Source))
        {
            SetState(CompletionState.Idle);
            return;
        }

        if (!_contextCollector.TryCollect(view, requestOptions.MaxPrefixLines, requestOptions.MaxSuffixLines, out var context))
        {
            _logger.Warning($"{(isAutoCompletion ? "Auto completion" : "Completion")} skipped. RequestId={requestId}, Reason=ContextCollectionFailed, State={State}");
            SetState(CompletionState.Idle);
            return;
        }

        var snapshot = CompletionRequestSnapshot.Create(
            context,
            requestOptions.ProviderConfig.ModelName,
            requestOptions.ProviderConfig.ProviderName,
            requestOptions.ProviderConfig.BaseUrl,
            requestOptions.ProviderConfig.MaxTokens,
            requestOptions.MaxPrefixLines,
            requestOptions.MaxSuffixLines,
            requestOptions.MaxCompletionLines,
            requestOptions.MaxCompletionCharacters);
        var cacheKey = snapshot.CacheKey;

        try
        {
            if (isAutoCompletion && _cache.TryGet(cacheKey, out var cachedCompletion))
            {
                _logger.Info($"Cache hit. ProviderName={requestOptions.ProviderConfig.ProviderName}, ModelName={requestOptions.ProviderConfig.ModelName}, RequestId={requestId}, Source=Cache, LatencyMs={ElapsedMs(started):F0}, State={State}");
                if (ContextStillMatches(view, requestOptions.MaxPrefixLines, requestOptions.MaxSuffixLines, snapshot, cancellationToken))
                {
                    if (GhostTextBroker.TryShow(view, cachedCompletion, requestId, "Cache"))
                    {
                        _logger.Info($"GhostText shown. ProviderName={requestOptions.ProviderConfig.ProviderName}, ModelName={requestOptions.ProviderConfig.ModelName}, RequestId={requestId}, Source=Cache, Lines={CountLines(cachedCompletion)}, Chars={cachedCompletion.Length}, LatencyMs={ElapsedMs(started):F0}, State={State}");
                        _logger.Info($"Auto completion request succeeded. ProviderName={requestOptions.ProviderConfig.ProviderName}, ModelName={requestOptions.ProviderConfig.ModelName}, RequestId={requestId}, Source=Cache, Lines={CountLines(cachedCompletion)}, Chars={cachedCompletion.Length}, LatencyMs={ElapsedMs(started):F0}, State={State}");
                    }
                }
                else
                {
                    _logger.Info($"Stale request ignored. ProviderName={requestOptions.ProviderConfig.ProviderName}, ModelName={requestOptions.ProviderConfig.ModelName}, RequestId={requestId}, Source=Cache, LatencyMs={ElapsedMs(started):F0}, State={State}");
                    SetState(CompletionState.Idle);
                }

                return;
            }

            if (isAutoCompletion)
            {
                _logger.Info($"Cache miss. ProviderName={requestOptions.ProviderConfig.ProviderName}, ModelName={requestOptions.ProviderConfig.ModelName}, RequestId={requestId}, Source=Api, State={State}");
                _logger.Info($"Auto completion request started. ProviderName={requestOptions.ProviderConfig.ProviderName}, ModelName={requestOptions.ProviderConfig.ModelName}, RequestId={requestId}, Source=Api, PrefixLines={requestOptions.MaxPrefixLines}, SuffixLines={requestOptions.MaxSuffixLines}, MaxLines={requestOptions.MaxCompletionLines}, MaxChars={requestOptions.MaxCompletionCharacters}, MaxTokens={requestOptions.ProviderConfig.MaxTokens}, TimeoutMs={requestOptions.ProviderConfig.Timeout.TotalMilliseconds:F0}, State={State}");
            }

            SetState(CompletionState.RequestingCompletion);
            _logger.Info($"Provider selected. ProviderName={requestOptions.ProviderConfig.ProviderName}, ModelName={requestOptions.ProviderConfig.ModelName}, RequestId={requestId}, Source={requestOptions.Source}, CompletionMode={(isAutoCompletion ? CompletionMode.Auto : CompletionMode.Manual)}");
            var provider = _providerFactory.Create(requestOptions.ProviderConfig);
            var providerResponse = await provider.GenerateCompletionAsync(
                new CompletionProviderRequest
                {
                    Language = "C/C++",
                    FilePath = context.FilePath,
                    Prefix = context.Prefix,
                    Suffix = context.Suffix,
                    CurrentLinePrefix = context.CurrentLinePrefix,
                    CurrentLineSuffix = string.Empty,
                    SystemPrompt = requestOptions.SystemPrompt,
                    UserPrompt = requestOptions.UserPromptBuilder(context),
                    ModelName = requestOptions.ProviderConfig.ModelName,
                    BaseUrl = requestOptions.ProviderConfig.BaseUrl,
                    MaxTokens = requestOptions.ProviderConfig.MaxTokens,
                    Temperature = requestOptions.ProviderConfig.Temperature,
                    Timeout = requestOptions.ProviderConfig.Timeout,
                    RequestId = requestId,
                    Source = requestOptions.Source,
                    CompletionMode = isAutoCompletion ? CompletionMode.Auto : CompletionMode.Manual,
                    Config = requestOptions.ProviderConfig
                },
                cancellationToken);

            if (!providerResponse.IsSuccess)
            {
                SetState(CompletionState.Idle);
                _logger.Warning($"{(isAutoCompletion ? "Auto completion" : "Completion")} provider returned no completion. ProviderName={requestOptions.ProviderConfig.ProviderName}, ModelName={requestOptions.ProviderConfig.ModelName}, RequestId={requestId}, Source={requestOptions.Source}, Error={providerResponse.ErrorMessage}, LatencyMs={providerResponse.LatencyMs:F0}, State={State}");
                return;
            }

            var formatted = _formatter.Format(context, providerResponse.Text, requestOptions.MaxCompletionLines, requestOptions.MaxCompletionCharacters);
            if (string.IsNullOrWhiteSpace(formatted))
            {
                SetState(CompletionState.Idle);
                _logger.Warning($"{(isAutoCompletion ? "Auto completion" : "Completion")} response was empty after formatting. RequestId={requestId}, Source={requestOptions.Source}, LatencyMs={ElapsedMs(started):F0}, State={State}");
                return;
            }

            if (isAutoCompletion)
            {
                _cache.Set(cacheKey, formatted, _settingsManager.GetCacheTtl());
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            if (!ContextStillMatches(view, requestOptions.MaxPrefixLines, requestOptions.MaxSuffixLines, snapshot, cancellationToken))
            {
                _logger.Info($"Context changed, completion discarded. RequestId={requestId}, Source={requestOptions.Source}, LatencyMs={ElapsedMs(started):F0}, State={State}");
                SetState(CompletionState.Idle);
                return;
            }

            _logger.Info($"Completion formatted. RequestId={requestId}, Source={requestOptions.Source}, Lines={CountLines(formatted)}, Chars={formatted.Length}, LatencyMs={ElapsedMs(started):F0}, State={State}");
            if (GhostTextBroker.TryShow(view, formatted, requestId, requestOptions.Source))
            {
                _logger.Info($"GhostText shown. RequestId={requestId}, Source={requestOptions.Source}, Lines={CountLines(formatted)}, Chars={formatted.Length}, LatencyMs={ElapsedMs(started):F0}, State={State}");
                if (isAutoCompletion)
                {
                    _logger.Info($"Auto completion request succeeded. RequestId={requestId}, Source=Api, Lines={CountLines(formatted)}, Chars={formatted.Length}, LatencyMs={ElapsedMs(started):F0}, State={State}");
                }
                else
                {
                    _logger.Info($"Manual completion request succeeded. RequestId={requestId}, Source=Manual, Lines={CountLines(formatted)}, Chars={formatted.Length}, LatencyMs={ElapsedMs(started):F0}, State={State}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            SetState(CompletionState.Idle);
            _logger.Info($"{(isAutoCompletion ? "Auto completion" : "Completion")} canceled. RequestId={requestId}, Source={requestOptions.Source}, LatencyMs={ElapsedMs(started):F0}, State={State}");
        }
        catch (Exception ex)
        {
            SetState(CompletionState.Error);
            _logger.Error($"{(isAutoCompletion ? "Auto completion request failed" : "Completion request failed")}. RequestId={requestId}, Source={requestOptions.Source}, ErrorType={ex.GetType().Name}, LatencyMs={ElapsedMs(started):F0}, State={State}");
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

    private CompletionExecutionOptions CreateRequestOptions(DeepSeekOptionsPage options, bool isAutoCompletion)
    {
        if (isAutoCompletion)
        {
            return new CompletionExecutionOptions
            {
                Source = "Api",
                ProviderConfig = _settingsManager.GetAutoProviderConfig(),
                MaxPrefixLines = Math.Max(1, options.AutoMaxPrefixLines),
                MaxSuffixLines = Math.Max(0, options.AutoMaxSuffixLines),
                MaxCompletionLines = Math.Max(1, options.AutoMaxCompletionLines),
                MaxCompletionCharacters = Math.Max(1, options.AutoMaxCompletionCharacters),
                SystemPrompt = _promptBuilder.AutoSystemMessage,
                UserPromptBuilder = _promptBuilder.BuildAutoUserPrompt
            };
        }

        return new CompletionExecutionOptions
        {
            Source = "Manual",
            ProviderConfig = _settingsManager.GetManualProviderConfig(),
            MaxPrefixLines = Math.Max(1, options.MaxPrefixLines),
            MaxSuffixLines = Math.Max(0, options.MaxSuffixLines),
            MaxCompletionLines = 12,
            MaxCompletionCharacters = 1200,
            SystemPrompt = _promptBuilder.SystemMessage,
            UserPromptBuilder = _promptBuilder.BuildUserPrompt
        };
    }

    private static double ElapsedMs(DateTimeOffset started)
    {
        return (DateTimeOffset.UtcNow - started).TotalMilliseconds;
    }

    private bool ShouldSkipProvider(
        CompletionProviderConfig providerConfig,
        CompletionMode completionMode,
        long requestId,
        string source)
    {
        if (!providerConfig.IsConfigured)
        {
            _logger.Warning($"Provider skipped. Reason=ProviderNotConfigured, RequestId={requestId}, Source={source}, CompletionMode={completionMode}");
            _logger.Warning("Provider is not configured. Open re:code settings and select a provider.");
            return true;
        }

        if (!providerConfig.IsImplemented)
        {
            _logger.Warning($"Provider skipped. Reason=ProviderNotSupported, ProviderName={providerConfig.ProviderName}, RequestId={requestId}, Source={source}, CompletionMode={completionMode}");
            return true;
        }

        if (providerConfig.RequiresApiKey && string.IsNullOrWhiteSpace(providerConfig.ApiKey))
        {
            _logger.Warning($"Provider skipped. Reason=MissingApiKey, ProviderName={providerConfig.ProviderName}, CompletionMode={completionMode}, RequestId={requestId}, Source={source}");
            _logger.Warning("API key is not configured for the selected provider.");
            return true;
        }

        return false;
    }

    private sealed class CompletionExecutionOptions
    {
        public string Source { get; set; }

        public CompletionProviderConfig ProviderConfig { get; set; }

        public int MaxPrefixLines { get; set; }

        public int MaxSuffixLines { get; set; }

        public int MaxCompletionLines { get; set; }

        public int MaxCompletionCharacters { get; set; }

        public string SystemPrompt { get; set; }

        public Func<EditorContext, string> UserPromptBuilder { get; set; }
    }
}
