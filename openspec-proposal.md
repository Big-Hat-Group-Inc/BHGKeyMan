# OpenSpec Proposal: BHGKeyMan Interactive Key Vault Secret Manager

## 1) Proposal Metadata
- **Proposal ID:** OSP-0001
- **Title:** Interactive Azure Key Vault Secret Manager for Windows 11 Cloud PC
- **Status:** Draft
- **Authors:** BHGKeyMan contributors
- **Last Updated:** 2026-03-11
- **Primary References:**
  - `spec.md`
  - `ApplicationResearch.md`
  - `README.md`

## 2) Problem Statement
Developers and operators using AI tooling on Windows 11 Cloud PCs need a secure, repeatable way to store and retrieve secrets without putting sensitive values in plaintext files, source control, or long-lived local environment variables.

Current repository state is documentation-only; there is no implementation, no deployment automation, and no executable workflow for secure secret operations.

## 3) Proposed Solution
Build a WPF (MVVM) desktop application that supports interactive user sign-in with Microsoft Entra ID and data-plane secret operations against Azure Key Vault.

The solution includes:
1. Interactive user auth for human-in-the-loop workflows.
2. Secret list/get/set workflows with role-based authorization.
3. Process-scoped environment variable injection for launching local tools.
4. In-memory cache (default) with optional DPAPI-protected local cache.
5. PowerShell scripts for provisioning Key Vault, configuring network/security posture, assigning RBAC, seeding placeholders, and validating access.

## 4) Goals
- Deliver an interactive end-user workflow to retrieve and manage secrets in Azure Key Vault.
- Enforce least-privilege access using Azure RBAC roles.
- Avoid plaintext local persistence of secret values by default.
- Provide repeatable operational scripts to configure Key Vault for one or more users.
- Provide actionable error handling for auth, permission, throttling, and connectivity failures.

## 5) Non-Goals
- Implementing a generic configuration management platform.
- Making plaintext `.env` persistence a default behavior.
- Building full vault-wide backup/restore orchestration as a baseline requirement.

## 6) Users and Personas
- **Secret Consumer:** Reads secret values and launches tools with process-scoped env vars.
- **Secret Operator:** Performs read + set/update operations.
- **Administrator:** Provisions infrastructure, assigns RBAC, and configures diagnostics/network controls.

## 7) Functional Scope
### In Scope
- Interactive sign-in and session state display.
- Secret metadata list/filter.
- Read latest secret version.
- Set/update secret values (new versions).
- Clipboard copy flow with optional auto-clear.
- Tool launch with selected env var mappings.
- Provisioning and operations scripts under `/scripts`.

### Out of Scope (Initial)
- Multi-platform desktop/mobile clients.
- Unattended-only auth as the primary app mode.
- Organization-wide secret rotation automation for every downstream dependency.

## 8) Security and Compliance Constraints
- Use Azure RBAC for Key Vault data-plane authorization.
- Prefer private endpoint-only Key Vault network access with public access disabled.
- Enable soft delete and purge protection.
- Never log secret values.
- Do not store secret values in plaintext local files.
- Use exponential backoff retries for transient faults and throttling.

## 9) Proposed Architecture
### Client
- C# WPF + MVVM.
- Service abstractions:
  - `ISecretStore`
  - Authentication provider
  - Cache provider (memory + optional DPAPI layer)
  - Process launcher for scoped env var injection

### Cloud
- Azure Key Vault (Secrets)
- Microsoft Entra ID
- Azure RBAC role assignments:
  - Key Vault Secrets User
  - Key Vault Secrets Officer
  - Key Vault Reader (metadata-only scenario)

### Optional Hardening
- Private endpoint and private DNS (`privatelink.vaultcore.azure.net`) for data-plane resolution.

## 10) Operational Script Plan
Create PowerShell scripts:
1. `scripts/01-prereqs-check.ps1`
2. `scripts/02-provision-keyvault.ps1`
3. `scripts/03-configure-network.ps1`
4. `scripts/04-assign-rbac.ps1`
5. `scripts/05-seed-secrets.ps1`
6. `scripts/06-verify-access.ps1`
7. `scripts/07-enable-diagnostics.ps1`

Each script should provide:
- Parameterized input.
- Validation and clear failure messages.
- Idempotent behavior where practical.
- Safe defaults aligned with least privilege.

## 11) Delivery Milestones
### Milestone 1: Project Foundation
- Scaffold WPF solution and MVVM structure.
- Add baseline config and dependency injection.
- Implement interactive auth flow.

### Milestone 2: Secret Workflows
- Implement list/get/set secret operations.
- Add retry/error handling and cache behavior.
- Add process-scoped env var launch workflow.

### Milestone 3: Infrastructure Automation
- Implement `/scripts` provisioning + RBAC + diagnostics.
- Validate least-privilege behavior with test principals.

### Milestone 4: Hardening and Validation
- Security checks (no secret logging, no plaintext persistence).
- Integration tests in a non-production Azure environment.
- Documentation updates (`README.md`, `AGENTS.md`, runbook notes).

## 12) Acceptance Criteria
- Interactive user can authenticate and retrieve authorized secrets.
- Authorized operator can set/update secrets and retrieve latest values.
- Unauthorized actions are denied and surfaced clearly.
- App defaults do not persist secret values in plaintext.
- Scripts can configure a working Key Vault path for multiple users.

## 13) Risks and Mitigations
- **Risk:** Misconfigured RBAC blocks expected operations.  
  **Mitigation:** include scripted role assignment + verification script.
- **Risk:** Network/DNS issues with private endpoint routing.  
  **Mitigation:** explicit network setup script and connectivity diagnostics in verification.
- **Risk:** Token/session friction in interactive flows.  
  **Mitigation:** clear re-authentication UX and resilient auth error mapping.
- **Risk:** Secret leakage via logs or accidental local storage.  
  **Mitigation:** centralized redaction policy and security tests.

## 14) Dependencies
- Azure subscription and tenant access.
- Permissions to create/configure Key Vault and RBAC assignments.
- Windows 11 Cloud PC runtime environment.
- Azure CLI + PowerShell for provisioning scripts.

## 15) Open Questions
- Should optional DPAPI local cache be enabled by default or opt-in only?
- What tenant policy constraints apply to public client registration for interactive auth?
- Is private endpoint mandatory for all environments or only production?

## 16) Decision
**Proposed for implementation** as the first executable increment of BHGKeyMan, using `spec.md` as the normative engineering specification.
