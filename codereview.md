# Code Review

**Reviewed:** 2026-03-11
**Build:** ✅ `dotnet build BHGKeyMan.slnx` — 0 warnings, 0 errors
**Tests:** ✅ 9/9 passed (285ms)
**Reviewer:** BHG-Bot (automated review)

## Scope

Reviewed all source files across the solution: `BHGKeyMan.App` (WPF desktop), `BHGKeyMan.Core` (domain library), `BHGKeyMan.Core.Tests` (xUnit), `scripts/` (7 PowerShell provisioning scripts), project files, and configuration.

## What Is Working Well

- **Clean layered architecture.** Core abstractions (`ISecretStore`, `ISecretCache`, `IEnvProcessLauncher`, `ISecretNameValidator`) are well-defined and the App layer composes them via constructor injection. Easy to test, easy to swap implementations.
- **Security-conscious defaults.** Secret values are never logged. Process-scoped env vars avoid persistent writes. DPAPI cache is opt-in. RBAC authorization is the default Key Vault access model.
- **Solid provisioning scripts.** All 7 scripts are idempotent (check-before-create), parameterized, and cover the full Key Vault lifecycle from prereqs through diagnostics. Private endpoint and firewall modes are properly separated.
- **Secret name validation.** Centralized regex (`^[A-Za-z][0-9A-Za-z-]{0,126}$`) enforced consistently across stores and scripts.
- **Retry configuration.** `AzureKeyVaultSecretStore` configures exponential backoff on the `SecretClient` with configurable `MaxRetryCount`.

## Findings

### 🔴 High Severity

| # | Location | Finding | Why It Matters | Recommendation |
|---|----------|---------|----------------|----------------|
| 1 | `DpapiSecretCacheStore.cs` | DPAPI cache stores a single `SecretEntry` in one file. Each `Save()` call overwrites the previous entry. `TryLoad()` only matches if the stored name equals the requested name. | The cache silently drops all but the most recently fetched secret. If a user retrieves `secret-a` then `secret-b`, `secret-a`'s DPAPI entry is gone. This defeats the purpose of persistent caching. | Store entries per-secret-name (e.g. one file per secret, or a keyed JSON dictionary). Use the secret name as part of the filename or as a dictionary key. |
| 2 | `AzureKeyVaultSecretStore.cs:49-50`, `InMemorySecretStore.cs:42-44` | `ExpiresAtUtc` is hard-coded to `nowUtc.AddHours(8)` in both `GetSecretAsync` implementations, ignoring the configured `SecretCacheTtl` from `AppConfig`. | The configured TTL in `appsettings.json` controls `MemorySecretCache` eviction, but the `SecretEntry.ExpiresAtUtc` field (which DPAPI uses for expiration checks) is always 8 hours regardless. Changing the config TTL has no effect on the DPAPI cache. | Pass the configured TTL into the store or compute `ExpiresAtUtc` from `AppConfig.SecretCacheTtl`. Alternatively, derive it from the `MemorySecretCache` TTL at the `CachedSecretStore` level before persisting. |
| 3 | `CachedSecretStore.cs:51-59` | `SetSecretAsync` invalidates the memory cache via `_cache.Remove(secretName)` on success, but does not invalidate or delete the DPAPI persisted entry. | After a secret rotation, the DPAPI cache can serve the old value on next cold start (memory cache empty, DPAPI hit returns stale entry) until the hard-coded 8-hour expiry passes. | Delete or invalidate the DPAPI entry on successful `SetSecretAsync`. Either delete the persisted file for that secret, or refresh it with the new value. |
| 4 | `MainViewModel.cs:198-207` | `ResolveSecretValue` calls `_secretStore.GetSecretAsync(secretName).GetAwaiter().GetResult()` — synchronous blocking on the WPF UI thread. | In Azure mode, a cache miss triggers a network call to Key Vault. Blocking the dispatcher risks a UI freeze (seconds) or deadlock if the `SynchronizationContext` is involved. Exceptions are silently swallowed, returning `null`. | Make `LaunchProcess` async. Resolve all secret values with `await` before calling the process launcher. Surface failures in `StatusMessage`. |
| 5 | `App.xaml.cs:78-88` | `LoadConfig()` catches all exceptions and silently falls back to `CreateDefaultConfig()`, which sets `UseInMemoryDemoStore = true`. | A typo in `appsettings.json`, a missing required property, or a malformed JSON file silently switches the app from Key Vault mode to demo mode. The operator sees "Demo mode" in the status bar but may not realize it's unintentional — writes go to an ephemeral in-memory store. | Fail fast on config parse errors. Show a blocking error dialog with the specific parse failure. At minimum, log the exception before falling back and display a warning banner in the UI. |

