# Implementation Plan

Date: 2026-03-12

Source inputs:
- `codereview.md`
- `proposals/README.md`
- `proposals/OSP-0003-async-ui-thread-safety.md`
- `proposals/OSP-0004-fail-fast-configuration.md`
- `proposals/OSP-0005-error-handling-hardening.md`
- `proposals/OSP-0006-secret-display-security.md`
- `proposals/OSP-0009-auth-operational-hardening.md`
- `proposals/OSP-0010-launch-policy-guardrails.md`
- `proposals/OSP-0011-wpf-design-system-accessibility.md`

## Phase 1: Core Safety and Validation

1. Harden `AppConfig`
   - validate `TenantId` and `ClientId` with `Guid.TryParse` in Azure mode
   - add launch policy settings for executable allowlists, environment variable allowlists, and feature toggle
2. Add user-safe error mapping
   - centralize exception-to-message translation in the app layer
   - replace direct `ex.Message` presentation in the main workflow
3. Harden process launch
   - enforce allowed executable paths and allowed environment variable names
   - block known high-risk environment variable names by default
   - surface precise but safe policy rejection messages
4. Harden verification script
   - use a unique temporary secret name when one is not supplied
   - avoid command-line `--value` secret injection
   - delete the verification secret in cleanup

Verification:
- unit tests for config validation and launcher policy
- script review for cleanup path and stdin-based secret set

## Phase 2: Command and Secret Workflow Refactor

1. Replace `RelayCommand` usage with async-aware command support
2. remove shared `_isBusy` race conditions
3. minimize secret value lifetime in the view model
4. change set/update secret entry to a safer input flow
5. keep secret reveal masked by default and clear state after timeouts where practical

Verification:
- view-model command-state tests
- manual sanity pass for load/get/set/launch flows

## Phase 3: WPF UX and Accessibility Refresh

1. move shared brushes, spacing, and button styles into `App.xaml`
2. redesign `MainWindow.xaml` for stronger hierarchy and smaller-width resilience
3. replace emoji reveal affordance with a consistent text/icon treatment
4. add automation properties and clearer status presentation

Verification:
- manual resize pass
- keyboard navigation pass

## Phase 4: Final Verification

1. run `dotnet test BHGKeyMan.slnx`
2. review resulting diffs for proposal alignment
3. update any stale documentation references if implementation diverges from proposal wording

## Delivery Order

1. Phase 1
2. Phase 2
3. Phase 3
4. Phase 4
