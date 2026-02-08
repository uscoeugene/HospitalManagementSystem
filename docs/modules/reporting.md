# Reporting Module

## Purpose

Provides application-level reporting services and DTOs for consumption by front-end dashboards and scheduled aggregation jobs.

## Key namespaces and classes

- `HMS.API.Application.Reporting`
  - `IReportService` — common reporting contracts
  - `ReportModels` — shared report models used across services

- Billing/Patient/Profile/Pharmacy/Lab report services implement detailed reporting functionalities:
  - `IBillingReportService`, `IPatientReportService`, `IProfileReportService`, `IPharmacyReportService`, `ILabReportService`

- `HMS.API.Infrastructure.Reporting.ReportingAggregatorService` — background service that precomputes expensive aggregates and stores them in Redis (if configured)

## Caching
- Uses `IDistributedCache` (StackExchange Redis) when configured via connection strings
- Aggregator caches keys like `reports:billing:kpi`, `reports:billing:monthly`, `reports:patients:recent`, `reports:profiles:recent`

## Extensions & notes
- Add more granular cache invalidation when underlying data changes
- Consider precomputing per-tenant reports if multi-tenancy is added
