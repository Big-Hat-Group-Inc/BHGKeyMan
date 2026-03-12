# BHGKeyMan

Manage secrets more easily for AI enabled workers.

## What is implemented
- WPF desktop app (`BHGKeyMan.App`) for interactive secret workflows.
- Core domain/services library (`BHGKeyMan.Core`) with:
  - Secret name validation using Key Vault-compatible naming constraints.
  - In-memory cache for secret retrievals.
  - Optional DPAPI local encrypted cache support.
  - Process launcher for process-scoped environment variable injection.
- Azure Key Vault integration service in app layer (`AzureKeyVaultSecretStore`).
- In-memory demo secret store for local development without Azure dependencies.
- Provisioning and operations PowerShell scripts under `scripts/`.
- Unit tests (`BHGKeyMan.Core.Tests`).

## Repository layout
- `BHGKeyMan.slnx` - solution file.
- `BHGKeyMan.App/` - WPF application.
- `BHGKeyMan.Core/` - domain/services and interfaces.
- `BHGKeyMan.Core.Tests/` - xUnit tests.
- `scripts/` - Azure provisioning and verification scripts.
- `spec.md` - implementation specification.
- `openspec-proposal.md` - proposal document.
- `ApplicationResearch.md` - source research.

## Build and run
```powershell
# restore + build
dotnet build BHGKeyMan.slnx

# run tests
dotnet test BHGKeyMan.slnx

# run desktop app
dotnet run --project .\BHGKeyMan.App\BHGKeyMan.App.csproj
```

## App configuration
Configuration file: `BHGKeyMan.App/appsettings.json`

Key settings:
- `KeyVaultUri`
- `TenantId`
- `ClientId`
- `SecretCacheTtlHours`
- `EnableDpapiCache`
- `DpapiCachePath`
- `MaxRetryCount`
- `UseInMemoryDemoStore`

### Local demo mode (default)
`UseInMemoryDemoStore: true`
- No Azure sign-in required.
- Useful for UI and workflow validation.

### Azure mode
Set `UseInMemoryDemoStore: false` and provide valid Key Vault/Entra values.
- Uses interactive browser auth.
- Requires Key Vault RBAC role assignments for your identity.

## Provisioning scripts
Scripts are in `scripts/` and intended to be run by operators.

1. `01-prereqs-check.ps1` - validates Azure CLI/login/subscription/tenant context.
2. `02-provision-keyvault.ps1` - creates resource group and Key Vault with RBAC + safety controls.
3. `03-configure-network.ps1` - configures private endpoint mode or firewall mode.
4. `04-assign-rbac.ps1` - assigns RBAC role by persona.
5. `05-seed-secrets.ps1` - seeds secrets from JSON input.
6. `06-verify-access.ps1` - verifies list/set/get behavior.
7. `07-enable-diagnostics.ps1` - enables Key Vault diagnostic settings.

## Security notes
- Secrets are not persisted in plaintext by default.
- Process launch uses process-scoped environment variable injection.
- Secret values should not be logged.
- Use private endpoint mode for strongest Key Vault network posture where possible.
