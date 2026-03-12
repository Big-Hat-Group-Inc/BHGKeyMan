using Microsoft.Extensions.Caching.Memory;

namespace BHGKeyMan.Core;

public sealed class MemorySecretCache : ISecretCache
{
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _ttl;

    public MemorySecretCache(TimeSpan ttl)
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _ttl = ttl;
    }

    public bool TryGet(string secretName, out SecretEntry entry)
    {
        if (_cache.TryGetValue<SecretEntry>(secretName, out var cached) && cached is not null)
        {
            entry = cached with { FromCache = true };
            return true;
        }

        entry = null!;
        return false;
    }

    public void Set(string secretName, string value, DateTimeOffset nowUtc)
    {
        var secretEntry = new SecretEntry(secretName, value, nowUtc, nowUtc.Add(_ttl), false);
        _cache.Set(secretName, secretEntry, secretEntry.ExpiresAtUtc);
    }

    public void Remove(string secretName)
    {
        _cache.Remove(secretName);
    }
}
