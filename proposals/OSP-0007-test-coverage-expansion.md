# OpenSpec Proposal: Test Coverage Expansion

## 1) Proposal Metadata
- **Proposal ID:** OSP-0007
- **Title:** Backfill Unit and Integration Test Coverage for Cache, Config, DPAPI, and ViewModel
- **Status:** Draft
- **Authors:** BHGKeyMan contributors
- **Last Updated:** 2026-03-11
- **Priority:** Medium
- **Code Review Findings:** #10
- **Primary References:**
  - `codereview.md` finding 10
  - `BHGKeyMan.Core.Tests/UnitTest1.cs`
  - All files affected by OSP-0002 through OSP-0006

## 2) Problem Statement

Current test coverage is limited to 5 tests (now 9 per the review) covering `SecretNameValidator`, `InMemorySecretStore` happy paths, and `EnvProcessLauncher` missing-exe. The highest-risk components have zero test coverage:

- **`CachedSecretStore`** — the decorator that orchestrates two cache layers and a backing store.
- **DPAPI round-trip** — encrypt → persist → load → decrypt.
- **DPAPI corruption recovery** — what happens when the cache file is corrupt.
- **Config parsing** — the `LoadConfig` path with missing/malformed JSON.
- **`AzureKeyVaultSecretStore` error mapping** — 404 → `KeyNotFoundException`, 403 → `UnauthorizedAccessException`.
- **`MemorySecretCache` TTL expiry** — entries should expire after the configured TTL.
- **`MainViewModel` command logic** — state transitions, error surfacing, clipboard behavior.

Without these tests, any fix to findings 1–9 could regress silently.

## 3) Proposed Solution

### 3.1 Test Organization

Rename `UnitTest1.cs` to descriptive test classes:

| Test Class | Coverage Area |
|------------|---------------|
| `SecretNameValidatorTests.cs` | Existing validator tests (moved from UnitTest1) |
| `InMemorySecretStoreTests.cs` | Existing in-memory store tests (moved from UnitTest1) |
| `EnvProcessLauncherTests.cs` | Existing launcher test (moved from UnitTest1) |
| `CachedSecretStoreTests.cs` | **New** — cache-miss/hit flow, set invalidation, DPAPI integration |
| `DpapiSecretCacheStoreTests.cs` | **New** — round-trip, corruption, expiry |
| `MemorySecretCacheTests.cs` | **New** — TTL expiry, eviction |
| `AppConfigTests.cs` | **New** — validation, deserialization edge cases |
| `MainViewModelTests.cs` | **New** — command execution, state transitions |

### 3.2 Test Specifications

#### CachedSecretStoreTests

1. **Cache miss → fetch → cache hit:** First `GetSecretAsync` calls inner store; second returns from memory cache.
2. **DPAPI fallback on cold start:** Memory cache empty; DPAPI has valid entry → returns cached value without calling inner store.
3. **SetSecretAsync invalidates memory cache:** After set, next get calls inner store.
4. **SetSecretAsync invalidates DPAPI cache:** After set, DPAPI file for that secret is deleted.
5. **Expired DPAPI entry falls through:** DPAPI entry with past `ExpiresAtUtc` is ignored.

#### DpapiSecretCacheStoreTests (Windows-only)

1. **Round-trip:** Save → Load returns identical `SecretEntry`.
2. **Per-secret isolation:** Save `secret-a` and `secret-b`; load each independently.
3. **Corruption recovery:** Write invalid bytes to cache file; `TryLoad` returns `false` and deletes the file.
4. **Expired entry:** Save with past `ExpiresAtUtc`; `TryLoad` returns `false`.
5. **Missing file:** `TryLoad` on nonexistent file returns `false` without throwing.

#### MemorySecretCacheTests

1. **Cache hit within TTL:** Add entry; retrieve within TTL → returns entry.
2. **Cache miss after TTL:** Add entry; wait past TTL → returns null/miss.
3. **Remove invalidates:** Add entry; `Remove(name)` → next get returns miss.

#### AppConfigTests

