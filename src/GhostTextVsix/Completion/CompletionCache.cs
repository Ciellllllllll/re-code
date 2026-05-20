using System;
using System.Collections.Concurrent;

namespace GhostTextVsix.Completion;

internal sealed class CompletionCache
{
    private sealed class CacheEntry
    {
        public NormalizedCompletion Completion { get; set; }

        public DateTimeOffset ExpiresAt { get; set; }
    }

    private readonly ConcurrentDictionary<string, CacheEntry> _entries = new();

    public bool TryGet(string key, out NormalizedCompletion completion)
    {
        completion = null;
        if (!_entries.TryGetValue(key, out var entry))
        {
            return false;
        }

        if (entry.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _entries.TryRemove(key, out _);
            return false;
        }

        completion = entry.Completion;
        return true;
    }

    public void Set(string key, NormalizedCompletion completion, TimeSpan ttl)
    {
        if (string.IsNullOrWhiteSpace(key) || completion == null || string.IsNullOrWhiteSpace(completion.CommitText))
        {
            return;
        }

        _entries[key] = new CacheEntry
        {
            Completion = completion,
            ExpiresAt = DateTimeOffset.UtcNow.Add(ttl)
        };
    }
}
