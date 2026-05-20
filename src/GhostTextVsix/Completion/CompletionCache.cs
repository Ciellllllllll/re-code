using System;
using System.Collections.Concurrent;

namespace GhostTextVsix.Completion;

internal sealed class CompletionCache
{
    private sealed class CacheEntry
    {
        public string Completion { get; set; }

        public DateTimeOffset ExpiresAt { get; set; }
    }

    private readonly ConcurrentDictionary<string, CacheEntry> _entries = new();

    public bool TryGet(string key, out string completion)
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

    public void Set(string key, string completion, TimeSpan ttl)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(completion))
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