1. **Valid JSON deserializes correctly.**
2. **Missing required field returns validation error** (when `UseInMemoryDemoStore` is `false`).
3. **Invalid `SecretCacheTtlHours` (0 or negative) returns validation error.**
4. **Placeholder GUIDs return validation error** (when `UseInMemoryDemoStore` is `false`).
5. **`UseInMemoryDemoStore: true` passes validation without Key Vault fields.**

#### MainViewModelTests

1. **GetSecretCommand updates `RetrievedSecretValue` and `StatusMessage`.**
2. **SetSecretCommand clears input and updates `StatusMessage`.**
3. **CopySecretCommand sets clipboard text** (may require WPF test infrastructure or abstraction).
4. **Error from store surfaces in `StatusMessage`.**

### 3.3 Test Infrastructure

- Use `Moq` or a similar mocking library for `ISecretStore`, `ISecretCache`, `IDpapiSecretCacheStore` fakes.
- Mark DPAPI tests with `[SupportedOSPlatform("windows")]` and a conditional skip for CI runners on non-Windows.
- Use `TempDirectory` fixtures for file-based tests to ensure cleanup.

## 4) Goals
- Every bug fix from OSP-0002 through OSP-0006 has a corresponding regression test.
- Test coverage spans the critical path: cache orchestration, DPAPI, config, error handling.
- Tests are organized by component, not in a single file.

## 5) Non-Goals
- Achieving a specific code coverage percentage target.
- End-to-end integration tests against a live Azure Key Vault (requires infrastructure).
- UI automation tests for `MainWindow.xaml`.

## 6) Affected Files

| File | Change |
|------|--------|
| `BHGKeyMan.Core.Tests/UnitTest1.cs` | Delete after migrating tests to new files. |
| `BHGKeyMan.Core.Tests/SecretNameValidatorTests.cs` | **New** — migrated from UnitTest1. |
| `BHGKeyMan.Core.Tests/InMemorySecretStoreTests.cs` | **New** — migrated from UnitTest1. |
| `BHGKeyMan.Core.Tests/EnvProcessLauncherTests.cs` | **New** — migrated from UnitTest1. |
| `BHGKeyMan.Core.Tests/CachedSecretStoreTests.cs` | **New.** |
| `BHGKeyMan.Core.Tests/DpapiSecretCacheStoreTests.cs` | **New.** |
| `BHGKeyMan.Core.Tests/MemorySecretCacheTests.cs` | **New.** |
| `BHGKeyMan.Core.Tests/AppConfigTests.cs` | **New.** |
| `BHGKeyMan.Core.Tests/MainViewModelTests.cs` | **New** (may require project reference to App). |
| `BHGKeyMan.Core.Tests/BHGKeyMan.Core.Tests.csproj` | Add `Moq` package reference; optionally add App project reference. |

## 7) Acceptance Criteria

1. All existing tests pass after migration to new files.
2. `UnitTest1.cs` is deleted.
3. At least one test exists for each of the 5 test classes listed above.
4. `CachedSecretStore` cache-miss → fetch → cache-hit flow is tested.
5. DPAPI corruption recovery is tested.
6. Config validation with malformed input is tested.
7. `dotnet test` passes on Windows with 0 failures.

## 8) Risks and Mitigations

- **Risk:** DPAPI tests fail on non-Windows CI runners.
  **Mitigation:** Use `[SupportedOSPlatform("windows")]` and conditional `[Fact(Skip = ...)]` or `#if WINDOWS` guards.

- **Risk:** ViewModel tests require WPF dispatcher or clipboard.
  **Mitigation:** Abstract clipboard behind `IClipboardService`. For dispatcher, use `SynchronizationContext.SetSynchronizationContext(new SynchronizationContext())` in test setup.

## 9) Dependencies

- **OSP-0002** must be implemented before `CachedSecretStoreTests` and `DpapiSecretCacheStoreTests` can be written against the new per-secret-file model.
- **OSP-0004** must be implemented before `AppConfigTests` can test the validation logic.
- **OSP-0005** must be implemented before corruption recovery tests can be written.

Tests for existing (pre-fix) behavior can be written immediately and updated as fixes land.
