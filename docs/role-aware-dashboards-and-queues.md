# Role-Aware Dashboards and Queues

This project now uses a reusable role-driven landing pattern for users who need a focused workspace.

## Purpose

The goal is to surface the right hospital workflow based on the signed-in user's role, while keeping the entry points simple and card-based.

Use this pattern when a screen needs:

- A role-specific landing page
- A queue hub for long-running worklists
- Quick actions that differ by user group
- Shared access to lab, pharmacy, billing, and clinical work

## Role Claims

The UI login flow now copies role names from the API response into `ClaimTypes.Role`.

That lets MVC code use:

- `User.FindAll(ClaimTypes.Role)`
- role-aware branching in controllers and views

The current built-in catalog uses canonical hospital roles such as:

- `System Administrator`
- `Hospital Administrator / Super Admin`
- `Doctor / Physician`
- `Nurse`
- `Pharmacist`
- `Laboratory Staff`
- `Radiology Staff`
- `Receptionist / Front Desk`
- `Billing / Accounts Officer`
- `Insurance / Claims Officer`
- `Cashier`
- `Medical Records / HIM Officer`
- `HR / Staff Manager`
- `Procurement Officer`
- `Inventory / Store Manager`
- `Finance Manager / Accountant`
- `Department Manager / Head of Department`
- `Hospital Operations Manager`
- `Auditor / Compliance Officer`
- `Patient Portal User`

Legacy role aliases such as `Admin`, `LabTech`, `Doctor`, `Cashier`, and `Pharmacist` are still recognized during migration, but new work should prefer the canonical names above.

## Shared View Models

- `DashboardViewModel`
- `DashboardCardViewModel`
- `QueuePageViewModel`
- `QueueItemViewModel`

## Entry Points

- `Views/Account/Dashboard.cshtml`
- `Controllers/QueuesController.cs`
- `Views/Queues/Index.cshtml`
- `Views/Queues/Lab.cshtml`
- `Views/Queues/Pharmacy.cshtml`
- `Views/Queues/Billing.cshtml`

## How To Use

1. Build a role-aware landing model in the controller.
2. Add one card per role or queue that matters to the current user.
3. Keep the visible labels friendly and workflow-focused.
4. Link to a dedicated queue page when the list is long enough to need paging or filtering.
5. Reuse `_ListQueryControls` and `_PagedNavigation` on queue pages instead of hard-coding new controls.
6. Keep GUIDs out of visible labels; use patient, invoice, prescription, or request display fields instead.

## Queue Page Pattern

Queue pages should:

- Accept a small set of filters, usually `status`
- Keep a predictable page size
- Render list rows as cards for readability
- Use clear badge colors for state
- Provide one primary action per item

## Current Queue Sources

- Lab requests: `/lab/requests`
- Pharmacy prescriptions: `/pharmacy/prescriptions`
- Billing invoices: `/billing`

## Developer Notes

- Add role claims in the UI auth cookie whenever the API returns them.
- Prefer queue pages for long operational lists and dashboard cards for the top-level overview.
- If a role does not have a dedicated queue yet, give it a clear workspace link instead of a fake worklist.
- Keep queue items friendly and clinically meaningful.
