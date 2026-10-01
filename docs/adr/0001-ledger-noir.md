# ADR 0001: Adopt the Ledger Noir design system

- Status: Accepted
- Date: 2026-09-30

## Context

SpendSense is a .NET 10 MAUI Blazor Hybrid app on MudBlazor 9. Its UI is stock MudBlazor: a teal app
bar, a hamburger drawer, `MudTable` lists, raw `<h1>` headings, an "Add" button under every title, and a
Roboto stylesheet fetched from Google Fonts (which fails offline in a local-first app). Nothing is shared
between pages, so every screen is a one-off.

A 14-screen storyboard ("Ledger Noir") defines the replacement: a dark-first theme with a light
companion, mono tabular money, a floating bottom tab bar with a FAB, and a WCAG AA contrast check. The
full token contract is in [`docs/design/tokens.md`](../design/tokens.md).

The migration ships as a stack of small pull requests, each building on the last
(`ledger/00-…` to `ledger/15-…`, created with `gh stack`). This ADR records the decisions that shape
every one of them.

## Decisions

### 1. Keep MudBlazor as the behaviour layer

MudBlazor stays for selects, date pickers, numeric and text fields, dialogs, popovers, snackbars, charts
and the theme provider. It is themed through `MudTheme` (`LedgerTheme.cs`). Our own components replace
its layout and visual pieces: app bar, drawer, nav menu, `MudTable`/`MudSimpleTable`,
`MudCard`/`MudPaper`/`MudGrid` page layout, `MudProgressLinear`, `MudChip`.

Accepted cost: colours live in two places (`tokens.css` and `LedgerTheme.cs`) and must be edited
together, and Mud's CSS still ships so ours must load after it. Dropping MudBlazor is revisited only
after the stack lands.

### 2. Pure UI slices

No schema, repository, route or business-logic changes. Every existing `@page` URL survives. The
storyboard is directional; where it omits a real field or behaviour, the real one wins (currency
selector and exchange rate, category icon text, goal status/current amount/category, budget
year/month, the Trends 3/6/12 range and per-category chart, transaction notes, recurring
frequency/day-of-month/active).

### 3. Navigation

- Five destinations: Home, Activity, Budgets, Trends, More.
- Activity switches between `/transactions` and `/recurring-transactions`; Budgets switches between
  `/monthly-budgets` and `/goals`. Routing is the state.
- Categories, Currencies and Preferences live under a `/more` hub.
- Below 768px: floating pill bar plus FAB. At or above 768px: a slim left rail and a centred
  `--ss-content-max` column (Mac Catalyst, Windows, iPad, landscape).
- Recurring transactions stay a separate entity with their own pages. Merging them into the transaction
  form behind a "Repeats" toggle is a data-model change and is out of scope.

### 4. Layouts

`MainLayout` holds providers and the content column only. `TabsLayout` (the router default) adds the
nav, FAB and section switch; `FocusLayout` (back-bar, no nav) is opted into per form page. Both nest
inside `MainLayout`, so providers never remount when moving between them.

### 5. Theme

- Mode is System / Light / Dark, default System, persisted in `Preferences`, applied live with no page
  reload and no flash of the wrong theme.
- Native-first: a `ThemeService` resolves the effective theme from the mode or the OS, sets
  `Application.UserAppTheme`, and applies `data-theme` on `<html>`. A tiny inline script reads a
  `localStorage` mirror before Blazor boots so first paint is correct.
- The design never depends on the webview's `prefers-color-scheme` following the native theme; the
  explicit `data-theme` attribute always wins.

### 6. CSS

- Load order: `tokens.css`, `fonts.css`, `MudBlazor.min.css`, `base.css`, `app.css`,
  `SpendSense.styles.css`.
- Component styling uses scoped `.razor.css` with tokens only. Global CSS is limited to tokens, fonts,
  base and charts, and uses the `ss-` prefix.
- Fonts are self-hosted as woff2 under `wwwroot/fonts/` with `font-display: swap`.
- No `backdrop-filter`.

### 7. Forms

Add and Edit share one `<Entity>Form.razor` on the shared form primitives. For the largest form
(transactions) the extraction was its own pure-move commit followed by the restyle, so the visual diff
reads cleanly; the smaller forms were extracted and restyled together. The "Validation Summary" side panel
on the Add pages is dropped: its success branch never rendered, and inline field messages replace it.

### 8. Money and privacy mode

All money renders through a `<Money/>` component (mono digits, sign glyph plus colour, `•••` when
`HideAmounts` is on, `aria-label="Amount hidden"`). Each page PR closes its own privacy-mode gap
(hard-coded `£` and `ToString("F2")`) in a separate `fix(privacy):` commit. `NotificationService` text
is non-UI and is fixed in a standalone PR outside the stack.

### 9. Charts

`MudChart` stays, with a token-driven `ChartOptions` palette per resolved theme (`ChartTheme.cs`). The
Home pie becomes a themed donut alongside the storyboard's category rows.

## Mock versus real

| Screen | Storyboard shows | Real behaviour that must be kept |
|---|---|---|
| Transaction form | Type, amount, description, category, date, repeats toggle, notes | Currency select, default-currency preselect (Add), 500 ms description→category suggestion, post-save notification checks. No repeats toggle. |
| Budgets | Category rows with progress | Copy to Next Month, Year/Month fields on the form |
| Goals | Name, target, saved, date, priority | Status, current amount, category, currency; per-goal progress from linked savings |
| Categories | Colour swatches | Arbitrary hex (max 9 chars), free-text icon field, blocked delete when in use |
| Currencies | Add form | Exchange rate, Set Default, blocked delete for the default and in-use currencies; there is no edit page |
| Trends | 6-month bars and category shares | 3/6/12-month range, total line chart, per-category bar chart |
| Preferences | Dark mode, hide amounts, income-next-month, active period | Theme becomes System/Light/Dark; the active period stays on the dashboard month switcher |
| Recurring | Not shown | Full list/add/edit incl. frequency, day of month, active switch |

## Out of scope

Recurring-to-transaction merge, the per-goal N+1 query, hard-coded `£` in `NotificationService`, the
leftover `Console.WriteLine` in `RecurringTransactionGenerator`, a CI build workflow, a screenshot test
harness. File these as follow-up issues.

## Consequences

- Sixteen small PRs, strictly chained through the shell (00–05), then largely independent page slices
  (06–14), then polish (15).
- Mid-stack, unmigrated pages look inconsistent next to migrated ones. The stack is reviewed
  bottom-up and merged atomically, so that state never ships.
- There are no tests and no CI. Each PR carries a smoke-test checklist and before/after screenshots
  (light and dark) in its description.

## Reference

The storyboard lives in a Claude artifact shared with the maintainer; the decisions above and
[`tokens.md`](../design/tokens.md) are the durable record.
