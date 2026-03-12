namespace BHGKeyMan.Core;

public sealed record AppConfig(
    Uri KeyVaultUri,
    string TenantId,
    string ClientId,
    TimeSpan SecretCacheTtl,
    bool EnableDpapiCache,
    int MaxRetryCount,
    string? DpapiCachePath = null,
    bool UseInMemoryDemoStore = true,
    int ClipboardClearTimeoutSeconds = 30,
    int SecretRevealTimeoutSeconds = 30,
    bool EnableProcessLaunch = false,
    IReadOnlyList<string>? AllowedExecutablePaths = null,
    IReadOnlyList<string>? AllowedEnvironmentVariableNames = null)
{
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (SecretCacheTtl <= TimeSpan.Zero)
            errors.Add("SecretCacheTtlHours must be greater than 0.");

        if (MaxRetryCount < 0)
            errors.Add("MaxRetryCount must be 0 or greater.");

        if (ClipboardClearTimeoutSeconds <= 0)
            errors.Add("ClipboardClearTimeoutSeconds must be greater than 0.");

        if (SecretRevealTimeoutSeconds <= 0)
            errors.Add("SecretRevealTimeoutSeconds must be greater than 0.");

        if (!UseInMemoryDemoStore)
        {
            if (string.IsNullOrWhiteSpace(TenantId) ||
                TenantId == "00000000-0000-0000-0000-000000000000" ||
                !Guid.TryParse(TenantId, out _))
                errors.Add("TenantId must be a valid GUID when not in demo mode.");

            if (string.IsNullOrWhiteSpace(ClientId) ||
                ClientId == "11111111-1111-1111-1111-111111111111" ||
                !Guid.TryParse(ClientId, out _))
                errors.Add("ClientId must be a valid GUID when not in demo mode.");

            if (KeyVaultUri.Scheme != "https")
                errors.Add("KeyVaultUri must use HTTPS.");

            if (KeyVaultUri.Host == "example-kv.vault.azure.net")
                errors.Add("KeyVaultUri must be updated from the placeholder value.");
        }

        if (EnableProcessLaunch)
        {
            if (AllowedExecutablePaths is null || AllowedExecutablePaths.Count == 0)
                errors.Add("AllowedExecutablePaths must contain at least one entry when process launch is enabled.");

            if (AllowedEnvironmentVariableNames is null || AllowedEnvironmentVariableNames.Count == 0)
                errors.Add("AllowedEnvironmentVariableNames must contain at least one entry when process launch is enabled.");
        }

        return errors;
    }
}

public sealed record SecretEntry(
    string Name,
    string Value,
    DateTimeOffset RetrievedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    bool FromCache);

public sealed record OperationResult(bool Success, string Message);

public sealed record SecretEnvMapping(string SecretName, string EnvironmentVariableName);

public enum AuthMode
{
    InteractiveUser,
    ServicePrincipal,
    ManagedIdentity
}
