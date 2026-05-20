using System.Threading;
using System.Threading.Tasks;

namespace GhostTextVsix.Completion.Providers;

internal sealed class UnsupportedCompletionProvider : ICompletionProvider
{
    private readonly CompletionProviderConfig _config;

    public UnsupportedCompletionProvider(CompletionProviderConfig config)
    {
        _config = config;
    }

    public string ProviderName => _config.ProviderName;

    public bool SupportsChatCompletions => false;

    public bool SupportsFimCompletions => false;

    public bool SupportsStreaming => false;

    public Task<CompletionProviderResponse> GenerateCompletionAsync(
        CompletionProviderRequest request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new CompletionProviderResponse
        {
            ProviderName = ProviderName,
            ModelName = request.ModelName,
            RequestId = request.RequestId,
            Source = request.Source,
            ErrorMessage = "Provider is not supported."
        });
    }
}
