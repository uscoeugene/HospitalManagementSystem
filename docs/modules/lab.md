# Lab Module

## Purpose

Handles laboratory tests, requests, results, and lab-related reporting. Integrates with patient visits and billing.

## Key namespaces and classes

- `HMS.API.Application.Lab`
  - `ILabService`, `LabService` — operations for creating lab requests and storing results
  - `ILabReportService`, `LabReportService` — reporting operations related to lab volumes and revenue
  - DTOs: `LabDtos`, `LabReportDtos`

- `HMS.API.Domain.Lab`
  - `LabTest`, `LabRequest`

- Controllers
  - `LabController`, `Reports/LabReportController`

## Notes
- Lab tests have a `Price` and `Currency` (default USD)
- Integrate with billing to ensure lab requests generate invoice items when appropriate
- Add result attachments or structured result types if needed
