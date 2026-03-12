# OpenSpec Proposal: Secret Exposure Reduction and Clipboard Controls

## 1) Proposal Metadata
- **Proposal ID:** OSP-0006
- **Title:** Secret Exposure Reduction and Clipboard Controls
- **Status:** Draft
- **Authors:** BHGKeyMan contributors
- **Last Updated:** 2026-03-12
- **Priority:** High
- **Code Review Finding:** `codereview.md` finding 1
- **Primary References:**
  - `codereview.md`
  - `BHGKeyMan.App/MainViewModel.cs`
  - `BHGKeyMan.App/MainWindow.xaml`
  - `BHGKeyMan.Core/DpapiSecretCacheStore.cs`

## 2) Problem Statement

Secret values are currently:
- stored in normal managed `string` fields in the view model
- bound through standard `TextBox` controls
- copied to the clipboard as plaintext
- serialized to JSON before DPAPI encryption

This is not a catastrophic design bug for a desktop app, but it is too permissive for a security-sensitive workflow and increases exposure through clipboard history, UI automation, screen capture, crash dumps, and memory inspection.

## 3) Proposed Solution

### 3.1 Reduce UI lifetime of secret values

- Stop keeping retrieved secrets in long-lived view-model properties longer than needed.
- Clear secret-bearing fields after reveal timeout, copy timeout, or navigation changes where practical.

### 3.2 Use safer input/output controls

- Replace normal secret-entry `TextBox` controls with a safer input pattern such as `PasswordBox` or an equivalent abstraction.
- Keep retrieved values masked by default and only reveal on explicit user action.

### 3.3 Treat clipboard as an elevated action

- Keep clipboard auto-clear.
- Add a user-facing warning before copy, or make clipboard copy configurable by policy.
- Avoid clearing the clipboard if the user has copied something else since the secret copy.

### 3.4 Add tests for exposure controls

- reveal timeout
- clipboard auto-clear
- value reset after copy or timeout where implemented

## 4) Goals

- Secret values spend less time in plaintext in UI state.
- Secret input and output paths are intentionally security-biased.
- Clipboard behavior is explicit and bounded.

## 5) Non-Goals

- Eliminating all plaintext memory exposure in a .NET desktop process.
- Introducing deprecated `SecureString`-centric design.

## 6) Affected Files

| File | Change |
|------|--------|
| `BHGKeyMan.App/MainWindow.xaml` | Replace raw secret text controls with safer patterns. |
| `BHGKeyMan.App/MainViewModel.cs` | Minimize secret field lifetime and harden copy/reveal flows. |
| `BHGKeyMan.Core/DpapiSecretCacheStore.cs` | Review serialization path and document residual plaintext-in-memory tradeoff. |
| `BHGKeyMan.Core.Tests/` and app tests | Add behavior tests for exposure controls. |

## 7) Acceptance Criteria

1. Secret values are masked by default in the UI.
2. Secret entry does not rely on a normal editable `TextBox` bound directly to a plaintext property.
3. Clipboard copy is auto-cleared and does not wipe user-replaced clipboard content.
4. Automated tests cover reveal and clipboard timeout behavior.

## 8) Risks and Mitigations

- **Risk:** Safer controls reduce convenience.
  **Mitigation:** Keep explicit reveal and copy actions, but require deliberate intent.

- **Risk:** Plaintext still exists briefly in managed memory.
  **Mitigation:** Document the residual risk and minimize lifetime rather than claiming perfect secrecy.

## 9) Test Plan

- Manual test masked-by-default retrieval flow.
- Manual test reveal timeout.
- Manual test clipboard copy and replacement behavior.
- Automated tests for timeout-driven state reset.
