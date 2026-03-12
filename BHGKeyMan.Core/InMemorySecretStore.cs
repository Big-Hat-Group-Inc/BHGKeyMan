namespace BHGKeyMan.Core;

public sealed class InMemorySecretStore : ISecretStore
{
    private readonly Dictionary<string, string> _secrets;
    private readonly ISecretNameValidator _validator;
    private readonly ISecretCache _cache;
    private readonly TimeSpan _cacheTtl;

    public InMemorySecretStore(ISecretNameValidator validator, ISecretCache cache, TimeSpan cacheTtl)
    {
        _validator = validator;
        _cache = cache;
        _cacheTtl = cacheTtl;
        _secrets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public Task<IReadOnlyList<string>> ListSecretNamesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<string> names = _secrets.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        return Task.FromResult(names);
    }

    public Task<SecretEntry> GetSecretAsync(string secretName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_validator.IsValid(secretName))
        {
            throw new ArgumentException("Secret name is invalid.", nameof(secretName));
        }

        if (_cache.TryGet(secretName, out var cached))
        {
            return Task.FromResult(cached);
        }

        if (!_secrets.TryGetValue(secretName, out var value))
        {
            throw new KeyNotFoundException($"Secret '{secretName}' was not found.");
        }

        var nowUtc = DateTimeOffset.UtcNow;
        _cache.Set(secretName, value, nowUtc);
        var result = new SecretEntry(secretName, value, nowUtc, nowUtc.Add(_cacheTtl), false);
        return Task.FromResult(result);
    }

    public Task<OperationResult> SetSecretAsync(string secretName, string secretValue, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_validator.IsValid(secretName))
        {
            return Task.FromResult(new OperationResult(false, "Secret name is invalid. Secret names must start with a letter and contain only letters, numbers, and hyphens."));
        }

        if (string.IsNullOrWhiteSpace(secretValue))
        {
            return Task.FromResult(new OperationResult(false, "Secret value is required."));
        }

        _secrets[secretName] = secretValue;
        _cache.Remove(secretName);
        return Task.FromResult(new OperationResult(true, "Secret set successfully. If this were Azure Key Vault, this operation would create a new version for existing names."));
    }
}
