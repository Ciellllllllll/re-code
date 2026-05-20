using System.Net.Http;
using GhostTextVsix.Diagnostics;

namespace GhostTextVsix.Completion.Providers;

internal sealed class OpenRouterCompletionProvider : OpenAICompatibleCompletionProvider
{
    public OpenRouterCompletionProvider(CompletionProviderConfig config, DeepSeekOutputLogger logger)
        : base(config, logger)
    {
    }

    protected override void AddProviderHeaders(HttpRequestMessage request)
    {
        request.Headers.TryAddWithoutValidation("X-Title", "GhostText VSIX");
    }
}
