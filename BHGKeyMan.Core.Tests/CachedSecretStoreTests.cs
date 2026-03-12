namespace BHGKeyMan.Core.Tests;

public class CachedSecretStoreTests
{
    private static AppConfig CreateConfig(bool enableDpapi = false, string? dpapiPath = null)
    {
        return new AppConfig(
            new Uri("https://example.vault.azure.net/"),
            "tenant", "client",
            TimeSpan.FromHours(2),
            enableDpapi,
            3,
            dpapiPath,
            true);
    }

    [Fact]
    public async Task CacheMiss_FetchesFromInnerStore()
    {
        var inner = new FakeSecretStore();
        inner.Entries["my-secret"] = "inner-value";
        var cache = new MemorySecretCache(TimeSpan.FromHours(2));
        var store = new CachedSecretStore(inner, cache, CreateConfig());

        var entry = await store.GetSecretAsync("my-secret");

        Assert.Equal("inner-value", entry.Value);
        Assert.False(entry.FromCache);
        Assert.Equal(1, inner.GetCallCount);
    }

    [Fact]
    public async Task CacheHit_ReturnsFromMemoryCache()
    {
        var inner = new FakeSecretStore();
        inner.Entries["my-secret"] = "inner-value";
        var cache = new MemorySecretCache(TimeSpan.FromHours(2));
        var store = new CachedSecretStore(inner, cache, CreateConfig());

        _ = await store.GetSecretAsync("my-secret");
        var second = await store.GetSecretAsync("my-secret");

        Assert.True(second.FromCache);
        Assert.Equal("inner-value", second.Value);
        Assert.Equal(1, inner.GetCallCount);
    }

    [Fact]
    public async Task SetSecretAsync_InvalidatesMemoryCache()
    {
        var inner = new FakeSecretStore();
        inner.Entries["my-secret"] = "v1";
        var cache = new MemorySecretCache(TimeSpan.FromHours(2));
        var store = new CachedSecretStore(inner, cache, CreateConfig());

        _ = await store.GetSecretAsync("my-secret");
        var cached = await store.GetSecretAsync("my-secret");
        Assert.True(cached.FromCache);

        await store.SetSecretAsync("my-secret", "v2");
        inner.Entries["my-secret"] = "v2";

        var afterSet = await store.GetSecretAsync("my-secret");
        Assert.False(afterSet.FromCache);
        Assert.Equal("v2", afterSet.Value);
    }

    [Fact]
    public async Task ListSecretNamesAsync_DelegatesToInner()
    {
        var inner = new FakeSecretStore();
        inner.Entries["alpha"] = "a";
        inner.Entries["beta"] = "b";
        var cache = new MemorySecretCache(TimeSpan.FromHours(2));
        var store = new CachedSecretStore(inner, cache, CreateConfig());

        var names = await store.ListSecretNamesAsync();

        Assert.Equal(2, names.Count);
    }

    private sealed class FakeSecretStore : ISecretStore
    {
        public Dictionary<string, string> Entries { get; } = new(StringComparer.OrdinalIgnoreCase);
        public int GetCallCount { get; private set; }

        public Task<IReadOnlyList<string>> ListSecretNamesAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<string> names = Entries.Keys.OrderBy(x => x).ToList();
            return Task.FromResult(names);
        }

        public Task<SecretEntry> GetSecretAsync(string secretName, CancellationToken cancellationToken = default)
        {
            GetCallCount++;
            if (!Entries.TryGetValue(secretName, out var value))
                throw new KeyNotFoundException(secretName);
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new SecretEntry(secretName, value, now, now.AddHours(8), false));
        }

        public Task<OperationResult> SetSecretAsync(string secretName, string secretValue, CancellationToken cancellationToken = default)
        {
            Entries[secretName] = secretValue;
            return Task.FromResult(new OperationResult(true, "OK"));
        }
    }
}
