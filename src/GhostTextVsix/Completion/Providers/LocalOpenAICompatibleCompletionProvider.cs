using GhostTextVsix.Diagnostics;

namespace GhostTextVsix.Completion.Providers;

internal sealed class LocalOpenAICompatibleCompletionProvider : OpenAICompatibleCompletionProvider
{
    public LocalOpenAICompatibleCompletionProvider(CompletionProviderConfig config, DeepSeekOutputLogger logger)
        : base(config, logger)
    {
    }
}
