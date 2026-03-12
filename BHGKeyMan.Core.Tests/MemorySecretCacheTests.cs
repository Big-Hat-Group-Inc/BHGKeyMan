namespace BHGKeyMan.Core.Tests;

public class MemorySecretCacheTests
{
    [Fact]
    public void TryGet_WithinTtl_ReturnsCachedEntry()
    {
        var cache = new MemorySecretCache(TimeSpan.FromHours(1));

        cache.Set("test-key", "test-value", DateTimeOffset.UtcNow);
        var found = cache.TryGet("test-key", out var entry);

        Assert.True(found);
        Assert.Equal("test-value", entry.Value);
        Assert.True(entry.FromCache);
    }

    [Fact]
    public void TryGet_MissingKey_ReturnsFalse()
    {
        var cache = new MemorySecretCache(TimeSpan.FromHours(1));

        var found = cache.TryGet("nonexistent", out _);

        Assert.False(found);
    }

    [Fact]
    public void Remove_InvalidatesEntry()
    {
        var cache = new MemorySecretCache(TimeSpan.FromHours(1));

        cache.Set("remove-test", "value", DateTimeOffset.UtcNow);
        Assert.True(cache.TryGet("remove-test", out _));

        cache.Remove("remove-test");
        Assert.False(cache.TryGet("remove-test", out _));
    }

    [Fact]
    public void Set_StoresEntryWithCorrectExpiry()
    {
        var ttl = TimeSpan.FromHours(4);
        var cache = new MemorySecretCache(ttl);
        var now = DateTimeOffset.UtcNow;

        cache.Set("expiry-test", "value", now);
        cache.TryGet("expiry-test", out var entry);

        Assert.Equal(now.Add(ttl), entry.ExpiresAtUtc);
    }

    [Fact]
    public void MultipleKeys_StoredIndependently()
    {
        var cache = new MemorySecretCache(TimeSpan.FromHours(1));

        cache.Set("key-a", "value-a", DateTimeOffset.UtcNow);
        cache.Set("key-b", "value-b", DateTimeOffset.UtcNow);

        Assert.True(cache.TryGet("key-a", out var a));
        Assert.True(cache.TryGet("key-b", out var b));
        Assert.Equal("value-a", a.Value);
        Assert.Equal("value-b", b.Value);
    }
}
