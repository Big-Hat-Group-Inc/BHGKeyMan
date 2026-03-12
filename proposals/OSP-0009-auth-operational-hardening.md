# OpenSpec Proposal: Operational Script Hygiene and Secret Input Hardening

## 1) Proposal Metadata
- **Proposal ID:** OSP-0009
- **Title:** Operational Script Hygiene and Secret Input Hardening
- **Status:** Draft
- **Authors:** BHGKeyMan contributors
- **Last Updated:** 2026-03-12
- **Priority:** Medium
- **Code Review Finding:** `codereview.md` finding 5
- **Primary References:**
  - `codereview.md`
  - `scripts/06-verify-access.ps1`
  - `scripts/05-seed-secrets.ps1`

## 2) Problem Statement

The verification flow currently:
- writes a real secret value to the vault
- passes the test value on the command line
- does not remove the test secret afterward

That leaves verification artifacts behind and unnecessarily exposes the value through command-line inspection and shell history.

## 3) Proposed Solution

### 3.1 Use disposable verification artifacts

- Generate a unique temporary verification secret name per run.
- Remove the secret at the end of the script on success and on failure when possible.

### 3.2 Avoid command-line secret exposure

- Prefer stdin-based value delivery to `az keyvault secret set` where supported.
- If stdin is not viable for a specific path, document the exposure tradeoff and use a short-lived temp file with cleanup.

### 3.3 Add cleanup safety

- Use `try/finally` style cleanup in PowerShell so test artifacts are removed even if the verification step fails after write.

## 4) Goals

- Verification leaves no long-lived test secret behind.
- Secret values are not exposed on the command line in normal operation.
- The script remains operator-friendly and idempotent.

## 5) Non-Goals

- Building a full script framework.
- Hiding the existence of verification actions from audit logs.

## 6) Affected Files

| File | Change |
|------|--------|
| `scripts/06-verify-access.ps1` | Use unique temporary secret names, stdin or temp-file input, and cleanup logic. |
| `scripts/05-seed-secrets.ps1` | Keep stdin-based patterns aligned with the verification script. |
| `README.md` | Document the verification behavior and cleanup guarantees. |

## 7) Acceptance Criteria

1. `06-verify-access.ps1` creates a unique temporary secret for each run.
2. The temporary secret is deleted after verification.
3. The verification value is not passed via command-line `--value` in the default path.
4. Script documentation states the expected cleanup behavior.

## 8) Risks and Mitigations

- **Risk:** Cleanup can fail if permissions change mid-run.
  **Mitigation:** Warn clearly and print the exact temporary secret name for manual removal.

- **Risk:** Stdin handling varies by environment.
  **Mitigation:** Test under PowerShell 7 and provide a documented fallback.

## 9) Test Plan

- Manual run verifying temporary secret creation and deletion.
- Manual inspection confirming no secret value appears in the command line.
- Failure-path test confirming cleanup still runs.
