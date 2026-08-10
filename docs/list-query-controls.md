# List Query Controls

This project now uses a reusable Razor partial pattern for long, list-heavy screens.

## Purpose

The goal is to keep filtering, sorting, page-size selection, and paging consistent across screens that return large datasets.

Use this pattern when a page needs:

- A filter dropdown
- A sort dropdown
- A records-per-page selector
- Shared paging links
- Preserved query-string state across navigation

## Shared Partials

- `Views/Shared/_ListQueryControls.cshtml`
- `Views/Shared/_PagedNavigation.cshtml`

## Partial Refresh

When a list or tab should update without a full page reload:

- Wrap the fragment in a stable container with an id, such as `chart-timeline-panel`.
- Set `UseAjax = true` on the query and pager models.
- Set `AjaxTarget` to the container selector, for example `#chart-timeline-panel`.
- Set `AjaxExtraQuery` when the server needs to know which fragment to return, for example `panel=timeline`.
- Keep the visible route clean. The fragment hint should be added only to the AJAX request, not to the user-facing link or form action.
- Return the fragment from the controller when that panel is requested.

The global script in `wwwroot/js/portal-layout.js` intercepts matching forms and links, fetches the fragment, and swaps only the target panel.

## Supporting View Models

- `ListQueryControlsViewModel`
- `PagedNavigationViewModel`

## How To Use

1. Build the query state in the controller or page model.
2. Populate hidden fields with stable context values such as patient id, visit id, category, sort order, or search term.
3. Pass the query model into `_ListQueryControls` for the form controls.
4. Pass the paging model into `_PagedNavigation` for the page links.
5. Keep page-size options small and predictable, typically `6`, `10`, `20`, or `50`.
6. For update-panel behavior, point both partials at the same target container and return the fragment from the controller.

## Chart Example

The patient chart timeline uses:

- `timelineCategory`
- `timelineSort`
- `timelinePageSize`
- `timelinePage`

This keeps the URL shareable and lets users return to the same filtered list state.
When JavaScript is enabled, the chart swaps just the timeline panel instead of reloading the full page.

## Developer Notes

- Prefer this shared pattern over hand-built pagination blocks for new list screens.
- Preserve hidden context fields when a list is nested inside a patient or visit workflow.
- Keep the default page size conservative for clinical workflows, then let the user expand it when needed.
- Do not expose raw database identifiers in visible labels; keep them in hidden fields or route values only.
