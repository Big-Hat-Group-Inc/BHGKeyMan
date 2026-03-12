namespace BHGKeyMan.Core;

public interface ISecretStore
{
    Task<IReadOnlyList<string>> ListSecretNamesAsync(CancellationToken cancellationToken = default);
    Task<SecretEntry> GetSecretAsync(string secretName, CancellationToken cancellationToken = default);
    Task<OperationResult> SetSecretAsync(string secretName, string secretValue, CancellationToken cancellationToken = default);
}

public interface ISecretNameValidator
{
    bool IsValid(string? secretName);
}

public interface ISecretCache
{
    bool TryGet(string secretName, out SecretEntry entry);
    void Set(string secretName, string value, DateTimeOffset nowUtc);
    void Remove(string secretName);
}

public interface IEnvProcessLauncher
{
    OperationResult Launch(string executablePath, string arguments, IReadOnlyList<SecretEnvMapping> mappings, Func<string, string?> secretResolver);
}
