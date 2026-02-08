# Sync Module

## Purpose

Handles synchronization with an external cloud service for profiles and other entities. Provides background sync and HTTP client abstractions for the cloud.

## Key namespaces and classes

- `HMS.API.Application.Sync`
  - `ISyncManager`, `ICloudSyncClient` — orchestrate sync tasks
  - DTOs for sync under `Application/Sync/DTOs`

- `HMS.API.Infrastructure.Sync`
  - `CloudSyncClient` — `HttpClient`-based implementation
  - `BackgroundSyncService` — hosted service to schedule sync runs
  - `SyncManager` — applies and reconciles sync operations

## Notes
- `CloudSyncClient` is configured via `Program.cs` using `AddHttpClient`
- Ensure resilient HTTP calls with retries (Polly) when integrating with unreliable cloud endpoints
