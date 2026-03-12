# Code Review

Date: 2026-03-12

Scope:
- `BHGKeyMan.App`
- `BHGKeyMan.Core`
- `scripts/`

Review lenses used:
- Software engineer: security, reliability, maintainability, testability
- Frontend developer: WPF UI quality, accessibility, layout, visual design

Verification:
- `dotnet test BHGKeyMan.slnx` passed
- 48 tests passed

## Findings

### 1. High: secrets are kept in normal UI strings and rendered through standard text controls
Files:
- `BHGKeyMan.App/MainViewModel.cs:23`
- `BHGKeyMan.App/MainViewModel.cs:25`
- `BHGKeyMan.App/MainViewModel.cs:149`
- `BHGKeyMan.App/MainViewModel.cs:317`
- `BHGKeyMan.App/MainWindow.xaml:71`
- `BHGKeyMan.App/MainWindow.xaml:86`
- `BHGKeyMan.Core/DpapiSecretCacheStore.cs:20`

Why this matters:
- Secret values live in plain managed `string` instances in view-model state, are bound into regular `TextBox` controls, and are copied to the clipboard.
- This increases exposure in memory, UI automation, clipboard history tools, crash dumps, and local inspection.
- The DPAPI cache also serializes the full secret value before encryption, so plaintext still exists transiently in process memory.

Recommendation:
- Avoid binding editable secret entry to a normal `TextBox` when possible; use a `PasswordBox`-style flow or a custom secure input pattern.
- Minimize secret lifetime in memory and avoid storing retrieved values longer than needed.
- Treat clipboard copy as an explicit elevated action with stronger warning/confirmation and optional disablement by policy.
- Add tests around clipboard clearing and reveal timeout behavior.

### 2. High: the app can inject secrets into any executable and any environment variable name with no policy guardrails
Files:
- `BHGKeyMan.App/MainViewModel.cs:351`
- `BHGKeyMan.App/MainWindow.xaml:91`
- `BHGKeyMan.Core/EnvProcessLauncher.cs:8`

Why this matters:
- Any local executable path can be launched.
- Any environment variable name can be supplied, including sensitive process-shaping variables like `PATH`, `COMSPEC`, `DOTNET_*`, or proxy-related variables.
- For a secret-management tool, this is too permissive by default and creates an easy exfiltration path.

Recommendation:
- Add an allowlist for executable locations and environment variable names.
- Require explicit confirmation showing executable path, secret name, and target environment variable before launch.
- Log only non-secret audit metadata for launches.
- Consider disabling this feature in production mode unless explicitly enabled by configuration.

### 3. Medium: command execution is implemented with `async void` semantics and has command-state race conditions
Files:
- `BHGKeyMan.App/RelayCommand.cs:7`
- `BHGKeyMan.App/MainViewModel.cs:50`
- `BHGKeyMan.App/MainViewModel.cs:217`
- `BHGKeyMan.App/MainViewModel.cs:242`
- `BHGKeyMan.App/MainViewModel.cs:294`

Why this matters:
- `RelayCommand` accepts `Action`, but several commands pass `async` lambdas. In WPF this becomes `async void` behavior.
- That makes exception flow and completion tracking harder and can lead to subtle reentrancy issues.
- `SetSecretAsync()` calls `LoadSecretsAsync()` while both manipulate `_isBusy`, so the inner `finally` can re-enable commands before the outer operation is finished.

Recommendation:
- Replace `RelayCommand` with an async-aware command type, such as `AsyncRelayCommand`.
- Centralize busy-state management so nested operations do not toggle it incorrectly.
- Add view-model tests for command disable/enable transitions.

### 4. Medium: configuration validation claims to require valid GUIDs but does not actually validate GUID format
Files:
- `BHGKeyMan.Core/Models.cs:33`
- `BHGKeyMan.App/App.xaml.cs:89`
- `BHGKeyMan.Core.Tests/AppConfigTests.cs:16`

