# Ledger Noir design tokens

The token contract for the Ledger Noir design system. Every later PR in the `ledger/*` stack
references these names. **Source of truth for values is `wwwroot/css/tokens.css`**; `LedgerTheme.cs`
mirrors the colour hex values for MudBlazor (`MudColor` cannot parse `var()`), so the two must be
changed together.

- Prefix: every custom property is `--ss-*`; any global CSS class is `ss-*`.
- Colours are **hex** (no `oklch()` — older Android WebViews).
- No `backdrop-filter`: surfaces are solid and elevation comes from shadow.
- Dark is the design's home theme; light is the companion. Default theme mode is **System**.

## Colour

| Token | Dark | Light | Role |
|---|---|---|---|
| `--ss-bg` | `#12151C` | `#F5F7FA` | Page background |
| `--ss-surface` | `#1B2029` | `#FFFFFF` | Cards, nav pill, list groups |
| `--ss-surface-2` | `#232A36` | `#ECF0F5` | Inputs, icon tiles, progress tracks |
| `--ss-border` | `#2E3644` | `#DFE4EC` | Decorative dividers and card outlines |
| `--ss-border-strong` | `#6B7689` | `#7F8A9B` | Input and control boundaries (>= 3:1) |
| `--ss-ink` | `#F4F6F8` | `#14181F` | Primary text |
| `--ss-ink-muted` | `#9BA3B4` | `#5B6472` | Secondary text and inactive icons |
| `--ss-accent` | `#33D6A6` | `#097754` | Brand, positive, active nav, primary actions |
| `--ss-accent-soft` | `#16332B` | `#DCF3EA` | Tint behind accent icons and badges |
| `--ss-on-accent` | `#12151C` | `#FFFFFF` | Text/icons on an accent fill (FAB, primary button) |
| `--ss-error` | `#FF6B5E` | `#B83629` | Over-budget, destructive, negative |
| `--ss-error-soft` | `#3A2129` | `#FBE2DE` | Tint behind error icons and badges |
| `--ss-warn` | `#F0B94E` | `#8A5A00` | Medium priority, near-limit |
| `--ss-warn-soft` | `#3A2E13` | `#F7E9C8` | Tint behind warn badges |

Category colours are **data** (stored per `Category`, arbitrary hex) and are the only place raw colour
values may appear outside `tokens.css`.

Deviation from the storyboard: the light accent is `#097754` (storyboard `#0A7D5B`) and the light error
is `#B83629` (storyboard `#C23B2E`). The storyboard values measured 4.41:1 and 4.30:1 on their own
tinted chips, just under AA; these values clear 4.5:1 everywhere they are used.

### Verified contrast (WCAG 2.1 AA: 4.5:1 text, 3:1 icons and UI boundaries)

| Pairing | Dark | Light |
|---|---|---|
| `ink` on `bg` | 16.86 | 16.58 |
| `ink` on `surface` | 15.08 | 17.79 |
| `ink-muted` on `bg` | 7.21 | 5.57 |
| `ink-muted` on `surface` | 6.45 | 5.98 |
| `ink-muted` on `surface-2` | 5.70 | 5.23 |
| `accent` on `bg` | 9.84 | 5.18 |
| `accent` on `surface` | 8.80 | 5.56 |
| `accent` on `surface-2` | 7.77 | 4.86 |
| `accent` on `accent-soft` | 7.33 | 4.78 |
| `error` on `bg` | 6.54 | 5.44 |
| `error` on `surface` | 5.85 | 5.84 |
| `error` on `surface-2` | 5.16 | 5.10 |
| `error` on `error-soft` | 5.26 | 4.73 |
| `on-accent` on `accent` | 9.84 | 5.56 |
| `warn` on `warn-soft` | 7.44 | 4.92 |
| `warn` on `surface` | 9.15 | 5.93 |

`--ss-border` is decorative (1.2–1.5:1) and must never be the only thing marking an input or control
boundary — WCAG 1.4.11 needs 3:1 there, which is what `--ss-border-strong` (3.05–3.98:1 against `bg`,
`surface` and `surface-2` in both themes) is for. Any new token pairing must be added to this table.

