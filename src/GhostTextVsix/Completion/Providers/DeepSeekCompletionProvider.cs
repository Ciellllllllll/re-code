using System;
using GhostTextVsix.Diagnostics;

namespace GhostTextVsix.Completion.Providers;

internal sealed class DeepSeekCompletionProvider : OpenAICompatibleCompletionProvider
{
    public DeepSeekCompletionProvider(CompletionProviderConfig config, DeepSeekOutputLogger logger)
        : base(config, logger)
    {
    }

    protected override Uri BuildRequestUri(CompletionProviderConfig config)
    {
        var configured = new Uri(config.BaseUrl.TrimEnd('/'), UriKind.Absolute);
        var builder = new UriBuilder(configured)
        {
            Path = NormalizeDeepSeekPath(configured.AbsolutePath),
            Query = string.Empty
        };

        return builder.Uri;
    }

    protected override ChatCompletionRequest CreateRequestBody(CompletionProviderRequest request)
    {
        var requestBody = base.CreateRequestBody(request);
        requestBody.Thinking = new ThinkingOptions
        {
            Type = "disabled"
        };
        requestBody.ResponseFormat = new ResponseFormatOptions
        {
            Type = "text"
        };

        return requestBody;
    }

    private static string NormalizeDeepSeekPath(string path)
    {
        var normalized = (path ?? string.Empty).TrimEnd('/');
        return normalized.Equals("/chat/completions", StringComparison.OrdinalIgnoreCase)
            ? normalized
            : "/chat/completions";
    }
}
