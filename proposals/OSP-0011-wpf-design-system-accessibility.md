# OpenSpec Proposal: WPF Design System and Accessibility Refresh

## 1) Proposal Metadata
- **Proposal ID:** OSP-0011
- **Title:** WPF Design System and Accessibility Refresh
- **Status:** Draft
- **Authors:** BHGKeyMan contributors
- **Last Updated:** 2026-03-12
- **Priority:** Medium
- **Code Review Finding:** `codereview.md` finding 6
- **Primary References:**
  - `codereview.md`
  - `BHGKeyMan.App/MainWindow.xaml`
  - `BHGKeyMan.App/App.xaml`
  - `spec.md` section 13

## 2) Problem Statement

The current WPF interface is functional but visually basic and not yet production-grade:
- fixed three-column desktop layout
- hardcoded colors, borders, and font sizes
- limited visual hierarchy
- weak accessibility metadata
- emoji-based reveal button instead of a consistent icon system

This makes the product feel like a prototype and leaves accessibility gaps for keyboard and assistive-technology users.

## 3) Proposed Solution

### 3.1 Introduce shared visual resources

Move colors, spacing, typography, borders, and button styles into `App.xaml` resources so the UI has a coherent design system.

### 3.2 Rework the layout for smaller displays

Replace the fixed desktop-first composition with a layout that can stack major sections vertically when width is constrained.

### 3.3 Improve accessibility

Add:
- `AutomationProperties.Name`
- clear focus visuals
- accessible labels for buttons and inputs
- empty states and loading states

### 3.4 Replace emoji-driven controls

Use a proper icon asset or vector path for reveal/hide controls instead of relying on emoji glyph rendering.

### 3.5 Improve user feedback

Promote status into clear success/error/info treatments, and add section descriptions so the interface communicates intent instead of relying only on borders.

## 4) Goals

- The app looks intentional and consistent.
- The layout works on smaller desktop window sizes.
- Primary controls are accessible to keyboard and assistive-technology users.
- The UI better communicates trust and safety for secret operations.

## 5) Non-Goals

- Rebranding the product from scratch.
- Implementing a full custom control library.

## 6) Affected Files

| File | Change |
|------|--------|
| `BHGKeyMan.App/App.xaml` | Add shared styles, brushes, spacing, and icon resources. |
| `BHGKeyMan.App/MainWindow.xaml` | Redesign layout, accessibility metadata, and status surfaces. |
| `BHGKeyMan.App/MainWindow.xaml.cs` | Minimal code-behind only if required for adaptive behavior. |

## 7) Acceptance Criteria

1. Hardcoded visual values in `MainWindow.xaml` are substantially reduced in favor of shared resources.
2. The window remains usable below the current `MinWidth` threshold.
3. Primary controls have automation names and clear focus behavior.
4. Reveal/hide uses a consistent icon approach instead of emoji glyphs.

## 8) Risks and Mitigations

- **Risk:** Visual refresh can create churn while core security work is still active.
  **Mitigation:** Keep the first pass scoped to layout, resources, and accessibility rather than broad feature changes.

## 9) Test Plan

- Manual resizing test across narrow and wide window sizes.
- Keyboard-only navigation pass.
- Accessibility inspection using Windows automation tools.
- Visual regression check for status/error/success states.
