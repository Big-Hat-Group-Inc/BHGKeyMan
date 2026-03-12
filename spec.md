# BHGKeyMan Application Specification

## 1. Purpose
Define an implementation-ready specification for a Windows 11 Cloud PC desktop application that lets end users interactively authenticate and use Azure Key Vault to store and retrieve secrets securely.

## 2. Source Basis
This specification is derived from:
- `ApplicationResearch.md`
- `README.md`

## 3. Product Scope
### 3.1 In Scope
- Interactive desktop app (C# WPF, MVVM) for secret retrieval and secret set/update.
- Authentication to Azure using Microsoft Entra ID user sign-in for interactive use.
- Azure Key Vault data-plane operations for secrets.
- Role-based authorization model using Azure RBAC.
- Optional secure local caching strategy (in-memory first, optional DPAPI-protected cache).
- User workflow to launch external tools with process-scoped environment variables.
- Operational scripts to provision and configure Azure resources and access controls.

### 3.2 Out of Scope
- Building a generic configuration store.
- Storing plaintext secrets in source files or long-lived `.env` files.
- Full vault-wide backup/restore automation as a default path.

## 4. Users and Roles
### 4.1 End Users (Secret Consumers)
- Sign in interactively.
- List permitted secrets/metadata.
- Retrieve secret values.
- Launch local tools with process-scoped environment variables.

### 4.2 Secret Operators
- All consumer capabilities.
- Set/update secret values (version-creating writes).

### 4.3 Administrators
- Provision vault/network/security controls.
- Assign RBAC roles.
- Configure diagnostic logging and lifecycle policy.

## 5. Functional Requirements
### FR-1 Authentication
- The app SHALL support interactive Entra user authentication suitable for WPF.
- The app SHOULD expose signed-in identity context (tenant/account) in the UI.
- The app SHALL support re-authentication when token/session issues occur.

### FR-2 Secret Retrieval
- The app SHALL retrieve the latest secret version when no version is provided.
- The app SHALL show clear errors for not found, forbidden, authentication, and connectivity failures.

### FR-3 Secret Write/Update
- The app SHALL allow authorized users to set/update secret values.
- The app SHALL reflect Key Vault versioning behavior (set on existing name creates new version).

### FR-4 Secret Listing and Search
- The app SHALL list accessible secret metadata.
- The app SHOULD support filtering/search by secret name.

### FR-5 Environment Variable Export
- The app SHALL support launching a child process with selected secrets mapped to process-scoped environment variables.
- The app SHALL NOT permanently write secret values to user-level/system-level environment variables by default.

### FR-6 Clipboard Handling
- The app SHALL provide explicit copy-to-clipboard behavior.
- The app SHOULD support auto-clear clipboard timeout.

### FR-7 Cache
- The app SHALL use in-memory caching for retrieved secrets.
- Cache TTL default SHOULD align with research guidance baseline (8-hour starting point), configurable by policy.
- Optional: DPAPI-protected local cache MAY be enabled for offline/dev convenience.

### FR-8 Audit-Friendly UX
- The app SHALL display operation outcomes without displaying secret contents in logs.
- The app SHOULD surface operation timestamps and user actions at a high level.

## 6. Security Requirements
### SR-1 Authorization Model
- Use Azure RBAC for Key Vault data plane as the primary authorization model.
- Separate personas by least privilege:
  - Read path: Key Vault Secrets User.
  - Write path: Key Vault Secrets Officer.
  - Metadata-only scenarios: Key Vault Reader.

### SR-2 Secret Handling
- Secret values SHALL be treated as sensitive in memory and UI.
- Secret values SHALL NOT be written to plaintext logs.
- Secret values SHALL NOT be persisted in plaintext local files.

### SR-3 Network Posture
- Preferred posture: private endpoint-only Key Vault access with public network access disabled.
- If private endpoint is unavailable, firewall-restricted access model SHALL be documented and enforced.

### SR-4 Resilience and Throttling
- Implement retry with exponential backoff for transient failures and throttling conditions.
- Handle HTTP 429 and transient service/network failures with bounded retries and user feedback.

### SR-5 Lifecycle and Safety Controls
- Enable soft delete and purge protection on vaults.
- Support secret rotation-aware refresh behavior.

## 7. Non-Functional Requirements
- Platform: Windows 11 Cloud PC (Windows 365 target environment).
- Performance: retrieval UX should remain responsive under normal API latency.
- Reliability: app should degrade gracefully under auth/network/rbac faults.
- Maintainability: MVVM architecture with dependency-injected service boundaries.
- Observability: diagnostic logs with no secret-value leakage.

## 8. Solution Architecture
### 8.1 Client Application
- WPF desktop app using MVVM.
- Suggested core components:
  - `ISecretStore` abstraction.
  - Key Vault implementation using Azure SDK `SecretClient`.
  - Auth provider abstraction (interactive user as baseline).
  - Cache service (memory + optional DPAPI layer).
  - Process launcher service for scoped env var injection.

### 8.2 Cloud Dependencies
- Azure Key Vault (Secrets).
- Microsoft Entra ID.
- Azure RBAC assignments for data-plane roles.
- Optional network hardening components:
  - Key Vault private endpoint.
  - Private DNS zone for `privatelink.vaultcore.azure.net`.

## 9. Data and Naming Rules
- Secret names must follow Key Vault naming constraints identified in research.
- Define a project naming convention for secrets using lowercase and hyphen separators.
- Maintain mapping metadata between secret names and environment variable names for tool launch.

Recommended initial mapping set is the inventory in `ApplicationResearch.md`.

## 10. Error Handling Specification
Map common failures to actionable UX:
- Authentication/session failure: request re-sign-in.
- Forbidden (RBAC): indicate insufficient permissions and required role.
- Not found: indicate secret missing.
- Connectivity/DNS/private endpoint: provide network guidance.
- Throttling/transient: retry automatically, then show retriable failure message.

## 11. Required Scripts (Provisioning and Operations)
Implement scripts in `/scripts` to standardize setup.

### 11.1 `scripts/01-prereqs-check.ps1`
Purpose:
- Validate local prerequisites for operators (Azure CLI availability, login state, subscription context).

Outputs:
- Pass/fail summary and remediation instructions.

### 11.2 `scripts/02-provision-keyvault.ps1`
Purpose:
- Provision resource group and Key Vault for the application environment.
- Enable safety controls (soft delete and purge protection settings).

Parameters:
- Subscription ID, tenant ID, location, resource group, vault name, tags.

### 11.3 `scripts/03-configure-network.ps1`
Purpose:
- Configure preferred network posture:
  - Private endpoint path and required DNS integration.
- Alternate mode (if approved): firewall-restricted public endpoint path.

### 11.4 `scripts/04-assign-rbac.ps1`
Purpose:
- Assign Key Vault data-plane roles to users/groups/service principals by persona.

Parameters:
- Principal identifiers, role type (Reader/Secrets User/Secrets Officer), scope.

### 11.5 `scripts/05-seed-secrets.ps1`
Purpose:
- Seed initial non-production secret placeholders from secure operator input.
- Validate secret naming rules before write.

### 11.6 `scripts/06-verify-access.ps1`
Purpose:
- Validate end-to-end read/write behavior for assigned principals.
- Verify expected deny behavior for least-privilege test accounts.

### 11.7 `scripts/07-enable-diagnostics.ps1`
Purpose:
- Enable Key Vault diagnostic logging and route to configured sink.

## 12. Application Configuration
Provide a local, non-secret configuration file (example: `appsettings.json`) containing:
- `KeyVaultUri`
- `TenantId`
- `ClientId` (for public client registration when required)
- Cache policy settings (TTL, DPAPI cache enable/disable)
- Feature toggles (clipboard auto-clear, process launch policy)

No secret values may be stored in this file.

## 13. UI Specification (Minimum)
Minimum screens/views:
1. **Sign-In / Session Status**
   - Current identity, tenant, sign-in/out controls.
2. **Secrets Browser**
   - List/filter secret names and metadata.
3. **Secret Detail**
   - Retrieve latest value (explicit reveal action).
   - Copy value action with optional auto-clear.
4. **Set/Update Secret**
   - Name/value input with validation.
5. **Tool Launch**
   - Select secret-to-env mappings and launch configured executable.
6. **Operations Log (Non-sensitive)**
   - Action results without secret contents.

## 14. Testing and Validation Requirements
### 14.1 Unit Tests
- Secret name validation.
- Cache expiration logic.
- Error mapping and retry decision logic.
- Env var injection behavior for child processes.

### 14.2 Integration Tests
- Live Key Vault read latest version.
- Set secret creates retrievable latest version.
- RBAC enforcement for read-only vs write-enabled identities.
- Network-path validation (private endpoint scenario if enabled).

### 14.3 Security Tests
- Verify no secret values in logs.
- Verify no plaintext persistence when DPAPI cache is disabled.
- Verify DPAPI-protected cache cannot be read without user context.

## 15. Delivery Plan
### Phase 1: Foundations
- Project scaffolding (WPF + MVVM + DI).
- Interactive auth and baseline secret read.

### Phase 2: Core Features
- Secret list, get, set/update.
- Cache layer and robust error handling.

### Phase 3: Operationalization
- Provisioning/ops scripts.
- RBAC/network hardening workflows.
- Diagnostics and runbook documentation.

### Phase 4: Hardening
- Security testing, UX hardening (clipboard controls), and release packaging.

## 16. Acceptance Criteria
- End user can sign in interactively and retrieve authorized secrets.
- Authorized operator can set/update secrets and retrieve latest values.
- Least-privilege model is enforced via RBAC role assignments.
- Application does not leak secret values to logs or plaintext local storage by default.
- Provisioning and validation scripts can stand up and verify a usable Key Vault configuration for one or more users.
