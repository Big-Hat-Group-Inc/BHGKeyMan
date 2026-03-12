using System.Runtime.Versioning;

namespace BHGKeyMan.Core.Tests;

[SupportedOSPlatform("windows")]
public class DpapiSecretCacheStoreTests : IDisposable
{
    private readonly string _tempDir;

    public DpapiSecretCacheStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "BHGKeyManTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void RoundTrip_SaveAndLoad_ReturnsIdenticalEntry()
    {
        var store = new DpapiSecretCacheStore(_tempDir);
        var original = new SecretEntry("test-secret", "secret-value",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(8), false);

        store.Save(original);
        var found = store.TryLoad("test-secret", out var loaded);

        Assert.True(found);
        Assert.Equal("test-secret", loaded.Name);
        Assert.Equal("secret-value", loaded.Value);
        Assert.True(loaded.FromCache);
    }

    [Fact]
    public void PerSecretIsolation_MultipleSecrets_IndependentlyStored()
    {
        var store = new DpapiSecretCacheStore(_tempDir);
        var entryA = new SecretEntry("secret-a", "value-a",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(8), false);
        var entryB = new SecretEntry("secret-b", "value-b",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(8), false);

        store.Save(entryA);
        store.Save(entryB);

        Assert.True(store.TryLoad("secret-a", out var loadedA));
        Assert.True(store.TryLoad("secret-b", out var loadedB));
        Assert.Equal("value-a", loadedA.Value);
        Assert.Equal("value-b", loadedB.Value);
    }

    [Fact]
    public void TryLoad_MissingFile_ReturnsFalse()
    {
        var store = new DpapiSecretCacheStore(_tempDir);

        var found = store.TryLoad("nonexistent", out _);

        Assert.False(found);
    }

    [Fact]
    public void TryLoad_ExpiredEntry_ReturnsFalseAndDeletesFile()
    {
        var store = new DpapiSecretCacheStore(_tempDir);
        var expired = new SecretEntry("expired-secret", "old-value",
            DateTimeOffset.UtcNow.AddHours(-2), DateTimeOffset.UtcNow.AddHours(-1), false);

        store.Save(expired);
        var found = store.TryLoad("expired-secret", out _);

        Assert.False(found);
        Assert.False(File.Exists(Path.Combine(_tempDir, "expired-secret.bin")));
    }

    [Fact]
    public void TryLoad_CorruptFile_ReturnsFalseAndDeletesFile()
    {
        var store = new DpapiSecretCacheStore(_tempDir);
        var filePath = Path.Combine(_tempDir, "corrupt-secret.bin");
        File.WriteAllBytes(filePath, new byte[] { 0x00, 0xFF, 0xDE, 0xAD });

        var found = store.TryLoad("corrupt-secret", out _);

        Assert.False(found);
        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public void Delete_RemovesFile()
    {
        var store = new DpapiSecretCacheStore(_tempDir);
        var entry = new SecretEntry("delete-me", "value",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(8), false);

        store.Save(entry);
        Assert.True(File.Exists(Path.Combine(_tempDir, "delete-me.bin")));

        store.Delete("delete-me");
        Assert.False(File.Exists(Path.Combine(_tempDir, "delete-me.bin")));
    }

    [Fact]
    public void Delete_NonexistentFile_DoesNotThrow()
    {
        var store = new DpapiSecretCacheStore(_tempDir);

        var ex = Record.Exception(() => store.Delete("nonexistent"));

        Assert.Null(ex);
    }
}
