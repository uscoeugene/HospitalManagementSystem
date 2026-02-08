# Tests

## Purpose

Contains unit and integration tests for the API. Ensures critical flows like authentication, payments, profiles, and controllers behave as expected.

## Projects
- `HMS.API.Tests` — unit tests
- `HMS.API.IntegrationTests` — integration tests using `CustomWebApplicationFactory` and in-memory test server

## Notable tests
- `ProfileServiceTests` — unit tests for profile service behaviors
- Integration tests for payments, lab, notifications, and auth flows

## Notes
- Integration tests rely on test factory and may run migrations against an in-memory or localdb test database
- Add more tests for billing edge cases and concurrency scenarios
