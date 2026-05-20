using System.Threading;
using System.Threading.Tasks;

namespace GhostTextVsix.Completion.Providers;

internal interface ICompletionProvider
{
    string ProviderName { get; }

    bool SupportsChatCompletions { get; }

    bool SupportsFimCompletions { get; }

    bool SupportsStreaming { get; }

    Task<CompletionProviderResponse> GenerateCompletionAsync(
        CompletionProviderRequest request,
        CancellationToken cancellationToken);
}
