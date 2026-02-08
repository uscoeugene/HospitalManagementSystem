# Common Module

## Purpose

Utilities and common abstractions used across modules: current user, notifications, event publishing, DTO helpers, and value objects.

## Key classes
- `ICurrentUserService`, `CurrentUserService` — abstract access to current authenticated user
- `INotificationService`, `NotificationService` — reusable notification mechanisms
- `IEventPublisher`, `EventPublisher` — local event bus
- Value objects: `Money`, `AuditTrail`
- `PagedResult<T>` — generic paging helper

## Notes
- `CurrentUserMiddleware` sets the current user context for requests
- Use `IEventPublisher` to decouple domain events from external integrations
