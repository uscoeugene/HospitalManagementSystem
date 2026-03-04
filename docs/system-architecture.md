# System Architecture

This document describes the overall system logic for the HMS system (central + local/offline), tenancy, authentication, sync, subscriptions, outbox, push notifications and how UI should interact with the APIs. Use this as the canonical reference when building UI and services.

## 1. High-level boundaries

- Central (Online) Service
  - Hosts `AuthDbContext`.
  - Stores central users, roles, permissions, tenants, tenant subscriptions, tenant nodes, refresh tokens, audit info.
  - Handles billing webhooks, subscription management and issues central JWTs for human users.
  - Acts as the canonical source-of-truth for tenant and billing state.

- Local (Hospital) Service
  - Hosts `HmsDbContext`.
  - Stores operational data: patients, visits, invoices, prescriptions, payments, local user cache, local roles/permissions, outbox, and other domain entities.
  - Operates offline, allowing hospitals to continue working when connectivity to central is unavailable.

## 2. Authentication & Authorization

- Central authentication
  - Primary login against central endpoint `POST /auth/login`.
  - On success central issues a signed JWT containing `tenant_id` claim and `permission` claims depending on user roles.

- Local authentication
  - Local login endpoint `POST /localauth/login` exists for offline operation.
  - Local users are stored in `LocalUser` table in `HmsDbContext`.
  - Successful local login returns a locally-signed JWT (signed with `LocalJwt:Key`) which contains cached role and permission claims, enabling offline authorization.

- Permissions
  - Permission-based policies are enforced via `[HasPermission("code")]` attribute and the `PermissionAuthorizationHandler` which checks `permission` claims in the JWT.
  - Local JWTs embed cached `permission` claims so the same authorization model works offline.

## 3. Tenancy

- `TenantId` is the single source of truth for tenant scoping.
- Middleware (`CurrentUserMiddleware` and `TenantMiddleware`) sets an ambient `CurrentTenantAccessor.CurrentTenantId` from the active JWT or from `X-Tenant-Id` header.
- EF global query filters apply tenant scoping to entities derived from `BaseEntity` that expose `TenantId`.

## 4. Subscriptions

- Central holds `TenantSubscription` records with `Plan`, `Status`, `StartAt`, `EndAt`, etc.
- Middleware checks `ITenantSubscriptionService.IsTenantAllowedAsync` and returns HTTP 402 (Payment Required) for requests from tenants with inactive subscriptions.
- Billing provider webhooks are received at `/subscriptions/webhook` (signed) and update the central subscription records.

## 5. Sync and Outbox

- Local nodes push local changes to central and pull remote changes via `SyncManager` and `ICloudSyncClient`.
  - `BackgroundSyncService` runs periodically and calls `SyncManager.RunOnceAsync()` to push and pull data.
  - The manager pushes unsynced local records and pulls authoritative changes since a time window.

- Outbox pattern
  - Local changes that need to be published (e.g., events for other systems) are saved to `OutboxMessage`.
  - `OutboxProcessor` publishes messages (using `IEventPublisher`), notifies in-process listeners, broadcasts via SignalR and may push to tenant nodes.

- Push notifier
  - The central service can push subscription or important events to registered tenant nodes using `TenantNode.CallbackUrl`.
  - Each node has a `CallbackSecret` (Base64). Central signs JSON pushes with HMAC-SHA256 and sets `X-Central-Signature` header.
  - Nodes must verify this signature before applying changes.

- Manual sync
  - Admins can trigger a tenant-scoped sync on a node via `POST /sync/tenant/{tenantId}/sync-now`.
  - `ISyncManager.RunOnceAsync(Guid tenantId)` performs a tenant-scoped pull/push (subscriptions, local users, profiles).

## 6. Local user management & offline flow

- `LocalUser` stores locally-created accounts and password hashes.
- Local admins can create/manage users (`/localusers`) on the node even when offline.
- Local user changes are pushed to central during the next sync; central may also update local users which are pulled back to nodes.
- Local login issues a local JWT enabling offline authorization and usage.

## 7. Security practices

- JWT signing keys
  - Central tokens: `Jwt:Key` (central key). Rotate and keep secure.
  - Local tokens: `LocalJwt:Key` (node-local key). Keep secure and unique per node if desired.

- Webhooks & push
  - Use HMAC signatures (shared callback secret) for push and webhook verification.
  - Validate signatures using fixed-time comparisons.

- Storage
  - Passwords must be hashed (use `IPasswordHasher`). Never store plaintext.
  - Use Secure, HttpOnly cookies for JWTs in the browser.

## 8. UI interaction patterns & offline UX

- Login
  - UI should attempt central login first; if offline or central fails, offer local login fallback.
  - After successful login, UI stores JWT in Secure HttpOnly cookie and a non-HttpOnly `tenantId` cookie for client-side logic (short-lived).

- Sync status & management
  - UI shows online/offline status via `navigator.onLine` and by periodically pinging an API health endpoint.
  - Provide a user-facing `Sync now` action that triggers `POST /sync/tenant/{tenantId}/sync-now` on the node.

- Retrying operations
  - For administrative actions performed locally while offline, maintain a client-side retry queue (localStorage) or rely on server-side `SyncManager` to push changes.

## 9. Deployment & operational notes

- Databases
  - Central service runs `AuthDbContext` migrations and database.
  - Each hospital node runs `HmsDbContext` migrations and local DB instance.

- Backups & rotation
  - Securely back up both central and local DBs.
  - Rotate keys and node secrets periodically.

- Monitoring
  - Monitor outbox queues, failed push attempts, last sync times and webhook processing errors.

## 10. Extensibility points

- Add a node registration workflow where nodes automatically register callback URLs and exchange secrets.
- Add fine-grained feature flags in `TenantSubscription.MetadataJson`.
- Add role/permission sync with conflict resolution and centralized policy management.

---

Keep this document as the canonical reference for UI and API implementations. When building pages, refer to the endpoints listed here and follow the offline-first patterns: attempt central first, fallback to local and keep the user informed of network state and last sync time.