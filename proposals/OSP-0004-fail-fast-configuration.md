# OpenSpec Proposal: Strong Configuration Validation

## 1) Proposal Metadata
- **Proposal ID:** OSP-0004
- **Title:** Strong Configuration Validation
- **Status:** Draft
- **Authors:** BHGKeyMan contributors
- **Last Updated:** 2026-03-12
- **Priority:** High
- **Code Review Finding:** `codereview.md` finding 4
- **Primary References:**
  - `codereview.md`
  - `BHGKeyMan.Core/Models.cs`
  - `BHGKeyMan.App/App.xaml.cs`
  - `BHGKeyMan.Core.Tests/AppConfigTests.cs`

## 2) Problem Statement

`AppConfig.Validate()` says tenant and client identifiers must be valid GUIDs in non-demo mode, but the implementation only rejects placeholders and empty strings. Malformed values can pass startup validation and then fail later during authentication.

The current unit tests reinforce the problem because their "valid" Azure configuration uses non-GUID strings.

## 3) Proposed Solution

### 3.1 Enforce GUID format

Use `Guid.TryParse` for:
- `TenantId`
- `ClientId`

Validation should fail fast during startup when Azure mode is enabled.

### 3.2 Keep explicit demo-mode behavior

Demo mode remains allowed with placeholders, but only when `UseInMemoryDemoStore` is `true`.

### 3.3 Fix the test suite

Update `AppConfigTests` so:
- valid Azure-mode test values are real GUID-shaped strings
- malformed GUID strings are explicitly tested and rejected

## 4) Goals

- Startup validation matches the documented contract.
- Invalid Azure identifiers fail before any auth attempt.
- Tests accurately cover both valid and invalid config shapes.

## 5) Non-Goals

- Adding schema-based config loading.
- Supporting multiple auth modes in the same proposal.

## 6) Affected Files

| File | Change |
|------|--------|
| `BHGKeyMan.Core/Models.cs` | Add real GUID validation to `AppConfig.Validate()`. |
| `BHGKeyMan.Core.Tests/AppConfigTests.cs` | Correct test fixtures and add malformed GUID tests. |
| `BHGKeyMan.App/App.xaml.cs` | No major behavior change beyond consuming stricter validation. |

## 7) Acceptance Criteria

1. Non-demo mode rejects malformed `TenantId` values.
2. Non-demo mode rejects malformed `ClientId` values.
3. Demo mode still permits placeholders.
4. Unit tests contain valid GUID-shaped positive cases and malformed negative cases.

## 8) Risks and Mitigations

- **Risk:** Existing local configs with malformed IDs will stop starting.
  **Mitigation:** This is intentional fail-fast behavior; the error message should name the invalid setting.

## 9) Test Plan

- Unit test malformed tenant GUID.
- Unit test malformed client GUID.
- Unit test valid Azure-mode config with real GUID strings.
- Manual startup test with invalid `appsettings.json` identifiers.
