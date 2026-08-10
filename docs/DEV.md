# Development Guide

This document explains how to run the solution locally, apply EF Core migrations, seed data, and provision tenants.

Prerequisites

- .NET 8 SDK
- SQL Server or LocalDB instance reachable from your machine
- (optional) dotnet-ef tool: dotnet tool install --global dotnet-ef

Repository layout

- HMS.API - ASP.NET Core Web API, hosts two EF Core DbContexts: AuthDbContext and HmsDbContext
- HMS.UI - Razor Pages / MVC UI that calls the API

Configuration and secrets

- Connection string: set in HMS.API/appsettings.Development.json or via environment variable ConnectionStrings__Default
  Example connection string for LocalDB:

  "Server=(localdb)\\MSSQLLocalDB;Database=HmsDb;Trusted_Connection=True;"

- JWT secrets (required for authentication): set Jwt:Key and LocalJwt:Key. Use user-secrets or environment variables.

  PowerShell example (from repo root):

  cd HMS.API
  dotnet user-secrets init
  dotnet user-secrets set "Jwt:Key" "<a-secure-random-secret-at-least-32-chars>"
  dotnet user-secrets set "LocalJwt:Key" "<a-secure-random-local-secret>"

Running migrations (manual)

If you prefer to run EF migrations manually instead of letting the app apply them on startup:

From the repository root (PowerShell):

dotnet ef database update --project HMS.API --context AuthDbContext --startup-project HMS.API
dotnet ef database update --project HMS.API --context HmsDbContext --startup-project HMS.API

Notes:
- If you don't have dotnet-ef installed, install it with: dotnet tool install --global dotnet-ef
- Ensure the HMS.API project has Microsoft.EntityFrameworkCore.Design package available (it already does in this repo).

Automatic migrations and seeding

By default the API applies pending migrations and runs seed logic on startup. This is controlled by configuration key ApplyMigrationsOnStartup (defaults to true).

To let the application handle migrations and seeding simply run:

dotnet run --project HMS.API

The startup process will:
- apply migrations for AuthDbContext and HmsDbContext
- run seed logic in Auth.Infrastructure.SeedData.EnsureSeedDataAsync to create permissions, roles and base users
- run HmsSeedData to create required domain data

If you want to disable automatic migrations (for production or controlled migration deployments) set ApplyMigrationsOnStartup=false in appsettings or environment.

Verifying seed and initial users

After startup, verify roles/permissions and users exist in the Auth database (table names: Roles, Permissions, Users, RolePermissions).

You can query the DB directly using SQL Server Management Studio or use the Tenants / Users API endpoints provided by the API.

Tenant provisioning

The system supports multi-tenancy. Tenants are stored in the Auth database (Tenants table). Two ways to provision a tenant:

1) Using the API (recommended)

- POST /api/tenants with tenant details (Name, Code, Address, ContactEmail, ContactPhone). The API controller will validate and create a tenant record.

Example curl (once API is running):

curl -X POST "http://localhost:5000/api/tenants" -H "Content-Type: application/json" -d '{"name":"Demo Hospital","code":"DEMO","address":"123 Demo St","contactEmail":"admin@demo.local","contactPhone":"+100000000"}'

2) Direct DB insert (quick and dirty)

INSERT INTO Tenants (Id, Name, Code, Address, ContactEmail, ContactPhone, CreatedAt)
VALUES (NEWID(), 'Demo Hospital', 'DEMO', '123 Demo St', 'admin@demo.local', '+100000000', GETUTCDATE());

After creating a tenant you will need to create users for that tenant and assign roles/permissions. Use the API endpoints under /api/users and /api/roles or insert directly into Users and UserRoles.

Running the UI (HMS.UI)

From repo root:

dotnet run --project HMS.UI

UI configuration

- If running API and UI on the same machine, the UI will use relative settings to connect. Adjust HMS.UI/appsettings.json or set HmsApi:BaseUrl to point to your running API base URL.

Troubleshooting

- Error: cannot connect to SQL Server: ensure your connection string points to a running SQL Server instance and the database user has permissions.
- Error: missing developer HTTPS certificate: either trust the certificate (dotnet dev-certs https --trust) or run the app with HTTP only (Program.cs handles HTTPS gracefully).
- EF tools errors: install dotnet-ef tool and ensure environment PATH includes the tool location.

Deployment mode (Bootstrap / Online / OnPrem)

The application exposes a runtime "deployment mode" which controls tenant resolution and some UI banners. There are three effective places the system reads deployment mode from (in precedence order):

1) System:DeploymentMode in the Auth DB (AppSettings table) — authoritative at runtime and editable via Admin UI or API. This is the recommended place for production so operators can change mode without redeploying.
2) Deployment:Mode in HMS.API configuration (appsettings.json / environment) — used as a fallback when the DB key is missing.
3) HMS.UI configuration (Deployment:Mode or System:DeploymentMode) — UI-only fallback when API health check cannot be reached.

How this repo sets the default

- The auth DB seeding now ensures a default System:DeploymentMode key is created with value "Bootstrap" when the database is empty. This makes a fresh local install show the Bootstrap banner until you switch it.

How to change deployment mode

- Using the UI: Login as an admin and go to Admin -> App Settings. Change "Deployment Mode" and save.
- Using the API: POST to /AppSettings/upsert with JSON body { "key": "System:DeploymentMode", "value": "Online" }

Example curl (while the API is running and you have an admin auth cookie or token):

curl -X POST "https://localhost:7142/AppSettings/upsert" -H "Content-Type: application/json" -d '{"key":"System:DeploymentMode","value":"Online"}'

If you cannot reach the API from the UI during install, set Deployment:Mode in HMS.API/appsettings.Development.json to the desired value or update HMS.UI/appsettings.Development.json with a fallback value until the API is reachable.

Next recommended steps

- Secure production secrets (use environment variables or a secrets manager).
- Create a CI workflow to run migrations in a controlled manner and run tests.
- Add a README with tenant onboarding steps for non-technical users if needed.
