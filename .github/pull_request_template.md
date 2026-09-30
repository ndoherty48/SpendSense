## Summary

<!-- What and why, one paragraph. -->
Stack: PR N of 16. Depends on #<prev>. Next: #<next>.

## Scope

In: <screens / files>.
Out: <explicitly not in this PR>.

## Behaviour preserved

- [ ] <screen-specific list, e.g. HasDependencies delete guard, suggestion debounce>
- [ ] Intentional behaviour fixes (separate commit): <e.g. privacy mode now hides amounts on X>
- [ ] Deviations from the mock (real fields win): <list>

## Screenshots

<!-- Attach to the PR; do not commit them. Narrow (~390px) required; wide (>=1024px) once the shell lands. -->
<!-- States: populated, empty, error/over-budget, validation error, hidden amounts. -->

| Screen / state | Before (main) | After light | After dark |
|---|---|---|---|
|  |  |  |  |

## Verification

- [ ] `cd App/SpendSense && dotnet build -f net10.0-maccatalyst` (no new warnings)
- [ ] Ran on Mac Catalyst (light + dark, narrow + wide resize)
- [ ] iOS simulator / Android / Windows: <required for theme, shell, form and platform changes>
- [ ] Smoke matrix for touched screens (create, edit, delete, guards)
- [ ] Contrast and 44px targets checked on new UI
- [ ] `tokens.css` and `LedgerTheme.cs` hex values still in sync

## Reviewer guide

<!-- Commit order, where to focus, known gaps. -->
