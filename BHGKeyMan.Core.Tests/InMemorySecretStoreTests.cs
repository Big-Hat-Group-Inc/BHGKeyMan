namespace BHGKeyMan.Core.Tests;

public class InMemorySecretStoreTests
{
    private static InMemorySecretStore CreateStore(TimeSpan? ttl = null)
    {
        var cacheTtl = ttl ?? TimeSpan.FromHours(8);
        return new InMemorySecretStore(new SecretNameValidator(), new MemorySecretCache(cacheTtl), cacheTtl);
    }

    [Fact]
    public async Task SetAndGet_ReturnsValue()
    {
        var store = CreateStore();

        var setResult = await store.SetSecretAsync("openai-api-key", "secret-value");
        var entry = await store.GetSecretAsync("openai-api-key");

        Assert.True(setResult.Success);
        Assert.Equal("secret-value", entry.Value);
        Assert.False(entry.FromCache);
    }

    [Fact]
    public async Task SecondGet_UsesCache()
    {
        var store = CreateStore();

        await store.SetSecretAsync("anthropic-api-key", "abc");
        _ = await store.GetSecretAsync("anthropic-api-key");
        var second = await store.GetSecretAsync("anthropic-api-key");

        Assert.True(second.FromCache);
        Assert.Equal("abc", second.Value);
    }

    [Fact]
    public async Task InvalidName_ReturnsFailureOnSet()
    {
        var store = CreateStore();

        var result = await store.SetSecretAsync("bad_name", "value");

        Assert.False(result.Success);
    }

    [Fact]
    public async Task InvalidName_ThrowsOnGet()
    {
        var store = CreateStore();

        await Assert.ThrowsAsync<ArgumentException>(() => store.GetSecretAsync("bad_name"));
    }

    [Fact]
    public async Task GetMissing_ThrowsKeyNotFound()
    {
        var store = CreateStore();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => store.GetSecretAsync("no-such-key"));
    }

    [Fact]
    public async Task SetSecretAsync_InvalidatesCache()
    {
        var store = CreateStore();

        await store.SetSecretAsync("my-key", "v1");
        _ = await store.GetSecretAsync("my-key");
        var cached = await store.GetSecretAsync("my-key");
        Assert.True(cached.FromCache);

        await store.SetSecretAsync("my-key", "v2");
        var afterSet = await store.GetSecretAsync("my-key");

        Assert.False(afterSet.FromCache);
        Assert.Equal("v2", afterSet.Value);
    }

    [Fact]
    public async Task GetSecretAsync_UsesConfiguredTtl()
    {
        var ttl = TimeSpan.FromHours(2);
        var store = CreateStore(ttl);

        await store.SetSecretAsync("ttl-test", "value");
        var entry = await store.GetSecretAsync("ttl-test");

        var expectedExpiry = entry.RetrievedAtUtc.Add(ttl);
        Assert.Equal(expectedExpiry, entry.ExpiresAtUtc);
    }

    [Fact]
    public async Task EmptyValue_ReturnsFailure()
    {
        var store = CreateStore();

        var result = await store.SetSecretAsync("valid-name", "");

        Assert.False(result.Success);
    }
}
