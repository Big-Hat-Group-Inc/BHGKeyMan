# OpenSpec Proposal: Async Command Execution and Busy-State Safety

## 1) Proposal Metadata
- **Proposal ID:** OSP-0003
- **Title:** Async Command Execution and Busy-State Safety
- **Status:** Draft
- **Authors:** BHGKeyMan contributors
- **Last Updated:** 2026-03-12
- **Priority:** High
- **Code Review Finding:** `codereview.md` finding 3
- **Primary References:**
  - `codereview.md`
  - `BHGKeyMan.App/RelayCommand.cs`
  - `BHGKeyMan.App/MainViewModel.cs`

## 2) Problem Statement

The WPF command layer currently uses `RelayCommand(Action)` while several commands pass async lambdas. In practice this behaves like `async void`, which weakens exception handling, completion tracking, and command-state control.

`MainViewModel` also uses a shared `_isBusy` flag across nested operations. In `SetSecretAsync()`, the method calls `LoadSecretsAsync()`, and both methods independently set and clear busy state. That allows buttons to be re-enabled before the full outer workflow finishes.

## 3) Proposed Solution

### 3.1 Introduce an async-aware command type

Add an `AsyncRelayCommand` that:
- accepts `Func<Task>`
- tracks `IsRunning`
- exposes `CanExecute`
- catches and routes failures through a supplied error handler

### 3.2 Eliminate nested busy-state races

Replace the single shared `_isBusy` toggle pattern with one of these approaches:
- per-command running state, or
- a scoped busy counter

The goal is to ensure nested operations do not clear busy state prematurely.

### 3.3 Make command enablement deterministic

Commands should be disabled when:
- their required inputs are empty
- that command is already running
- a higher-level operation blocks it

### 3.4 Add view-model tests

Add tests that verify:
- commands disable during execution
- commands re-enable only after the full workflow completes
- async command failures are surfaced without crashing the UI thread

## 4) Goals

- No UI commands rely on `async void` semantics.
- Command state remains correct during nested async operations.
- Async exceptions are captured and shown safely.
- View-model behavior is regression-tested.

## 5) Non-Goals

- Replacing the app with a third-party MVVM framework.
- Reworking unrelated business logic in the same change.

## 6) Affected Files

| File | Change |
|------|--------|
| `BHGKeyMan.App/RelayCommand.cs` | Replace or supplement with async-aware command support. |
| `BHGKeyMan.App/MainViewModel.cs` | Migrate command creation and busy-state handling. |
| `BHGKeyMan.Core.Tests/` or new app test project | Add view-model tests for command state and failures. |

## 7) Acceptance Criteria

1. No command in `MainViewModel` uses an async lambda through `Action`.
2. `SetSecretAsync()` does not re-enable related buttons until the refresh flow has finished.
3. Async command failures do not escape as unhandled dispatcher exceptions.
4. Automated tests cover command-state transitions.

## 8) Risks and Mitigations

- **Risk:** Command refactoring touches most user actions.
  **Mitigation:** Migrate one command at a time and add tests before broader cleanup.

- **Risk:** Busy-state logic becomes more complex.
  **Mitigation:** Keep state ownership local to the command implementation instead of scattering `_isBusy` rules.

## 9) Test Plan

- Unit test async command `CanExecute` transitions.
- Unit test nested set-then-refresh workflow state.
- Manual test repeated button clicks during load/get/set/launch flows.
