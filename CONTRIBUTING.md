# Contributing Guidelines and Architecture Standards

## Purpose
This file documents mandatory project-wide architecture and contribution rules. These rules are enforced for all services in the solution and must be followed exactly.

## Architecture Principles
- Follow Clean Architecture: separate projects/layers for Domain, Application, Infrastructure, and API for each bounded context/service.
- Services are independent and communicate over HTTP APIs using JWT for authentication and authorization.
- No direct database relationships across services. Do not create foreign keys or shared tables referencing another service's database.

## Auth Service Responsibilities (MUST remain ONLY responsible for)
- Authentication (username/password, refresh tokens, etc.).
- Role management (create/assign roles).
- Permission management (create/assign permissions to roles).
- JWT issuance (access tokens and optional refresh tokens).

Auth Service MUST NOT store or manage user biodata, profile, or professional data. Auth tokens must include the canonical identifier (UserId GUID) and necessary claims (roles/permissions).

## User Profile Service (New) - Mandatory Rules
- Purpose: store personal and professional profile data for users (name, contact, address, professional credentials, preferences, etc.).
- Identity: The User Profile Service uses the UserId (GUID) issued by Auth Service as the sole identifier for user profiles. Do not generate separate primary identifiers for the same user; the profile primary key must be the UserId when persisted.
- No DB couples: Do NOT create database foreign keys or shared tables between Auth and Profile services. Communication occurs only via API + JWT.
- JWT usage: API endpoints must extract the UserId from validated JWT claims (e.g., claim type `sub` or `user_id`). Do not trust client-supplied identifiers; always derive identity from the validated token.
- Authorization: Use the existing permission-based authorization policy mechanism. Apply permission policies to profile endpoints to restrict actions (read, update, manage). Implement and register permission handlers consistent with the project's PermissionPolicyProvider and PermissionAuthorizationHandler.
- Offline-first support: Profile data must support offline-first patterns (local cache, delta sync, conflict resolution). The Profile service must expose sync endpoints (push/pull) and support sync metadata (UpdatedAt, Version, ChangeSet) to enable deterministic synchronization.
- Auditing: Profile changes must include audit metadata (CreatedAt, UpdatedAt, UpdatedByUserId) and be captured in domain events or change logs where appropriate.

## API Contract & Security
- All Profile API endpoints require JWT bearer tokens. The Profile API must validate the token signature and issuer using the same or compatible public keys used by Auth Service.
- Tokens must include role and permission claims. Profile service must rely on permission claims for policy evaluation; if necessary call Auth Service introspection endpoints for up-to-date permissions.
- The Profile service must expose endpoints for:
  - Get current user's profile (reads UserId from token)
  - Get profile by UserId (only when caller has appropriate permission)
  - Update profile (caller must be the profile owner or have manage permission)
  - Sync endpoints: Pull changes since timestamp/anchor, Push local changes (with conflict metadata)

## Data Model and Boundaries
- Profile domain models must be owned by the Profile service only.
- Keep profile aggregates small and cohesive. Avoid embedding authentication-related fields (password hash, roles) in profile entities.

## Communication Patterns
- Use JWT in Authorization header for all cross-service calls.
- Prefer idempotent endpoints for sync/push operations.
- For sensitive operations, the Profile service may optionally call Auth Service to validate permissions or resolve role changes, but this must be done via API — never via direct DB access.

## Testing and CI
- Integration tests must use isolated in-memory or test databases for each service. Do not rely on shared production DB instances.
- Tests must seed minimal Auth data required for token issuance and roles; the Profile service tests must use JWT tokens issued by a test instance of Auth Service or by generating tokens using the same signing key.

## Code Organization & Naming
- Projects should follow folder/project per layer convention: `ServiceName.Domain`, `ServiceName.Application`, `ServiceName.Infrastructure`, `ServiceName.API`.
- Public APIs and DTOs should be clearly versioned (e.g., `/api/v1/profiles`).

## Sync & Offline Considerations
- Sync endpoints should accept and return metadata necessary for deterministic merge (e.g., `etag`, `version`, `lastModified`, `conflictResolutionHints`).
- Profile service must support reconciliation strategies: last-write-wins (configurable), merge strategies per field, and server-side conflict handlers.

## Enforcement
- Code review must verify adherence to these rules. Pull requests that introduce DB-level coupling between services, or add profile data to the Auth service, will be rejected.

## Contact
If clarifications are needed, open an issue or discussion in the repository and tag the architecture owners.