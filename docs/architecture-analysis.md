# Architecture Analysis

This document captures a practical analysis of the Hospital Management System codebase so it can be referenced alongside the canonical architecture notes.

## Overview

The solution is organized into 3 main areas:

- `HMS.API`: backend API, business logic, persistence, auth, sync, and reporting
- `HMS.UI`: ASP.NET Core MVC frontend that consumes the API and renders Razor views
- `HMS.API.Tests` and `HMS.API.IntegrationTests`: unit and integration test projects

At a high level, the system is a layered monolith with modular business domains rather than a strict microservice architecture.

## Architecture

The intended architecture is documented clearly in `docs/system-architecture.md`:

- `AuthDbContext` is the source of truth for identity, roles, permissions, tenants, subscriptions, refresh tokens, and auth audit data
- `HmsDbContext` is the source of truth for operational data such as patients, visits, billing, pharmacy, lab, profiles, payments, and outbox messages

In practice, the codebase is split into these backend layers:

- `Domain/`: entities, enums, and value objects
- `Application/`: service layer, DTOs, interfaces, and module logic
- `Infrastructure/`: EF Core persistence, auth storage, sync, notifications, reporting, hosting, and outbox processing
- `Controllers/`: HTTP endpoints

The UI is a separate ASP.NET Core MVC application, but functionally it behaves like a server-rendered client/BFF:

- UI controllers call the API through `HMS.UI/Services/ApiClient.cs`
- API responses are mapped into view models
- Razor views and partials render the user interface

## Patterns Used

### 1. Layered Architecture

The API follows a layered structure with reasonably clear separation between domain, application, infrastructure, and transport concerns.

This is visible in:

- `HMS.API/Domain`
- `HMS.API/Application`
- `HMS.API/Infrastructure`
- `HMS.API/Controllers`

### 2. Service Layer Pattern

Most business logic lives in application services rather than controllers.

Examples:

- `HMS.API/Application/Patient/PatientService.cs`
- `HMS.API/Application/Billing/BillingService.cs`
- `HMS.API/Application/Lab/LabService.cs`
- `HMS.API/Application/Profile/ProfileService.cs`

This aligns well with the project rule to use a service layer pattern.

### 3. DTO-Based API Boundary

The system consistently uses DTOs between controllers and clients instead of exposing EF entities directly.

Examples:

- `HMS.API/Application/Lab/DTOs/LabDtos.cs`
- `HMS.API/Application/Patient/DTOs`
- `HMS.API/Application/Billing/DTOs`
- `HMS.API/Application/Auth/DTOs`

This improves API contract clarity and reduces direct coupling to persistence models.

### 4. Middleware for Cross-Cutting Concerns

Important runtime behavior is implemented through middleware:

- tenant resolution
- current user resolution
- API exception/response shaping
- subscription enforcement

Examples:

- `HMS.API/Middleware/HybridTenantMiddleware.cs`
- `HMS.API/Middleware/CurrentUserMiddleware.cs`
- `HMS.API/Middleware/ApiResponseMiddleware.cs`
- `HMS.API/Middleware/ApiResponseWrappingMiddleware.cs`

### 5. Claims and Permission-Based Authorization

Authorization is permission-driven rather than simple role checks.

Examples:

- `[HasPermission("code")]` attributes on API controllers
- custom policy provider and authorization handler in `HMS.API/Security`
- JWT permission claims issued during login

This is a good fit for hospital workflows where permissions are usually more granular than roles.

### 6. Multi-Tenancy via Ambient Tenant Context

The system uses:

- tenant resolution in middleware
- `CurrentTenantAccessor`
- EF Core global query filters on `BaseEntity` types with `TenantId`

This gives tenant scoping without repeating tenant filters in every query.

### 7. Outbox and Background Processing

The code includes outbox/eventing and sync-oriented patterns:

- `OutboxMessage`
- `OutboxProcessor`
- `BackgroundSyncService`
- `SyncManager`
- SignalR notification hub

This suggests the design is trying to support distributed or hybrid deployments, not just a simple single-node web app.

### 8. MVC UI with Server-Side Composition

The UI follows classic ASP.NET Core MVC patterns:

- controllers + views
- view models
- Razor partials
- TempData messages

This matches the project instructions well and is consistent with enterprise CRUD-style hospital systems.

## Strengths

- Clear separation between auth/tenant data and hospital operational data
- Good use of a service layer instead of placing business logic directly in controllers
- Strong DTO usage across modules
- Permission-based authorization is more flexible than role-only checks
- Tenant scoping is centralized rather than duplicated everywhere
- Presence of integration tests shows some attention to end-to-end behavior
- The UI and API separation makes future replacement or expansion easier

## Weaknesses

### 1. Boundary Drift Between Modules

The intended architecture is clean, but the actual implementation sometimes crosses boundaries.

Example:

