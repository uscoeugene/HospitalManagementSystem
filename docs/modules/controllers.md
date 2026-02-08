# Controllers (API Surface)

## Purpose

Controllers expose the HTTP API surface. They map requests to application services and enforce authorization via `HasPermission` attributes.

## Notable controllers
- `AuthController` — login, refresh, registration
- `PatientsController`, `ProfileController` — patient and profile CRUD
- `BillingController`, `PaymentsController`, `PharmacyController`, `InventoryController`, `LabController` — domain operations
- Reports controllers under `Controllers/Reports` — multiple report endpoints for dashboards

## Routing & Attributes
- Controllers use attribute routing, e.g., `[Route("api/reports/pharmacy")]`
- Permissions are enforced using `HasPermissionAttribute` on endpoints

## Swagger
- Minimal Swagger setup is in `Program.cs` with `SimpleResponseExamplesFilter` to add example responses for some endpoints

## Notes
- Keep controllers thin: move business logic to services
- Use DTOs for request/response models and mapping between domain entities and DTOs
