# Payments Module

## Purpose

Handles recording payments, refunds, receipts, and integration points to external payment processors.

## Key namespaces and classes

- `HMS.API.Application.Payments`
  - `IPaymentService`, `PaymentService` — high-level payment orchestration
  - DTOs: `PaymentDtos`

- `HMS.API.Domain.Payments`
  - `Payment`, `Receipt`, `Refund`, `RefundReversal`

- Controllers
  - `PaymentsController`

## Notes
- PaymentService integrates with BillingService to apply payments to invoices and generate receipts
- Ensure idempotency when handling external gateway callbacks
- Provide refund and reversal workflows