### 🟡 Medium Severity

| # | Location | Finding | Why It Matters | Recommendation |
|---|----------|---------|----------------|----------------|
| 6 | `DpapiSecretCacheStore.cs:35-53` | `TryLoad()` does not handle `CryptographicException`, `IOException`, `JsonException`, or other deserialization failures. Any corruption or profile change throws an unhandled exception. | A corrupt cache file or Windows profile change crashes the secret retrieval path instead of gracefully falling through to the backing store. The user must manually delete the cache file to recover. | Catch `CryptographicException`, `IOException`, and `JsonException` in `TryLoad`. Delete the corrupt file and return `false` so the backing store is used. |
| 7 | `EnvProcessLauncher.cs:29-38` | `Process.Start(startInfo)` is not wrapped in exception handling. | An invalid executable path that passes `File.Exists()` (e.g. permissions issue, locked file) or a process startup failure throws an unhandled exception that bypasses the `OperationResult` return path. | Wrap `Process.Start()` in a try-catch for `InvalidOperationException`, `System.ComponentModel.Win32Exception`, and `IOException`. Return a failed `OperationResult` with diagnostic info. |
| 8 | `MainWindow.xaml:52-53, 63-64` | Secret values are displayed in a plain `TextBox` and copied to clipboard without masking or auto-clear. | Shoulder-surfing risk, screen capture exposure, and clipboard history retention. For a secret management tool, displaying values in plaintext by default undermines the security posture. | Mask the value by default (e.g. `PasswordBox` or `•••••` display). Add an explicit "Reveal" toggle. Implement a timed clipboard clear (e.g. 30 seconds) after copy. |
| 9 | `MainViewModel.cs:172-181` | `CopySecret` calls `Clipboard.SetText()` with no auto-clear. The status message says "Clear manually or implement timer-based clear policy" — indicating this is a known gap. | Secrets persist in the clipboard (and clipboard history tools like Windows Clipboard History) indefinitely until manually cleared or overwritten. | Implement a `DispatcherTimer` that clears the clipboard after a configurable timeout (30s is a reasonable default). Disable Windows Clipboard History for the copied value if possible. |
| 10 | `BHGKeyMan.Core.Tests/UnitTest1.cs` | Test coverage is limited to 5 tests covering `SecretNameValidator`, `InMemorySecretStore` happy paths, and `EnvProcessLauncher` missing-exe. No tests for `CachedSecretStore`, DPAPI behavior, config parsing, `AzureKeyVaultSecretStore` error mapping, or `MainViewModel` logic. | The highest-risk bugs (stale cache, silent config fallback, sync-over-async, DPAPI corruption) have no regression tests. Any fix to findings 1-6 could regress silently. | Add tests for: (a) `CachedSecretStore` cache-miss → fetch → cache-hit flow, (b) `CachedSecretStore.SetSecretAsync` invalidation, (c) DPAPI round-trip and corruption recovery, (d) config parsing with missing/malformed JSON, (e) `MemorySecretCache` TTL expiry. |
| 11 | `BHGKeyMan.Core/Class1.cs` | Empty file — 0 bytes. | Dead code. Minor but clutters the project. | Delete `Class1.cs`. |

### 🔵 Low Severity / Observations

