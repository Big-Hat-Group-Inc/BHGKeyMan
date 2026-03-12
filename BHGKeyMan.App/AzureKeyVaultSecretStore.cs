using Azure;
using Azure.Core;
using Azure.Security.KeyVault.Secrets;
using BHGKeyMan.Core;

namespace BHGKeyMan.App;

public sealed class AzureKeyVaultSecretStore : ISecretStore
{
    private readonly SecretClient _client;
    private readonly ISecretNameValidator _validator;
    private readonly TimeSpan _cacheTtl;

    public AzureKeyVaultSecretStore(AppConfig config, ISecretNameValidator validator, TokenCredential credential)
    {
        _validator = validator;
        _cacheTtl = config.SecretCacheTtl;
        var options = new SecretClientOptions
        {
            Retry =
            {
                Mode = RetryMode.Exponential,
                MaxRetries = config.MaxRetryCount
            }
        };

        _client = new SecretClient(config.KeyVaultUri, credential, options);
    }

    public async Task<IReadOnlyList<string>> ListSecretNamesAsync(CancellationToken cancellationToken = default)
    {
        var names = new List<string>();
        await foreach (var secret in _client.GetPropertiesOfSecretsAsync(cancellationToken))
        {
            names.Add(secret.Name);
        }

        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names;
    }

    public async Task<SecretEntry> GetSecretAsync(string secretName, CancellationToken cancellationToken = default)
    {
        ValidateName(secretName);

        try
        {
            var secret = await _client.GetSecretAsync(secretName, cancellationToken: cancellationToken);
            var nowUtc = DateTimeOffset.UtcNow;
            return new SecretEntry(secretName, secret.Value.Value, nowUtc, nowUtc.Add(_cacheTtl), false);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new KeyNotFoundException($"Secret '{secretName}' was not found.", ex);
        }
        catch (RequestFailedException ex) when (ex.Status == 403)
        {
            throw new UnauthorizedAccessException($"Not authorized to read secret '{secretName}'.", ex);
        }
    }

    public async Task<OperationResult> SetSecretAsync(string secretName, string secretValue, CancellationToken cancellationToken = default)
    {
        ValidateName(secretName);

        if (string.IsNullOrWhiteSpace(secretValue))
        {
            return new OperationResult(false, "Secret value is required.");
        }

        try
        {
            await _client.SetSecretAsync(secretName, secretValue, cancellationToken);
            return new OperationResult(true, "Secret set successfully. Existing names receive a new secret version.");
        }
        catch (RequestFailedException ex) when (ex.Status == 403)
        {
            return new OperationResult(false, "Not authorized to set secrets. Assign Key Vault Secrets Officer role.");
        }
    }

    private void ValidateName(string secretName)
    {
        if (!_validator.IsValid(secretName))
        {
            throw new ArgumentException("Secret name is invalid.", nameof(secretName));
        }
    }
}
