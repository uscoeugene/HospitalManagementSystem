# Billing Module

## Purpose

Manages invoicing, payments, debtor tracking, reporting (revenue, KPIs), and related background events.

## Key namespaces and classes

- `HMS.API.Application.Billing`
  - `IBillingService`, `BillingService` — core billing operations: create invoice, apply payments, update invoice status
  - `IBillingReportService`, `BillingReportService` — reporting operations: KPIs, monthly revenue, debt aging
  - DTOs: `InvoiceDtos`, `DebtPaymentDtos`, `ReportingDtos`, `BillingReportDtos`

- `HMS.API.Domain.Billing`
  - `Invoice`, `InvoiceItem`, `InvoicePayment`, `DebtorEntry`, `BillingAudit`

- `HMS.API.Controllers`
  - `BillingController`, `Reports/BillingReportController`, `Reports/BillingExtendedController`

## Important behavior
- Payments are recorded on `InvoicePayment` with optional `Receipt` generation.
- Debtor entries track outstanding debts per patient.
- Events like `InvoiceStatusChangedEvent` are published via the outbox to notify other parts of the system.

## Persistence & Migrations
- Billing-related entities are configured in `HmsDbContext` and migrations are present under `Infrastructure/Persistence/Migrations/`.

## Reporting
- `BillingReportService` provides summary KPIs and monthly revenue which are also cached by `ReportingAggregatorService` when Redis is configured.

## Extensions & notes
- Ensure idempotent payment application for external payment callbacks.
- Consider reconciliation flows for failed external transfers and refunds.