Why this matters:
- `AppConfig.Validate()` only rejects placeholder values and empty strings.
- Invalid tenant/client identifiers can pass validation and then fail later during authentication.
- The current test data labeled as “valid” Azure config is not even GUID-shaped, which hides the gap.

Recommendation:
- Validate `TenantId` and `ClientId` with `Guid.TryParse`.
- Update tests to use real GUID-format values and add negative tests for malformed IDs.

### 5. Medium: the access-verification script persists a real secret and exposes the test value on the command line
Files:
- `scripts/06-verify-access.ps1:17`
- `scripts/06-verify-access.ps1:21`

Why this matters:
- The script creates or updates a secret but never removes it, which leaves verification artifacts in the vault.
- Passing the value via `--value` puts it directly on the command line, which can leak through shell history, process inspection, or operational logging.

Recommendation:
- Use a unique temporary secret name for each run and delete it afterward.
- Prefer stdin-based secret input where supported, or at minimum document the exposure tradeoff clearly.

### 6. Medium: the WPF UI is functional but not production-grade from a UX or accessibility standpoint
Files:
- `BHGKeyMan.App/MainWindow.xaml:7`
- `BHGKeyMan.App/MainWindow.xaml:21`
- `BHGKeyMan.App/MainWindow.xaml:34`
- `BHGKeyMan.App/MainWindow.xaml:42`
- `BHGKeyMan.App/MainWindow.xaml:72`

Why this matters:
- The window is fixed to a desktop-first three-column layout with `MinWidth="900"`, which will feel cramped or unusable on smaller displays.
- Styling is mostly raw controls with hardcoded colors, borders, and font sizes instead of shared resources.
- The eye button uses an emoji glyph, which is inconsistent across systems and weak for accessibility.
- There are no obvious automation properties, focus cues, empty states, loading states, or visual hierarchy beyond borders.

Recommendation:
- Move colors, spacing, typography, and button styles into `App.xaml` resources.
- Rework the layout to adapt below tablet widths, likely stacking panels vertically.
- Replace the emoji reveal button with a real icon and accessible name.
- Add `AutomationProperties.Name` to primary actions and inputs.
- Add a proper header, section descriptions, empty states, and success/error visual treatments instead of only a status line.

### 7. Low: error messages are directly surfaced from exceptions, which can leak implementation detail
Files:
- `BHGKeyMan.App/MainViewModel.cs:229`
- `BHGKeyMan.App/MainViewModel.cs:258`
- `BHGKeyMan.App/MainViewModel.cs:286`
- `BHGKeyMan.App/MainViewModel.cs:309`
- `BHGKeyMan.App/MainViewModel.cs:376`
- `BHGKeyMan.App/App.xaml.cs:120`

Why this matters:
- Raw exception messages from Azure SDK, file IO, and auth flows are shown directly to the user.
- This is useful for development, but in production it can leak environmental details and produce poor UX.

Recommendation:
- Map known failures to user-safe messages.
- Send technical detail to structured logging instead of the primary UI.

## Strengths

- Secret-name validation is centralized and consistently reused.
- The split between app layer and core abstractions is clear and easy to extend.
- DPAPI caching is optional instead of always-on.
- Unit coverage on the core library is decent for a small solution.
- Azure Key Vault usage is wrapped behind an interface, which keeps the design testable.

## Notable Gaps

- No automated tests for the WPF view model or UI behaviors.
- No accessibility review artifacts.
- No visual design system or reusable style resources yet.
- No audit trail for launch-with-secret operations.

## Suggested Priority Order

1. Reduce secret exposure in the UI and process-launch flow.
2. Replace `RelayCommand` with an async-aware command implementation.
3. Harden configuration validation.
4. Fix the verification script so it does not leave secrets behind.
5. Refactor the WPF UI into a styled, accessible, responsive layout.