## Type

| Token | Value |
|---|---|
| `--ss-font-display` | `'Space Grotesk', system-ui, sans-serif` |
| `--ss-font-body` | `'IBM Plex Sans', system-ui, sans-serif` |
| `--ss-font-mono` | `'IBM Plex Mono', ui-monospace, monospace` |

| Style | Font | Size / weight |
|---|---|---|
| Page title (`h1`) | display | 22px / 700 |
| Section title | display | 15px / 600 |
| Body, row title | body | 13.5px / 600 |
| Caption, row subtitle | body | 11px / 400 |
| Micro label (uppercase, `0.06–0.09em`) | body | 10–11px / 600 |
| Hero balance | mono | 36px / 600 |
| Row amount | mono | 13.5px / 600 |

All money is IBM Plex Mono with `font-variant-numeric: tabular-nums`. Sign is shown with a `+`/`−`
glyph as well as colour (WCAG 1.4.1).

## Shape, space and layout

| Token | Value |
|---|---|
| `--ss-radius-sm` / `-md` / `-lg` / `-pill` | `10px` / `14px` / `20px` / `999px` |
| `--ss-space-1` … `--ss-space-8` | `4 8 12 16 20 24 32 40` px |
| `--ss-target` | `44px` minimum interactive size |
| `--ss-content-max` | `720px` content column on wide windows |
| `--ss-nav-height` / `--ss-nav-gap` | `64px` / `22px` (pill height and offset from the bottom edge) |
| `--ss-rail-width` | `88px` |

- **Breakpoint: 768px.** Below it the floating pill nav and FAB are shown; at or above it the slim left
  rail. CSS custom properties cannot be used inside media queries, so `768px` is written literally.
- Pill offset is `calc(var(--ss-nav-gap) + env(safe-area-inset-bottom, 0px))`. `MainPage` already
  insets with `SafeAreaEdges="All"`, so the env value is usually 0.

## Elevation

| Token | Dark | Light |
|---|---|---|
| `--ss-shadow-nav` | `0 12px 28px rgba(0,0,0,.55), 0 0 0 1px rgba(255,255,255,.03)` | `0 10px 24px rgba(20,24,31,.10), 0 0 0 1px rgba(20,24,31,.02)` |
| `--ss-shadow-fab` | `0 10px 22px rgba(0,0,0,.55), 0 0 0 1px rgba(255,255,255,.06)` | `0 10px 20px rgba(9,119,84,.35)` |

## Component to token map

| Component | Tokens |
|---|---|
| `HeroBalanceCard` | `surface`, `border`, `radius-lg`, mono hero |
| `StatTile` | `surface-2`, `ink-muted` label, mono value |
| `ListRow` | icon tile on `surface-2` (accent or `ink-muted` icon), `ink` title, `ink-muted` subtitle, mono amount |
| `ProgressBar` | track `surface-2`, fill `accent`, over-budget fill `error` |
| `Badge` | `*-soft` background with the matching solid token as text |
| `SegmentedControl` | track `surface-2`, active segment `surface`, text `ink` / `ink-muted` |
| `BottomNav` / `SideRail` | `surface`, `border`, `shadow-nav`, active `accent`, inactive `ink-muted` |
| `Fab` | `accent` fill, `on-accent` icon, `shadow-fab` |
| Form inputs | `surface-2` fill, `border-strong` outline, `ink` text |

## Accessibility rules

- Text contrast >= 4.5:1, icons and UI boundaries >= 3:1, in both themes (table above).
- Interactive targets >= `--ss-target` (44px).
- Colour is never the only signal: over-budget rows also carry an icon and a label, amounts carry a
  sign glyph.
- Exactly one `<h1>` per page (`Routes.razor` focuses it on navigation).
- Icon-only buttons have an `aria-label`; the active tab has `aria-current="page"`.
- Honour `prefers-reduced-motion`.
