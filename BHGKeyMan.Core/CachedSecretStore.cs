namespace BHGKeyMan.Core;

public sealed class CachedSecretStore : ISecretStore
{
    private readonly ISecretStore _inner;
    private readonly ISecretCache _cache;
    private readonly DpapiSecretCacheStore? _dpapiStore;
    private readonly TimeSpan _cacheTtl;

    public CachedSecretStore(ISecretStore inner, ISecretCache cache, AppConfig config)
    {
        _inner = inner;
        _cache = cache;
        _cacheTtl = config.SecretCacheTtl;

        if (config.EnableDpapiCache && !string.IsNullOrWhiteSpace(config.DpapiCachePath) && OperatingSystem.IsWindows())
        {
            _dpapiStore = new DpapiSecretCacheStore(config.DpapiCachePath);
        }
    }

    public Task<IReadOnlyList<string>> ListSecretNamesAsync(CancellationToken cancellationToken = default)
    {
        return _inner.ListSecretNamesAsync(cancellationToken);
    }

    public async Task<SecretEntry> GetSecretAsync(string secretName, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGet(secretName, out var entry))
        {
            return entry;
        }

        if (_dpapiStore is not null && OperatingSystem.IsWindows() && _dpapiStore.TryLoad(secretName, out var dpapiEntry))
        {
            _cache.Set(secretName, dpapiEntry.Value, DateTimeOffset.UtcNow);
            return dpapiEntry with { FromCache = true };
        }

        var result = await _inner.GetSecretAsync(secretName, cancellationToken);
        _cache.Set(secretName, result.Value, DateTimeOffset.UtcNow);

        if (_dpapiStore is not null && OperatingSystem.IsWindows())
        {
            var persistEntry = result with { ExpiresAtUtc = DateTimeOffset.UtcNow.Add(_cacheTtl) };
            _dpapiStore.Save(persistEntry);
        }

        return result;
    }

    public async Task<OperationResult> SetSecretAsync(string secretName, string secretValue, CancellationToken cancellationToken = default)
    {
        var result = await _inner.SetSecretAsync(secretName, secretValue, cancellationToken);
        if (result.Success)
        {
            _cache.Remove(secretName);
            if (_dpapiStore is not null && OperatingSystem.IsWindows())
            {
                _dpapiStore.Delete(secretName);
            }
        }

        return result;
    }
}
