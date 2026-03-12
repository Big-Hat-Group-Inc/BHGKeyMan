# OpenSpec Proposal: DPAPI Cache Overhaul

## 1) Proposal Metadata
- **Proposal ID:** OSP-0002
- **Title:** Fix DPAPI Cache — Per-Secret Storage, Configurable TTL, Write Invalidation
- **Status:** Draft
- **Authors:** BHGKeyMan contributors
- **Last Updated:** 2026-03-11
- **Priority:** High
- **Code Review Findings:** #1 (single-entry overwrite), #2 (hard-coded 8h TTL), #3 (stale cache on write)
- **Primary References:**
  - `codereview.md` findings 1, 2, 3
  - `spec.md` FR-7 (Cache)
  - `BHGKeyMan.Core/DpapiSecretCacheStore.cs`
  - `BHGKeyMan.App/CachedSecretStore.cs`
  - `BHGKeyMan.App/AzureKeyVaultSecretStore.cs`
  - `BHGKeyMan.Core/InMemorySecretStore.cs`

## 2) Problem Statement

The DPAPI persistent cache has three interconnected bugs that undermine its purpose:

1. **Single-entry overwrite (Finding #1):** `DpapiSecretCacheStore` stores one `SecretEntry` in a single file. Each `Save()` overwrites the previous entry. Retrieving `secret-a` then `secret-b` silently drops `secret-a`'s cached value.

2. **Hard-coded TTL (Finding #2):** `ExpiresAtUtc` is set to `nowUtc.AddHours(8)` in both `AzureKeyVaultSecretStore` and `InMemorySecretStore`, ignoring the configurable `SecretCacheTtl` in `AppConfig`. The user-facing config knob has no effect on DPAPI expiration.

3. **Stale cache on write (Finding #3):** `CachedSecretStore.SetSecretAsync` invalidates the in-memory cache but does not touch the DPAPI entry. After a secret rotation, a cold start (empty memory cache) serves the old value from DPAPI until the hard-coded 8-hour window expires.

Together, these bugs mean the DPAPI cache can silently serve stale secrets after rotation and can only hold one secret at a time.

## 3) Proposed Solution

### 3.1 Per-Secret File Storage

Replace the single-file model with one encrypted file per secret name.

- **File naming:** Use the secret name (already validated against `^[A-Za-z][0-9A-Za-z-]{0,126}$`) as the filename: `{DpapiCachePath}/{secretName}.bin`.
- **`DpapiCachePath` config:** Reinterpret as a directory path rather than a single file path. Create the directory on first use if it does not exist.
- Update `IDpapiSecretCacheStore` (or introduce it if not present) to accept the secret name as a routing key.

### 3.2 Configurable TTL

- Remove the hard-coded `nowUtc.AddHours(8)` from `AzureKeyVaultSecretStore.GetSecretAsync` and `InMemorySecretStore.GetSecretAsync`.
- Compute `ExpiresAtUtc` using the configured `AppConfig.SecretCacheTtl` (currently `SecretCacheTtlHours`).
- Pass the TTL value into the store implementations via constructor injection or compute it at the `CachedSecretStore` level before persisting to DPAPI.

### 3.3 Write Invalidation

- In `CachedSecretStore.SetSecretAsync`, after the inner store succeeds and the memory cache is invalidated, also delete the corresponding DPAPI file for that secret name.
- Alternatively, overwrite the DPAPI entry with the new value (refresh-on-write). Deletion is simpler and avoids caching a value that may not match what Key Vault actually stored (e.g., if server-side policies transform values).

## 4) Goals
- DPAPI cache correctly holds multiple secrets concurrently.
- Configured TTL governs both memory and DPAPI expiration.
- Secret rotation via `SetSecretAsync` immediately invalidates all cache layers.
- No stale values served on cold start after a write.

## 5) Non-Goals
- Encrypting the filename itself (secret names are not considered sensitive; values are).
- Supporting non-Windows DPAPI equivalents (out of scope per `spec.md`).
- Adding a cache-wide purge/flush command (can be a separate proposal).

## 6) Affected Files

| File | Change |
|------|--------|
| `BHGKeyMan.Core/DpapiSecretCacheStore.cs` | Refactor to per-secret-name file storage; accept directory path; add `Delete(secretName)` method. |
| `BHGKeyMan.Core/Interfaces.cs` | Add `Delete(string secretName)` to `IDpapiSecretCacheStore` (or equivalent cache persistence interface). |
| `BHGKeyMan.App/CachedSecretStore.cs` | Call DPAPI delete on `SetSecretAsync` success. Pass TTL when constructing `SecretEntry`. |
| `BHGKeyMan.App/AzureKeyVaultSecretStore.cs` | Replace `AddHours(8)` with configurable TTL from `AppConfig`. |
| `BHGKeyMan.Core/InMemorySecretStore.cs` | Replace `AddHours(8)` with configurable TTL from `AppConfig`. |
| `BHGKeyMan.Core/Models.cs` | No schema change needed; `SecretEntry.ExpiresAtUtc` already exists. |
| `BHGKeyMan.App/appsettings.json` | Update `DpapiCachePath` default to a directory path (e.g., `%LOCALAPPDATA%/BHGKeyMan/cache/`). |

## 7) Acceptance Criteria

1. Retrieving `secret-a` then `secret-b` retains both entries in the DPAPI cache directory.
2. Changing `SecretCacheTtlHours` in config changes the `ExpiresAtUtc` on newly cached entries.
3. After `SetSecretAsync("secret-a", "new-value")`, a cold restart (empty memory cache) does **not** return the old DPAPI-cached value for `secret-a`.
4. Expired DPAPI entries are ignored on load (existing behavior, now with correct TTL).
5. All existing tests continue to pass; new tests added per OSP-0007.

## 8) Risks and Mitigations

- **Risk:** Existing users have a single-file cache at the old `DpapiCachePath`.
  **Mitigation:** On startup, if `DpapiCachePath` points to a file instead of a directory, delete the legacy file and create the directory. Log a one-time info message.

- **Risk:** File-per-secret creates many small files if the user accesses hundreds of secrets.
  **Mitigation:** Acceptable for the expected use case (tens of secrets). A future proposal can add a compacted dictionary format if needed.

## 9) Test Plan

- Unit test: Save two secrets, load each by name, verify both are present.
- Unit test: Save with TTL = 1 second, wait, verify load returns expired/miss.
- Unit test: Save, then delete by name, verify load returns miss.
- Integration test: `CachedSecretStore` set → verify DPAPI file deleted.
- Integration test: Full round-trip with configurable TTL from `AppConfig`.
