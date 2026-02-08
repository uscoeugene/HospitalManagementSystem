# Infrastructure Module

## Purpose

Contains cross-cutting infrastructure concerns: EF DbContexts, outbox processing, background services, auth persistence, caching setup, SignalR hubs, and common utilities.

## Key components

- `AuthDbContext`, `HmsDbContext` — EF Core contexts for auth and domain data
- `OutboxProcessor` — background service that flushes outbox messages to external systems
- `ReservationCleanupService` — cleans stale pharmacy reservations
- `ReportingAggregatorService` — precomputes and caches report aggregates
- `NotificationHub` — SignalR hub for real-time notifications
- `EventPublisher` — implementation used to publish events across the app

## Configuration
- Databases are configured in `Program.cs` using connection string `Default`
- Redis is optional and configured via `ConnectionStrings:Redis` or `Redis:Configuration`

## Notes
- Migrations exist under `Infrastructure/Persistence/Migrations`
- Outbox pattern reduces coupling between domain events and external systems
