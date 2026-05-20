using GhostTextVsix.Diagnostics;

namespace GhostTextVsix.Completion.Providers;

internal sealed class CompletionProviderFactory
{
    private readonly DeepSeekOutputLogger _logger;

    public CompletionProviderFactory(DeepSeekOutputLogger logger)
    {
        _logger = logger;
    }

    public ICompletionProvider Create(CompletionProviderConfig config)
    {
        _logger.Info($"Provider selected. ProviderName={config.ProviderName}, ModelName={config.ModelName}, ProviderType={config.ProviderType}, IsLocal={config.IsLocal}");

        switch (config.ProviderType)
        {
            case CompletionProviderType.DeepSeek:
                return new DeepSeekCompletionProvider(config, _logger);
            case CompletionProviderType.OpenRouter:
                return new OpenRouterCompletionProvider(config, _logger);
            case CompletionProviderType.LocalOpenAICompatible:
                return new LocalOpenAICompatibleCompletionProvider(config, _logger);
            case CompletionProviderType.OpenAICompatible:
            default:
                return new OpenAICompatibleCompletionProvider(config, _logger);
        }
    }
}
