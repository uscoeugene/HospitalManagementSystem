# Pharmacy Module

## Purpose

Manages drugs, inventory items, stock movements, prescriptions, dispensing, reservations, and reports related to pharmacy operations.

## Key namespaces and classes

- `HMS.API.Application.Pharmacy`
  - `IPharmacyService`, `PharmacyService` — high-level operations for prescriptions, charging, dispensing
  - `IInventoryService`, `InventoryService` — inventory CRUD, stock adjustments, audits, reservations
  - `IPharmacyReportService`, `PharmacyReportService` — reports: stock shortages, daily dispenses, revenue per drug
  - DTOs: `PharmacyDtos`, `InventoryDtos`, `PharmacyReportDtos`

- `HMS.API.Domain.Pharmacy`
  - `Drug`, `InventoryItem`, `InventoryCategory`, `InventoryAudit`, `Reservation`, `DispenseLog`, `Prescription`

- `HMS.API.Infrastructure.Pharmacy`
  - `ReservationCleanupService` — background service to clean stale reservations

- Controllers
  - `PharmacyController`, `Reports/PharmacyReportController`, `InventoryController`

## Inventory specifics
- Audit trail via `InventoryAudit` for adjustments and reservations
- Availability calculation considers reserved quantities

## Reporting
- Aggregator caches pharmacy reports when Redis is enabled.
- `PharmacyReportService` implements methods used by `Reports/PharmacyReportController` endpoints.

## Extensions & notes
- Implement FIFO/LIFO cost handling if needed for cost of goods sold
- Consider batch/lot tracking and expiry management for drugs
- Ensure transactional integrity when charging and dispensing prescriptions
