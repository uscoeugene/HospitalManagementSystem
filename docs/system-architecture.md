# System Architecture

This document describes the overall system logic for the HMS system, tenancy, authentication, sync, subscriptions, outbox, push notifications and how UI should interact with the APIs. Use this as the canonical reference when building UI and services.

## 0. Single source of truth rule

- `AuthDbContext` is the single source of truth for identity, roles, permissions, tenants, tenant subscriptions, tenant nodes, refresh tokens, and auth audit data.
- `HmsDbContext` is the single source of truth for operational hospital data: patients, visits, invoices, prescriptions, lab requests, payments, profiles, outbox messages, and related clinical/business records.
- The UI must not maintain a separate identity store. It stores authentication/session material only in cookies and always validates permissions through API-issued claims.
- Local/offline user tables, local role/permission tables, and local login endpoints are not part of the active architecture. Do not add or depend on `LocalUser`, `/localauth/login`, or locally-issued auth tokens unless the architecture is deliberately revised.
- Tenant-scoped writes must use the tenant resolved by middleware or authenticated claims. Client-provided tenant values are compatibility inputs only and must not override a trusted server-resolved tenant.

## 1. High-level boundaries

- Identity and Tenant Service
  - Hosts `AuthDbContext`.
  - Stores users, roles, permissions, tenants, tenant subscriptions, tenant nodes, refresh tokens, audit info.
  - Handles billing webhooks, subscription management and issues JWTs for human users.
  - Acts as the canonical source of truth for tenant, subscription, and authorization state.

- Hospital Operations Service
  - Hosts `HmsDbContext`.
  - Stores operational data: patients, visits, invoices, prescriptions, payments, user profiles, outbox, and other hospital domain entities.
  - Does not own identity, roles, permissions, or tenant subscription authority.

## 2. Authentication & Authorization

- Authentication
  - Login uses `POST /auth/login`.
  - On success, the API issues a signed JWT containing tenant and permission claims derived from `AuthDbContext`.
  - Refresh tokens are also owned by `AuthDbContext`.
  - The API may accept legacy tenant headers/body values for compatibility, but a tenant resolved by middleware or trusted claims takes precedence.

- Permissions
  - Permission-based policies are enforced via `[HasPermission("code")]` attribute and the `PermissionAuthorizationHandler` which checks `permission` claims in the JWT.
  - Role and permission changes must be made through the auth/role APIs backed by `AuthDbContext`.

## 3. Tenancy

- `TenantId` is the single source of truth for tenant scoping.
- Middleware (`HybridTenantMiddleware` and `CurrentUserMiddleware`) sets an ambient `CurrentTenantAccessor.CurrentTenantId` from trusted request context such as host/domain, authenticated claims, or compatibility headers.
- EF global query filters apply tenant scoping to entities derived from `BaseEntity` that expose `TenantId`.

## 4. Subscriptions

- Central holds `TenantSubscription` records with `Plan`, `Status`, `StartAt`, `EndAt`, etc.
- Middleware checks `ITenantSubscriptionService.IsTenantAllowedAsync` and returns HTTP 402 (Payment Required) for requests from tenants with inactive subscriptions.
- Billing provider webhooks are received at `/subscriptions/webhook` (signed) and update the central subscription records.

## 5. Sync and Outbox

- Nodes push operational changes and pull authoritative shared changes via `SyncManager` and `ICloudSyncClient`.
  - `BackgroundSyncService` runs periodically and calls `SyncManager.RunOnceAsync()` to push and pull data.
  - The manager pushes unsynced operational records and pulls authoritative changes since a time window.
  - Sync must not create an alternative source of truth for identity, roles, permissions, or local login.

- Outbox pattern
  - Local changes that need to be published (e.g., events for other systems) are saved to `OutboxMessage`.
  - `OutboxProcessor` publishes messages (using `IEventPublisher`), notifies in-process listeners, broadcasts via SignalR and may push to tenant nodes.

- Push notifier
  - The central service can push subscription or important events to registered tenant nodes using `TenantNode.CallbackUrl`.
  - Each node has a `CallbackSecret` (Base64). Central signs JSON pushes with HMAC-SHA256 and sets `X-Central-Signature` header.
  - Nodes must verify this signature before applying changes.

- Manual sync
  - Admins can trigger a tenant-scoped sync on a node via `POST /sync/tenant/{tenantId}/sync-now`.
  - `ISyncManager.RunOnceAsync(Guid tenantId)` performs tenant-scoped pull/push for supported records such as subscriptions and profiles.

## 6. User management

- Users, roles, permissions, password hashes, refresh tokens, and auth audit records are managed through APIs backed by `AuthDbContext`.
- Do not introduce `LocalUser`, local role/permission caches, or separate local password hashes in `HmsDbContext`.
- User profile data may live in `HmsDbContext`, but it is profile/clinical staff metadata, not authentication authority.

## 7. Security practices

- JWT signing keys
  - Tokens are signed with `Jwt:Key`. Rotate and keep secure.
  - `LocalJwt:Key` is legacy compatibility configuration only unless local auth is deliberately reintroduced.

- Webhooks & push
  - Use HMAC signatures (shared callback secret) for push and webhook verification.
  - Validate signatures using fixed-time comparisons.

- Storage
  - Passwords must be hashed (use `IPasswordHasher`). Never store plaintext.
  - Use Secure, HttpOnly cookies for JWTs in the browser.

## 8. UI interaction patterns

- Login
  - UI should authenticate through `POST /auth/login`.
  - After successful login, UI stores JWT in Secure HttpOnly cookie and a non-HttpOnly `tenantId` cookie for client-side logic (short-lived).

- Sync status & management
  - UI shows online/offline status via `navigator.onLine` and by periodically pinging an API health endpoint.
  - Provide a user-facing `Sync now` action that triggers `POST /sync/tenant/{tenantId}/sync-now` on the node.

- Retrying operations
  - For retryable operational actions, rely on explicit API retry behavior or server-side `SyncManager` where configured.
  - Do not queue or replay identity, role, permission, or login changes through a separate local identity store.

## 9. Deployment & operational notes

- Databases
  - Central service runs `AuthDbContext` migrations and database.
  - Each hospital node runs `HmsDbContext` migrations and local DB instance.

- Backups & rotation
  - Securely back up both central and local DBs.
  - Rotate keys and node secrets periodically.

- Monitoring
  - Monitor outbox queues, failed push attempts, last sync times and webhook processing errors.
  - Auth, tenant resolution, and sync failures must be logged. Avoid silent `catch { }` blocks in these paths.

## 10. Extensibility points

- Add a node registration workflow where nodes automatically register callback URLs and exchange secrets.
- Add fine-grained feature flags in `TenantSubscription.MetadataJson`.
- Add centralized policy management and permission administration workflows in `AuthDbContext`.

---

Keep this document as the canonical reference for UI and API implementations. When building pages, refer to the endpoints listed here and preserve the single-source-of-truth boundaries above.
