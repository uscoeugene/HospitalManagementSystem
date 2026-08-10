# HospitalManagementSystem

This repository contains the HMS.API (backend) and HMS.UI (Razor Pages / MVC) projects for a hospital management system targeting .NET 8.

Quickstart

- Ensure prerequisites are installed: .NET 8 SDK, SQL Server (or LocalDB), and PowerShell (or your preferred shell).
- Configure a database connection string and secure JWT secrets (see docs/DEV.md).
- From the repository root run the API which will apply migrations and seed initial data:

  dotnet run --project HMS.API

- Start the UI:

  dotnet run --project HMS.UI

See docs/DEV.md for full development setup, migration and tenant provisioning instructions.
