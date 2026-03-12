# OpenSpec Proposal: User-Safe Error Mapping and Diagnostics

## 1) Proposal Metadata
- **Proposal ID:** OSP-0005
- **Title:** User-Safe Error Mapping and Diagnostics
- **Status:** Draft
- **Authors:** BHGKeyMan contributors
- **Last Updated:** 2026-03-12
- **Priority:** Medium
- **Code Review Finding:** `codereview.md` finding 7
- **Primary References:**
  - `codereview.md`
  - `BHGKeyMan.App/MainViewModel.cs`
  - `BHGKeyMan.App/App.xaml.cs`

## 2) Problem Statement

The UI currently surfaces raw exception messages from authentication, file access, Azure SDK operations, and startup parsing. That exposes implementation detail and produces inconsistent user experience.

For a secrets application, production-facing messages should be user-safe, while diagnostic detail should go to logs or developer-only tracing.

## 3) Proposed Solution

### 3.1 Add error mapping

Map common failures into stable categories:
- authentication failure
- authorization failure
- secret not found
- configuration error
- connectivity or transient service error
- process launch failure

Each category should have:
- user-facing message
- optional operator hint
- diagnostic detail for logs only

### 3.2 Centralize presentation

Create a small error translation helper or service used by:
- `App.xaml.cs`
- `MainViewModel`

### 3.3 Add structured diagnostics

Introduce logging of sanitized technical detail without secret values. This may be debug-only initially, but the design should support future production logging.

## 4) Goals

- End users see clear, safe, actionable messages.
- Raw exception strings are no longer shown directly in the main UI.
- Diagnostic detail remains available to developers and operators.

## 5) Non-Goals

- Building a full telemetry platform.
- Internationalization of error messages.

## 6) Affected Files

| File | Change |
|------|--------|
| `BHGKeyMan.App/MainViewModel.cs` | Replace direct `ex.Message` usage with mapped messages. |
| `BHGKeyMan.App/App.xaml.cs` | Sanitize startup/config errors shown in message boxes. |
| `BHGKeyMan.App/` | Add an error mapping helper/service. |

## 7) Acceptance Criteria

1. No primary user message is built directly from raw `Exception.Message`.
2. Known error classes are translated into user-safe guidance.
3. Technical detail is available through diagnostics without secret values.

## 8) Risks and Mitigations

- **Risk:** Over-sanitizing messages makes troubleshooting harder.
  **Mitigation:** Keep developer diagnostics available alongside user-safe summaries.

## 9) Test Plan

- Unit test error translation for auth, not found, unauthorized, and config failures.
- Manual test that the UI no longer exposes Azure SDK raw messages directly.
