# Profile Module

## Purpose

Manages user profiles and public-facing profile data used for synchronization with cloud services.

## Key namespaces and classes

- `HMS.API.Application.Profile`
  - `IProfileService`, `ProfileService` — CRUD for user profiles
  - `IProfileReportService`, `ProfileReportService` — reporting endpoints for profiles
  - DTOs: `UserProfileDtos`, `ProfileReportDtos`

- `HMS.API.Domain.Profile`
  - `UserProfile`

- Controllers
  - `ProfileController`, `Reports/ProfileReportController`

## Notes
- Profile sync DTOs exist under `Application/Sync/DTOs`
- Profiles are used by reporting and sync modules
