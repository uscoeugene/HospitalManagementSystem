# HospitalManagementSystem (HMS.API) - Project Documentation

This documentation set covers the `HMS.API` solution in this repository. It provides a high-level summary of the project, its modules, responsibilities, and links to module-specific documents with design details and developer guidance.

## Project overview

`HMS.API` is an ASP.NET Core Web API (targeting .NET 8) that implements a Hospital Management System. It follows a layered approach with clear separation between domain models, application services, infrastructure concerns, and HTTP controllers. The API includes features for authentication & authorization, patient and profile management, billing and payments, laboratory workflow, pharmacy & inventory, reporting, synchronization with a cloud endpoint, notifications via SignalR, and background processing (outbox, reservation cleanup, reporting aggregation, sync background).

Key characteristics:
- Language: C# 12
- Framework: .NET 8
- Pattern: Layered architecture (Domain / Application / Infrastructure / API)
- Persistence: Entity Framework Core (SQL Server by default)
- Caching: Optional Redis (for reporting caches)
- Authentication: JWT Bearer
- Authorization: Permission-based policy provider
- Real-time notifications: SignalR
- Background processing: Hosted services

## Documentation layout

- `docs/README.md` (this file) — project summary and links to module documentation
- `docs/modules/*` — per-module detailed documents

## Modules

Below are the primary modules. Each module has a dedicated document under `docs/modules/`:

- Auth — `docs/modules/auth.md`
- Billing — `docs/modules/billing.md`
- Pharmacy — `docs/modules/pharmacy.md`
- Lab — `docs/modules/lab.md`
- Patient — `docs/modules/patient.md`
- Profile — `docs/modules/profile.md`
- Payments — `docs/modules/payments.md`
- Reporting — `docs/modules/reporting.md`
- Sync — `docs/modules/sync.md`
- Infrastructure — `docs/modules/infrastructure.md`
- Common — `docs/modules/common.md`
- Controllers (API surface) — `docs/modules/controllers.md`
- Tests — `docs/modules/tests.md`

Each module document includes: responsibilities, key classes and namespaces, DTOs and domain models, registration and configuration points, sample usage or endpoints, testing notes, and extension guidance.

---

Refer to the module documents for implementation details and development guidance.
