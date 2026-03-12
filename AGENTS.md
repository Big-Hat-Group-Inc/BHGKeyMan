# AGENTS.md

## Repository status and purpose
- Project name: `BHGKeyMan`.
- Goal: securely manage secrets for AI-enabled developer workflows.
- This repository now contains an executable .NET solution with a WPF app, core library, tests, and operational scripts.

## Current file layout
- `BHGKeyMan.slnx` - solution root.
- `BHGKeyMan.App/` - WPF desktop app.
- `BHGKeyMan.Core/` - domain models, interfaces, validation, cache, process launch utilities.
- `BHGKeyMan.Core.Tests/` - xUnit unit tests.
- `scripts/` - PowerShell provisioning/operations scripts for Azure Key Vault setup.
- `spec.md` - implementation specification.
- `openspec-proposal.md` - OpenSpec proposal.
- `ApplicationResearch.md` - background research and architecture rationale.

## Existing rule/instruction files
No additional in-repo assistant rule files were found:
- `.cursor/rules/*.md`
- `.cursorrules`
- `.github/copilot-instructions.md`
- `claude.md`
- `agents.md`

## Project type and stack
- Language/runtime: C# / .NET 10.
- App type: WPF (`net10.0-windows`).
- Test framework: xUnit.
- Azure SDK usage:
  - `Azure.Identity`
  - `Azure.Security.KeyVault.Secrets`

## Essential commands
Run from repo root:

```powershell
# restore + build
dotnet build BHGKeyMan.slnx

# run tests
dotnet test BHGKeyMan.slnx

# run desktop app
dotnet run --project .\BHGKeyMan.App\BHGKeyMan.App.csproj
```

## Application configuration
Config file: `BHGKeyMan.App/appsettings.json`

Important keys:
- `KeyVaultUri`
- `TenantId`
- `ClientId`
- `SecretCacheTtlHours`
- `EnableDpapiCache`
- `DpapiCachePath`
- `MaxRetryCount`
- `UseInMemoryDemoStore`

Behavior:
- `UseInMemoryDemoStore: true` => local demo mode (no Azure calls).
- `UseInMemoryDemoStore: false` => Azure Key Vault mode with interactive browser auth.

## Code organization and patterns
### Core abstractions (`BHGKeyMan.Core`)
- `ISecretStore`: list/get/set secret operations.
- `ISecretNameValidator`: validates Key Vault-compatible secret names.
- `ISecretCache`: cache abstraction.
- `IEnvProcessLauncher`: launches process with process-scoped env vars.

### Implementations
- `SecretNameValidator` enforces `^[A-Za-z][0-9A-Za-z-]{0,126}$`.
- `MemorySecretCache` stores `SecretEntry` values with TTL.
- `DpapiSecretCacheStore` provides optional local encrypted cache (Windows DPAPI).
- `InMemorySecretStore` is demo/dev store and demonstrates list/get/set behavior.
- `EnvProcessLauncher` handles process startup with env var mappings.

### App layer (`BHGKeyMan.App`)
- MVVM style with `MainViewModel` + commands.
- `AzureKeyVaultSecretStore` implements real Key Vault interactions.
- `CachedSecretStore` wraps a store with memory cache and optional DPAPI persistence.
- Startup wiring in `App.xaml.cs` chooses in-memory demo mode or Azure mode from config.

## Testing approach
- Unit tests in `BHGKeyMan.Core.Tests/UnitTest1.cs` cover:
  - Secret name validation rules.
  - Set/get behavior in in-memory store.
  - Cache-hit behavior on repeated gets.
  - Invalid-name handling.
  - Env process launcher failure behavior for missing executable.

## Operational scripts (`scripts/`)
- `01-prereqs-check.ps1` - validate az CLI/login/subscription/tenant.
- `02-provision-keyvault.ps1` - provision Key Vault + baseline hardening.
- `03-configure-network.ps1` - private endpoint mode or firewall mode.
- `04-assign-rbac.ps1` - assign Key Vault RBAC roles by persona.
- `05-seed-secrets.ps1` - seed secrets from JSON.
- `06-verify-access.ps1` - verify list/set/get access path.
- `07-enable-diagnostics.ps1` - enable Key Vault diagnostic settings.

## Naming, style, and conventions observed
- Prefer explicit immutable records for data models (`record`).
- Use clear service interfaces for separable concerns.
- Keep secret names lowercase-hyphen style in examples.
- Avoid logging secret values.
- Prefer process-scoped environment variables over persistent env var writes.

## Gotchas and non-obvious constraints
- DPAPI is Windows-only (`SupportedOSPlatform("windows")`).
- WPF app currently uses interactive auth only for Azure mode.
- Repo includes both demo mode (in-memory store) and production-oriented Azure store; verify `UseInMemoryDemoStore` before testing behavior.
- `scripts/03-configure-network.ps1` supports two mutually exclusive modes: `-UsePrivateEndpoint` or `-UseFirewallMode`.

## Guidance for future agents
1. Preserve least-privilege and non-plaintext-secret defaults.
2. Keep production and demo paths both functional.
3. If adding features, update:
   - `spec.md` (if behavior changes materially),
   - `README.md` command/config docs,
   - and this `AGENTS.md` with any new commands or patterns.
4. Run `dotnet test BHGKeyMan.slnx` after each significant code change.
