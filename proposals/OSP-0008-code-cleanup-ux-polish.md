# OpenSpec Proposal: Code Cleanup and UX Polish

## 1) Proposal Metadata
- **Proposal ID:** OSP-0008
- **Title:** Delete Dead Code, Improve Command State, and Clarify Shared Bindings
- **Status:** Draft
- **Authors:** BHGKeyMan contributors
- **Last Updated:** 2026-03-11
- **Priority:** Low
- **Code Review Findings:** #11, #13, #14
- **Primary References:**
  - `codereview.md` findings 11, 13, 14
  - `BHGKeyMan.Core/Class1.cs`
  - `BHGKeyMan.App/RelayCommand.cs`
  - `BHGKeyMan.App/MainWindow.xaml`
  - `BHGKeyMan.App/MainViewModel.cs`

## 2) Problem Statement

Three low-severity issues reduce code quality and UX polish:

1. **Dead code (Finding #11):** `BHGKeyMan.Core/Class1.cs` is an empty 0-byte file left over from project scaffolding.

2. **Commands always enabled (Finding #13):** `RelayCommand.CanExecuteChanged` is never raised. All commands are always enabled, even during async operations. This creates a poor UX — users can click "Get Secret" repeatedly during a fetch, or click "Launch" while secrets are being resolved.

3. **Shared secret name binding (Finding #14):** `SecretNameInput` is bound to two separate `TextBox` controls (Retrieve panel line 50, Set panel line 69). Changing the name in either panel changes both. This may confuse users who expect the Retrieve and Set panels to operate independently.

## 3) Proposed Solution

### 3.1 Delete Dead Code

Delete `BHGKeyMan.Core/Class1.cs`.

### 3.2 Command State Management

Enhance `RelayCommand` to support `CanExecute` state changes:

```csharp
public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => _execute();

    public event EventHandler? CanExecuteChanged;

    public void RaiseCanExecuteChanged() =>
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
```

Wire `CanExecute` predicates to commands in `MainViewModel`:

| Command | Disabled When |
|---------|---------------|
| `GetSecretCommand` | `SecretNameInput` is empty or a fetch is in progress |
| `SetSecretCommand` | `SecretNameInput` or `SecretValueInput` is empty or a set is in progress |
| `CopySecretCommand` | `RetrievedSecretValue` is null/empty |
| `LaunchProcessCommand` | `LaunchExecutablePath` is empty or a launch is in progress |
| `LoadSecretsCommand` | A load is in progress |

Call `RaiseCanExecuteChanged()` when the relevant properties change.

### 3.3 Separate Secret Name Inputs

Split `SecretNameInput` into two independent properties:

- `RetrieveSecretName` — bound to the Retrieve panel TextBox.
- `SetSecretName` — bound to the Set panel TextBox.

Update commands accordingly:
- `GetSecretCommand` uses `RetrieveSecretName`.
- `SetSecretCommand` uses `SetSecretName`.

Optionally add a "Use same name" checkbox or auto-populate `SetSecretName` when a secret is selected from the list, if the shared-context behavior is desirable in some workflows.

## 4) Goals
- No dead code files in the project.
- Commands reflect valid/invalid state, preventing invalid user actions.
- Retrieve and Set panels have independent secret name inputs.
- Better UX feedback through disabled buttons.

## 5) Non-Goals
- Full command infrastructure refactor (e.g., `AsyncRelayCommand` from CommunityToolkit).
- Visual feedback beyond button enabled/disabled state (e.g., spinners, progress bars).

## 6) Affected Files

| File | Change |
|------|--------|
| `BHGKeyMan.Core/Class1.cs` | **Delete.** |
| `BHGKeyMan.App/RelayCommand.cs` | Add `Func<bool>? canExecute` parameter and `RaiseCanExecuteChanged()`. |
| `BHGKeyMan.App/MainViewModel.cs` | Add `canExecute` predicates to commands. Split `SecretNameInput` into `RetrieveSecretName` and `SetSecretName`. Call `RaiseCanExecuteChanged()` on property changes. |
| `BHGKeyMan.App/MainWindow.xaml` | Update Retrieve panel binding to `RetrieveSecretName`; update Set panel binding to `SetSecretName`. |

## 7) Acceptance Criteria

1. `Class1.cs` does not exist in the project.
2. `GetSecretCommand` is disabled when the retrieve secret name field is empty.
3. `SetSecretCommand` is disabled when either the set secret name or value field is empty.
4. `CopySecretCommand` is disabled when no secret value has been retrieved.
5. Changing the secret name in the Retrieve panel does not change the name in the Set panel (and vice versa).
6. All existing tests pass; `dotnet build` produces 0 warnings, 0 errors.

## 8) Risks and Mitigations

- **Risk:** Users accustomed to the shared name behavior may be confused by independent inputs.
  **Mitigation:** When a secret is selected from the list, auto-populate both fields. The user can then modify either independently.

- **Risk:** Adding `canExecute` to many commands increases the number of `RaiseCanExecuteChanged` calls.
  **Mitigation:** Group raises in the property setters — a single call per property change. Performance is not a concern at this scale.

## 9) Test Plan

- Build verification: `dotnet build` succeeds with `Class1.cs` deleted.
- Unit test: `RelayCommand` with `canExecute = () => false` → `CanExecute` returns `false`.
- Unit test: `RaiseCanExecuteChanged` fires the event.
- Manual test: Verify buttons are disabled/enabled based on input state.
- Manual test: Verify Retrieve and Set name fields are independent.
