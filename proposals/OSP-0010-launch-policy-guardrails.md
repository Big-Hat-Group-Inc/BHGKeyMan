# OpenSpec Proposal: Process Launch Policy Guardrails

## 1) Proposal Metadata
- **Proposal ID:** OSP-0010
- **Title:** Process Launch Policy Guardrails
- **Status:** Draft
- **Authors:** BHGKeyMan contributors
- **Last Updated:** 2026-03-12
- **Priority:** High
- **Code Review Finding:** `codereview.md` finding 2
- **Primary References:**
  - `codereview.md`
  - `BHGKeyMan.App/MainViewModel.cs`
  - `BHGKeyMan.Core/EnvProcessLauncher.cs`
  - `spec.md` FR-5

## 2) Problem Statement

The current process-launch feature allows:
- any executable path
- any environment variable name
- secret injection with no explicit approval step

For a secret-management tool, this is too permissive. It makes accidental or intentional exfiltration much easier and does not reflect least-privilege defaults.

## 3) Proposed Solution

### 3.1 Add configuration-based allowlists

Introduce policy settings for:
- allowed executable paths or directories
- allowed environment variable names
- whether launch-with-secret is enabled at all

### 3.2 Block high-risk variable names by default

Deny or require explicit override for names such as:
- `PATH`
- `COMSPEC`
- `DOTNET_*`
- proxy-related variables

### 3.3 Require explicit user confirmation

Before launch, show a confirmation dialog containing:
- executable path
- secret name
- target environment variable name

The dialog must not show the secret value.

### 3.4 Record non-secret audit metadata

Capture local audit metadata for launch attempts:
- timestamp
- executable path
- secret name
- environment variable name
- success or failure

## 4) Goals

- Secret injection is limited to approved processes and variable names.
- Launching a secret-bearing child process requires explicit user confirmation.
- Audit metadata exists without logging secret values.

## 5) Non-Goals

- Building a centralized enterprise policy engine.
- Verifying executable signatures in the first iteration.

## 6) Affected Files

| File | Change |
|------|--------|
| `BHGKeyMan.Core/Models.cs` | Add launch policy configuration settings. |
| `BHGKeyMan.Core/EnvProcessLauncher.cs` | Enforce allowlists and blocked names. |
| `BHGKeyMan.App/MainViewModel.cs` | Add confirmation flow and audit integration. |
| `BHGKeyMan.App/MainWindow.xaml` | Clarify the launch UI and policy feedback. |
| `BHGKeyMan.App/appsettings.json` | Add launch policy configuration. |

## 7) Acceptance Criteria

1. Launch is blocked when the executable path is not allowed.
2. Launch is blocked when the environment variable name is not allowed.
3. A user confirmation step occurs before any secret-bearing process launch.
4. Audit metadata is recorded without secret values.

## 8) Risks and Mitigations

- **Risk:** Initial allowlists are too restrictive and frustrate operators.
  **Mitigation:** Make policy explicit and configurable, with clear rejection messages.

- **Risk:** Local audit logging itself becomes sensitive.
  **Mitigation:** Log metadata only; never log values or full environment state.

## 9) Test Plan

- Unit test blocked executable path.
- Unit test blocked environment variable name.
- Manual confirmation-dialog flow.
- Manual audit-log verification with no secret value leakage.
