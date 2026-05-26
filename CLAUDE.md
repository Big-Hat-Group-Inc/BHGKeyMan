# CLAUDE.md

## Project Overview

BHGKeyMan is a WPF desktop application for securely managing AI/developer secrets via Azure Key Vault. It supports dual-mode operation: in-memory demo mode (default) and Azure Key Vault production mode. Built on .NET 10 with C#.

## Essential Commands

```bash
# Build the solution
dotnet build BHGKeyMan.slnx

# Run all tests
dotnet test BHGKeyMan.slnx

# Run the WPF app
dotnet run --project BHGKeyMan.App/BHGKeyMan.App.csproj
```

Always run `dotnet test BHGKeyMan.slnx` after making code changes.

## Solution Structure

```
BHGKeyMan.slnx              # Modern SLNx solution file
BHGKeyMan.App/               # WPF app (net10.0-windows)
  App.xaml.cs                # DI composition root, startup wiring
  MainWindow.xaml            # Single-window card-based UI
  MainViewModel.cs           # MVVM ViewModel with 7 async commands
  AzureKeyVaultSecretStore.cs # Azure Key Vault ISecretStore impl
  AppErrorMapper.cs          # Exception-to-user-message translation
  appsettings.json           # App configuration
BHGKeyMan.Core/              # Domain library (net10.0)
  Interfaces.cs              # ISecretStore, ISecretNameValidator, ISecretCache, IEnvProcessLauncher
  Models.cs                  # AppConfig, SecretEntry, OperationResult, SecretEnvMapping, AuthMode
  InMemorySecretStore.cs     # Demo/test ISecretStore implementation
  CachedSecretStore.cs       # Decorator wrapping store + memory cache + optional DPAPI
  MemorySecretCache.cs       # TTL-based ISecretCache using IMemoryCache
  DpapiSecretCacheStore.cs   # Windows DPAPI encrypted file-based cache
  EnvProcessLauncher.cs      # Secure subprocess launcher with env var injection
  SecretNameValidator.cs     # Regex-based Key Vault name validation
BHGKeyMan.Core.Tests/        # xUnit tests (net10.0-windows)
scripts/                     # 7 PowerShell Azure provisioning scripts (01-07)
```

## Architecture

- **Pattern**: MVVM with manual DI composition in `App.xaml.cs`
- **Core abstractions**: `ISecretStore`, `ISecretNameValidator`, `ISecretCache`, `IEnvProcessLauncher`
- **Decorator pattern**: `CachedSecretStore` wraps any `ISecretStore` with memory + optional DPAPI cache layers
- **Data models**: Sealed records for immutability (`AppConfig`, `SecretEntry`, `OperationResult`)
- **Commands**: `AsyncRelayCommand` / `RelayCommand` implementing `ICommand`
- **Config validation**: `AppConfig.Validate()` returns `List<string>` of errors, checked at startup

## Coding Conventions

- **Nullable**: Enabled globally. Respect nullable annotations.
- **Implicit usings**: Enabled. Don't add redundant `using System;` etc.
- **Classes**: Use `sealed` on classes not designed for inheritance.
- **Data models**: Use `sealed record` for immutable data structures.
- **Fields**: Private fields use `_camelCase`.
- **Secret names**: Lowercase with hyphens (e.g., `openai-api-key`). Validated by regex `^[A-Za-z][0-9A-Za-z-]{0,126}$`.
- **Environment variables**: `UPPERCASE_WITH_UNDERSCORES` (e.g., `OPENAI_API_KEY`).
- **No hardcoded secrets**: All secrets flow through configuration or Key Vault.
- **No secret logging**: Never log secret values.
- **Platform attributes**: Use `[SupportedOSPlatform("windows")]` on Windows-only code (e.g., DPAPI).
- **One class per file** for service implementations; `Interfaces.cs` and `Models.cs` are multi-type files.

## Testing

- Framework: xUnit 2.9.3 with coverlet
- Test project references both Core and App projects
- Tests cover: validation, in-memory store, caching, DPAPI store, process launcher, config validation, async commands
- Add tests for any new public behavior

## Key Constraints

- DPAPI is Windows-only (`System.Security.Cryptography.ProtectedData`)
- WPF app uses interactive browser auth only for Azure mode
- `UseInMemoryDemoStore: true` (default) bypasses Azure entirely
- `EnvProcessLauncher` enforces allowlists for both executable paths and environment variable names
- Blocked env vars: `PATH`, `COMSPEC`, `PATHEXT`, `PSMODULEPATH`, `DOTNET_ROOT`, and `DOTNET_*` prefix
- Process launch requires `EnableProcessLaunch: true` plus populated allowlists in config

## Configuration

Config file: `BHGKeyMan.App/appsettings.json` under the `KeyVault` section. Environment variable expansion is supported in paths (e.g., `%LOCALAPPDATA%`). In demo mode, placeholder GUIDs are accepted; in Azure mode, valid GUIDs are required for `TenantId` and `ClientId`.

## Related Documentation

- `spec.md` - Full implementation specification with requirements (FR/SR/NFR)
- `ApplicationResearch.md` - Architecture rationale and security research
- `AGENTS.md` - Complementary AI assistant guidance
- `README.md` - User-facing build/run/config docs