| # | Location | Finding | Recommendation |
|---|----------|---------|----------------|
| 12 | `MainViewModel.cs:100-101` | `SignIn()` is a no-op — sets a status string but performs no actual authentication. | Fine for the current demo/dev phase, but should be wired to `InteractiveBrowserCredential.GetTokenAsync()` or similar before production use. Track as a known gap. |
| 13 | `RelayCommand.cs` | `CanExecuteChanged` is never raised by any command. | Commands are always enabled. For better UX, disable commands during async operations (e.g. disable "Get Secret" while a fetch is in progress) and call `RaiseCanExecuteChanged()`. |
| 14 | `MainWindow.xaml` | `SecretNameInput` is bound to two separate TextBoxes (Retrieve panel line 50, Set panel line 69). | Changing the name in either panel changes both. This may be intentional (shared context), but it can confuse users who expect independent inputs. Consider separating into `RetrieveSecretName` and `SetSecretName` properties if the behaviors should be independent. |
| 15 | `scripts/05-seed-secrets.ps1` | Secrets are passed as `--value` arguments to `az keyvault secret set`. | Command-line arguments may appear in process listings, shell history, and audit logs. For production seeding, consider using `--file` or piping values via stdin. Acceptable for non-production use. |
| 16 | `BHGKeyMan.App.csproj` | `Azure.Identity` 1.17.0 and `Azure.Security.KeyVault.Secrets` 4.8.0 — verify these are current. | Run `dotnet list package --outdated` periodically. Azure SDK packages receive regular security and feature updates. |
| 17 | `appsettings.json` | Default config ships with `UseInMemoryDemoStore: true` and placeholder GUIDs. | Good for dev. Ensure deployment documentation clearly states the config must be updated before production use, and that finding 5 (silent fallback) is addressed so misconfigs don't silently revert to demo mode. |

## Architecture Assessment

The overall architecture is sound. The layered design (Core abstractions → App implementations → WPF MVVM) follows established patterns and will scale well as features are added. Key observations:

- **Dependency injection** is manual but clean. If the app grows, consider `Microsoft.Extensions.DependencyInjection` with `IHost` for startup orchestration.
- **The `CachedSecretStore` decorator pattern** is the right approach for layered caching. The bugs are in implementation details (DPAPI single-entry, TTL mismatch, stale-on-write), not in the pattern itself.
- **Scripts are production-quality** for their scope. Idempotent, parameterized, and cover the full provisioning lifecycle.

## Priority Order

1. **Fix stale DPAPI cache** — per-secret storage, TTL from config, invalidation on write (findings 1, 2, 3)
2. **Remove sync-over-async on UI thread** — make `LaunchProcess` fully async (finding 4)
3. **Fail fast on bad config** — surface parse errors instead of silent demo-mode fallback (finding 5)
4. **Harden error handling** — DPAPI corruption recovery, process launch exceptions (findings 6, 7)
5. **Secret value masking and clipboard auto-clear** (findings 8, 9)
6. **Backfill test coverage** for cache, config, DPAPI, and ViewModel paths (finding 10)
7. **Cleanup** — delete `Class1.cs`, separate shared TextBox bindings if appropriate (findings 11, 14)

## Spec Compliance Check

Cross-referenced against `spec.md` requirements:

| Requirement | Status | Notes |
|---|---|---|
| FR-1 Authentication | ⚠️ Partial | Interactive auth is wired for Azure mode but `SignIn` button is a no-op placeholder |
| FR-2 Secret Retrieval | ✅ Implemented | Error mapping for 404/403 present in `AzureKeyVaultSecretStore` |
| FR-3 Secret Write/Update | ✅ Implemented | Versioning behavior documented in success message |
| FR-4 Secret Listing/Search | ⚠️ Partial | Listing works; filtering/search not implemented |
| FR-5 Env Var Export | ✅ Implemented | Process-scoped injection via `EnvProcessLauncher` |
| FR-6 Clipboard Handling | ⚠️ Partial | Copy works; auto-clear not implemented |
| FR-7 Cache | ⚠️ Bugs | Memory cache works; DPAPI has single-entry and TTL bugs |
| FR-8 Audit-Friendly UX | ✅ Implemented | Status messages surface outcomes without secret values |
| SR-1 RBAC | ✅ Implemented | Scripts assign correct roles; app uses RBAC-enabled vault |
| SR-2 Secret Handling | ⚠️ Partial | No plaintext logs, but UI displays values unmasked |
| SR-3 Network Posture | ✅ Implemented | Private endpoint and firewall scripts both functional |
| SR-4 Resilience/Throttling | ✅ Implemented | Exponential backoff configured on SecretClient |
| SR-5 Lifecycle Controls | ✅ Implemented | Soft delete + purge protection enabled in provisioning |
