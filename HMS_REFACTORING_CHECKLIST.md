# HMS Refactoring Checklist

## Patient Chart Shell
- [ ] Patient header renders from the shared patient header partial.
- [ ] Chart opens from patient details and visit details.
- [ ] Visit switcher works without exposing raw GUIDs.
- [ ] Overview shows patient, selected visit, and quick counts.
- [ ] Summary cards stay compact and do not push the clinical workspace below the fold.
- [ ] Workflow actions remain readable on mobile and desktop.
- [ ] Chart tabs include clear left/right scroll affordances.
- [ ] Timeline supports compact visual rendering with paging, filtering, sorting, and page-size selection.
- [ ] Timeline pager updates only the timeline panel instead of reloading the entire page when JavaScript is available.
- [ ] Timeline orders events by timestamp.
- [ ] Existing clinical, lab, pharmacy, and billing records appear in the correct tabs.
- [ ] Empty modules show clear "not yet implemented" messaging.

## Reusable List Controls
- [ ] Use `Views/Shared/_ListQueryControls.cshtml` for list filters and sort controls.
- [ ] Use `Views/Shared/_PagedNavigation.cshtml` for page links and range summaries.
- [ ] Use AJAX panel refresh for long lists when the user only needs to update a subsection.
- [ ] Preserve hidden context fields when a list belongs to a patient workflow.
- [ ] Keep page-size options consistent across list-heavy screens.
- [ ] Document new long-list pages to reuse the shared query/pager pattern instead of creating bespoke controls.

## Role-Aware Dashboards And Queues
- [x] Dashboard adapts its workspace cards to the signed-in user's roles.
- [x] Role claims are preserved in the UI cookie so dashboard logic can branch safely.
- [x] Queue hub exposes role-specific entry points for clinical, lab, pharmacy, and billing work.
- [x] Lab, pharmacy, and billing queue pages reuse shared card-based list rendering.
- [x] Queue pages use shared query controls and paging instead of bespoke list chrome.
- [ ] Doctor-specific queue data is expanded beyond the patient search / chart entry point.

## Data Contract Updates
- [ ] Patient payloads expose creation and update timestamps where needed.
- [ ] Prescription payloads expose creation and update timestamps where needed.
- [ ] UI models stay aligned with API DTOs.
- [ ] No unnecessary database schema changes are introduced.

## Security And Roles
- [x] Built-in hospital roles use canonical names and remain reserved from editing or deletion.
- [x] Doctor is seeded as a first-class role.
- [ ] HmsTenantId is treated only as a fallback hint.
- [ ] Notification access remains available to both user-facing and internal workflows.
- [ ] DevAdmin endpoints stay limited to local or debug scenarios.

## Billing And Clinical Rules
- [ ] Lab credit flows require a linked invoice.
- [ ] Pharmacy credit flows require a linked invoice.
- [ ] Patient-facing UI does not expose raw database identifiers.
- [ ] Billing and care navigation use friendly names and contextual labels.

## Modules To Build From Scratch
- [ ] Appointments
- [ ] Admissions
- [ ] Wards
- [ ] Beds
- [ ] Radiology

## UI Standards
- [ ] Bootstrap 5 only.
- [ ] Bootstrap Icons only.
- [ ] Card-based layout.
- [ ] Responsive at mobile and desktop breakpoints.
- [ ] Use TempData alerts for user feedback.
- [ ] Prefer reusable Razor partials over duplicated markup.
- [ ] Slim UI over large padded spaces

## Verification
- [ ] Patient chart loads for a patient with visits.
- [ ] Patient chart loads for a patient without visits.
- [ ] Visit-specific chart selection works.
- [ ] Lab, pharmacy, and billing tabs show expected records.
- [ ] No inline JavaScript was added.
- [ ] Reusable paging/filtering controls render correctly on list-heavy pages.
- [ ] Role-aware dashboard and queue pages load for each supported role.

- [ ] Document the system-admin maintenance workflow for agents and developers.
- [ ] Keep maintenance actions gated behind `system.maintenance.manage`.
- [ ] Use the platform host for global admin operations; avoid tenant-scoped hosts for reseed/reset tasks.
- [ ] Require explicit confirmation for tenant auth resets.

- [ ] Use `PlatformHosts` to pin the root/system admin host explicitly.
- [ ] Record all maintenance actions in `AuthAudits`.
- [ ] Require `RESET` plus tenant code confirmation for tenant auth resets.

- [ ] Prefer `PlatformContext:Hosts` for the central/system host list.
- [ ] Support environment-variable overrides for platform hosts in each deployment.
- [ ] Keep tenant domain mappings separate from the central platform host.

- [ ] Use `System:DeploymentMode=Bootstrap` for new installations before normal routing begins.
- [ ] Manage the central host list from `/Admin/AppSettings` using `PlatformContext:Hosts`.
- [ ] Keep central hosts separate from tenant domain records and LAN/offline tenant hosts.
- [ ] Switch from Bootstrap to Online only after the system is fully configured.

- [ ] Show a clear Bootstrap banner on login and portal pages during new-install setup.
- [ ] Keep the system maintenance link visible only to users with `system.maintenance.manage`.
- [ ] If the admin sidebar is missing, verify you are on the platform host and that the auth DB has been reseeded with the latest permission catalog.

- [x] Show a bootstrap checklist card on the dashboard when `System:DeploymentMode=Bootstrap`.
- [x] Keep a dev-only permission refresh endpoint for older databases.
- [x] Prefer `POST /admin/seed/permissions` over a full reset when only the permission catalog is stale.
