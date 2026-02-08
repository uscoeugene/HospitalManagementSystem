# Auth Module

## Purpose

Handles user authentication, password hashing, token issuance (JWT), refresh tokens, and seed data for initial admin users and permissions.

## Key namespaces and classes

- `HMS.API.Application.Auth`
  - `IAuthService` — interface for authentication operations
  - `AuthService` — implementation handling login, token creation, refresh
  - `IPasswordHasher` — interface for password hashing utilities
  - `PasswordHasher` — implementation
  - DTOs: `LoginRequest`, `LoginResponse`, `RefreshDtos`, `RegisterRequest`, `RoleDtos`

- `HMS.API.Infrastructure.Auth`
  - `AuthDbContext` — EF Core DbContext for auth entities
  - `SeedData` — ensures initial admin and permission seed data

- `HMS.API.Domain.Auth`
  - `User`, `Role`, `Permission`, `RolePermission`, `AuthAudit`

## Authentication configuration

Configured in `Program.cs` using JWT Bearer authentication:
- Key is read from `Jwt:Key` configuration (default fallback used in development)
- `TokenValidationParameters` set to validate signing key, disable issuer/audience validation in dev

## Authorization

Permission-based policies are implemented using:
- `PermissionRequirement`, `PermissionPolicyProvider`, `PermissionAuthorizationHandler`
- `HasPermissionAttribute` for decorating controllers/actions

## Important files and locations
- `HMS.API/Application/Auth/AuthService.cs`
- `HMS.API/Infrastructure/Auth/AuthDbContext.cs`
- `HMS.API/Infrastructure/Auth/SeedData.cs`
- DTOs under `HMS.API/Application/Auth/DTOs/`

## Extensions & notes
- Extend `SeedData` to add environment-specific users
- Consider rotating JWT signing key and using a stronger secret in production
- Add refresh token persistence if multi-device logout is required
