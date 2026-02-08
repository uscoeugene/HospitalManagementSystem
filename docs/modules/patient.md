# Patient Module

## Purpose

Manages patient entities, visits, duplicates detection, searching, and patient-related reporting.

## Key namespaces and classes

- `HMS.API.Application.Patient`
  - `IPatientService`, `PatientService` — CRUD and search operations
  - `IPatientReportService`, `PatientReportService` — patient reporting (recent patients, summaries)
  - DTOs: `PatientDtos`, `PatientReportDtos`, `DuplicateDtos`

- `HMS.API.Domain.Patient`
  - `Patient`, `Visit`

- Controllers
  - `PatientsController`, `Reports/PatientsReportController`

## Notes
- Duplicate detection uses `StringSimilarity` utilities
- Patient summary report is cached by the reporting aggregator
- Consider GDPR and PII protection for exported patient data