- `AuthService` can create/update user profiles through profile services, which couples identity flow to operational/profile persistence

This is not catastrophic, but it weakens the purity of the split between auth and operational concerns.

### 2. Heavy Reliance on Ambient Tenant State

Tenant context depends on `CurrentTenantAccessor` using `AsyncLocal`.

This is convenient, but it can become fragile when:

- background services run outside normal request flow
- async flows become more complex
- tests do not fully reproduce middleware behavior

Ambient context is powerful, but it also makes correctness less obvious.

### 3. Tenant Resolution Trust Model Is Looser Than Ideal

The code accepts compatibility inputs such as forwarded host values and tenant headers.

Examples:

- `X-Tenant-Id`
- `X-Forwarded-Host`

This helps the UI and deployment flexibility, but it also increases security and correctness risk if request trust boundaries are not tightly controlled.

### 4. Global Response Wrapping Is Brittle

`ApiResponseWrappingMiddleware` rewrites successful JSON responses into a standard envelope.

That gives consistency, but it can create edge cases for:

- streaming
- file responses
- nonstandard JSON payloads
- controller logic expecting raw response behavior

This is a useful pattern, but middleware-based response rewriting can become hard to maintain.

### 5. Large Services

Some services have grown into multi-responsibility classes.

Examples:

- `PatientService`
- `BillingService`
- `LabService`

These classes contain validation, orchestration, persistence, business rules, mapping, and reporting-style enrichment. That makes them harder to test, reason about, and extend safely.

### 6. Large UI Controllers

Some MVC controllers contain a lot of orchestration and defensive fallback logic.

The clearest example is:

- `HMS.UI/Controllers/PatientsController.cs`

It handles:

- API orchestration
- view-model composition
- fallback parsing
- repeated error handling
- reflection-based mapping in some places

This makes the UI layer harder to maintain than it should be.

### 7. Silent Exception Handling

There are several `catch { }` blocks or broad catches that swallow failures.

This is one of the more important maintainability weaknesses because it can:

- hide real bugs
- make production diagnosis difficult
- create inconsistent UI behavior

This is especially risky in:

- tenant resolution
- enrichment logic
- billing side effects
- UI composition logic

### 8. Performance Risks from Extra Per-Item Queries

Some service methods perform enrichment lookups inside loops or per-item mapping.

Examples include:

- invoice list enrichment with patient and visit lookups
- lab request mapping with follow-up lookups

This may be acceptable for small datasets, but it is likely to degrade as records grow.

### 9. Security Rough Edges

A few areas deserve tightening:

- legacy-compatible token validation still exists through `LocalJwt`
- development HTTP client disables certificate validation
- some controller actions return internal exception details more directly than they should

These are manageable, but they should be cleaned up before production hardening.

### 10. Inconsistent Strictness Across the Codebase

Parts of the code are thoughtfully structured, while other parts are clearly pragmatic workarounds.

Examples:

- strong service layering in the API
- ad hoc fallback logic in UI controllers
- reflection-based property access in some UI paths
- response-envelope workarounds in the API client

This inconsistency is a sign that the architecture is good in direction, but under pressure in implementation.

## Overall Assessment

This is a solid, serious codebase with a better architectural foundation than many typical line-of-business ASP.NET systems.

Its strongest qualities are:

- layered organization
- service-based business logic
- explicit auth vs operations separation
- tenant-aware design
- extensibility toward sync, reporting, and hybrid deployment

Its main risk is not that the architecture is bad, but that the implementation is gradually accumulating convenience shortcuts:

- boundary leakage
- large services/controllers
- ambient context reliance
- hidden exception handling
- UI workarounds

The current architecture is good enough to grow, but the next quality step should be tightening boundaries and reducing incidental complexity before the codebase gets much larger.

## Recommended Improvement Priorities

### High Priority

- Reduce silent `catch { }` usage and improve logging
- Remove or tighten legacy-compatible auth and tenant override paths where no longer needed
- Break up oversized services and controllers
- Remove direct stack trace exposure from API responses

### Medium Priority

- Refactor repeated UI API orchestration into reusable helpers or UI services
- Replace reflection-based view-model mapping with explicit mapping
- Reduce per-item query enrichment in list endpoints
- Make module boundaries more explicit between auth, profile, and operational flows

### Lower Priority

- Standardize response handling strategy across API and UI
- Add broader automated tests around middleware, tenant scoping, and cross-module workflows
- Revisit whether some reporting and sync responsibilities should be further separated

## Summary

The Hospital Management System is best described as a modular layered monolith with:

- a dedicated API
- a separate MVC frontend
- service-layer business logic
- multi-tenant support
- permission-based authorization
- sync and outbox capabilities

It has a strong architectural direction, but also visible implementation strain. The most important long-term improvements are to tighten boundaries, simplify large classes, reduce hidden failure paths, and make tenant/security behavior more explicit.
