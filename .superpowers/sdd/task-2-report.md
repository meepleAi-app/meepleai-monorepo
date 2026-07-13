# Task 2 Report: Convert `MeepleCardGame` to a thin adapter over `MeepleCard`

Issue #2858 (C1), task 2 of 5. Branch: `feature/issue-2858-canonical-card-decision-table`.
Commit: `1e001d7a9` — `refactor(shared-games): MeepleCardGame becomes a MeepleCard adapter (#2858)`.

## What was implemented

Followed the brief's TDD step order exactly:

1. Rewrote `meeple-card-game.test.tsx` to the behavioral contract given in the brief (verbatim, with one assertion adjustment — see below).
2. Ran the new test against the old standalone-renderer implementation to confirm RED.
3. Replaced `meeple-card-game.tsx` body with the adapter (brief Step 3, verbatim).
4. Updated `shared-games-grid.tsx`: dropped `compact` from `SharedGamesGridGame` (`Omit<MeepleCardGameProps, 'labels' | 'compact' | 'className'>` → `Omit<MeepleCardGameProps, 'labels' | 'className'>`) and stopped forwarding `compact={compact}` to `<MeepleCardGame>`. Left `SharedGamesGrid`'s own `compact` prop and `<SkeletonCard compact={compact} />` untouched, per instruction.
5. Updated the 3 `[data-slot="shared-games-card"]` selectors in `shared-games-grid.test.tsx` to `[data-testid="shared-games-card"]`. Left the `shared-games-skeleton-card` and `shared-games-grid` selectors untouched.
6. Ran the full `shared-games` suite — green.
7. Ran `pnpm typecheck` — clean, no errors.
8. Committed exactly the 4 named files with the exact commit message from the brief.

## TDD evidence

### RED

Command: `pnpm exec vitest run src/components/ui/shared-games/meeple-card-game.test.tsx` (run against the *old* standalone-renderer body, new test file already in place).

Result: `Test Files 1 failed (1)` / `Tests 3 failed | 12 passed (15)`.

Failing tests (as predicted by the brief):
- `carries data-testid=shared-games-card on the card root` — old renderer emits `data-slot="shared-games-card"`, not `data-testid`.
- `renders the canonical rating readout (value.toFixed(1)) from rating + ratingMax=5` — old renderer emits `★`/`☆` glyphs via a custom `Stars` component, not a `4.0` text node.
- `renders the connection strip when any entity count > 0` — old renderer uses `EntityChip` directly with no `[data-testid="connection-chip-strip"]` wrapper.

This matches exactly what the brief's Step 2 predicted would fail and why.

### GREEN

Command: `pnpm exec vitest run src/components/ui/shared-games/meeple-card-game.test.tsx` (run against the new adapter body).

First pass: 1 failure (`renders the 🎲 cover fallback when coverUrl is missing` — see assertion adjustment below). After adjusting that one assertion:

```
✓ src/components/ui/shared-games/meeple-card-game.test.tsx (15 tests) 336ms
Test Files  1 passed (1)
     Tests  15 passed (15)
```

Full suite: `pnpm exec vitest run src/components/ui/shared-games`

```
✓ src/components/ui/shared-games/skeleton-card.test.tsx (6 tests)
✓ src/components/ui/shared-games/error-state.test.tsx (6 tests)
✓ src/components/ui/shared-games/contributors-sidebar.test.tsx (8 tests)
✓ src/components/ui/shared-games/shared-games-filters.test.tsx (12 tests)
✓ src/components/ui/shared-games/shared-games-hero.test.tsx (6 tests)
✓ src/components/ui/shared-games/shared-games-grid.test.tsx (7 tests)
✓ src/components/ui/shared-games/empty-state.test.tsx (8 tests)
✓ src/components/ui/shared-games/meeple-card-game.test.tsx (15 tests)

Test Files  8 passed (8)
     Tests  68 passed (68)
```

`pnpm typecheck` → `tsc --noEmit` exits clean, no output.

Re-ran the full `shared-games` suite once more after the commit (post pre-commit-hook prettier/eslint auto-fix) to confirm nothing regressed: still `8 passed (8)` / `68 passed (68)`.

## Test-assertion adjustment (per the brief's "important latitude")

One assertion in the brief's verbatim test did not match the real canonical `MeepleCard`/`GridCard` output:

**Brief's prediction** (Step 1, verbatim):
```tsx
it('renders the 🎲 cover fallback when coverUrl is missing', () => {
  render(<MeepleCardGame {...baseProps} coverUrl={null} />);
  expect(screen.getByText('🎲')).toBeInTheDocument();
});
```

**Actual canonical DOM** (observed via the RTL error dump on first GREEN attempt): `GridCard` renders the 🎲 glyph **twice** for `entity="game"` when there's no cover image:
1. `Cover`'s emoji-band fallback (`data-slot="cover-emoji-band"`), driven by the adapter's `coverEmoji="🎲"` prop — this is the one the test is meant to verify.
2. `EntityBadge`'s top-left badge (`data-slot="meeple-card-entity-badge"`), which independently renders `entityIcon.game` (also 🎲) next to the "Game" label — this is canonical `MeepleCard` behavior unrelated to the adapter's `coverEmoji` wiring.

`screen.getByText('🎲')` therefore threw `Found multiple elements with the text: 🎲` (a `TestingLibraryElementError`, not an assertion failure) — this is a genuine ambiguity in the canonical render, not a defect in the adapter.

**Fix**: scoped the query to the `cover-emoji-band` slot specifically, which is the DOM element the adapter's `coverEmoji` prop actually controls:

```tsx
it('renders the 🎲 cover fallback when coverUrl is missing', () => {
  // Real canonical DOM: GridCard renders 🎲 twice (Cover emoji-band via
  // coverEmoji="🎲" AND EntityBadge's default entityIcon.game glyph), so a
  // single getByText('🎲') is ambiguous. Scope to the cover-emoji-band slot
  // to verify the adapter's coverEmoji wiring specifically.
  const { container } = render(<MeepleCardGame {...baseProps} coverUrl={null} />);
  const coverBand = container.querySelector('[data-slot="cover-emoji-band"]');
  expect(coverBand).not.toBeNull();
  expect(coverBand).toHaveTextContent('🎲');
});
```

No component code was changed to "force" the original assertion — the DOM is exactly what the canonical `Cover` + `EntityBadge` parts produce; only the test's targeting was made unambiguous.

All 14 other assertions in the brief's test file matched the real canonical output exactly, with no adjustment needed:
- `href="/shared-games/{id}"` on the `<a>` root (GridCard's `Link` when `href` is set).
- `data-testid="shared-games-card"` forwarded to the anchor.
- `<h3>` heading with the title (GridCard defaults `headingLevel` to 3).
- Subtitle text node = `String(year)`.
- Subtitle omitted when `year == null`.
- Rating readout `4.0` = `rating.toFixed(1)` from `Rating.tsx` (value=4, max=5 → normalized=4, `4.0`).
- `[data-testid="connection-chip-strip"]` present when `connections` is non-empty (verified via `ConnectionChipStrip.tsx`).
- Connection strip absent when `connections=[]` (via `useConnectionSource` returning `source: 'connections', items: []`, and `GridCard` gating render on `csItems.length > 0`).
- `+3` badge text via `CardFooter`'s `badge` prop.
- No `+1` badge when `newThisWeekCount < 2` (adapter maps to `badge: undefined`).
- `<img src>` cover when `coverUrl` provided (verified against `Cover.tsx`'s `usePlaceholder` branch).
- Wikidata `<footer>` rendering, source link with `rel`/`target` attrs, and absence when `wikidataCoverLicense` omitted — all verified against the existing (unchanged) `MeepleCardAttributionFooter.tsx`, rendered by `MeepleCard.tsx` as a sibling when `entity === 'game'`.

## Files changed

- `D:\Repositories\meepleai-monorepo-main\apps\web\src\components\ui\shared-games\meeple-card-game.tsx` — body replaced with the `MeepleCard` adapter (verbatim from brief Step 3). Public prop interface unchanged except `compact` removed.
- `D:\Repositories\meepleai-monorepo-main\apps\web\src\components\ui\shared-games\meeple-card-game.test.tsx` — rewritten to the new behavioral contract (verbatim from brief Step 1, plus the one scoped-query adjustment documented above).
- `D:\Repositories\meepleai-monorepo-main\apps\web\src\components\ui\shared-games\shared-games-grid.tsx` — `SharedGamesGridGame` no longer omits/carries `compact`; `<MeepleCardGame>` no longer receives `compact={compact}`.
- `D:\Repositories\meepleai-monorepo-main\apps\web\src\components\ui\shared-games\shared-games-grid.test.tsx` — 3 selectors changed from `[data-slot="shared-games-card"]` to `[data-testid="shared-games-card"]`.

## Self-review findings

- Confirmed no other consumer in the codebase passes `compact` to `MeepleCardGame` or reads it off a `SharedGamesGridGame` object: grepped `apps/web/src/app/(public)/shared-games/page-client.tsx` (the only production consumer of `SharedGamesGridGame`) — it imports the type but never sets `compact` on the game objects it builds, so the type-narrowing is a no-op at that call site (matches the brief's Step 7 prediction).
- Grepped the whole `apps/web/src` tree for `MeepleCardGame` usages beyond the 4 changed files plus `index.ts` (barrel re-export, untouched) and `skeleton-card.tsx` (doc-comment reference only, no code coupling) — no other call sites exist.
- Verified `GridCard.tsx` + `types.ts` + `GridCard.href.test.tsx` (Task 1's already-merged interface) directly before writing the adapter, to confirm `href` really does render a `<Link prefetch>` root with `data-testid` forwarded to the anchor, and that the Wikidata footer is emitted by `MeepleCard.tsx` as a sibling of `<Renderer {...props} />` — both match the brief's stated interface exactly.
- Verified `Rating.tsx` (`value.toFixed(1)`), `ConnectionChipStrip.tsx` (`data-testid="connection-chip-strip"`, gated on `connections.length === 0 → null`), `useConnectionSource.ts` (empty array still selects `source: 'connections'` but yields 0 items, so the strip stays hidden — correctly matching the "omit when all counts are 0" test), `Cover.tsx` (`coverEmoji` prop → emoji-band fallback), and `CardFooter.tsx` (`badge` prop → `+N` footer text) before finalizing — all matched the brief's Step 3 code and the test predictions, with the single 🎲-ambiguity exception noted above.
- `pnpm exec eslint` on the 2 non-test source files (`meeple-card-game.tsx`, `shared-games-grid.tsx`) produced zero errors/warnings. The 2 test files are excluded by the project's ESLint ignore patterns (expected — same as all other `*.test.tsx` files in this repo).
- Git status before staging showed only the intended 4 files as modified (plus unrelated pre-existing untracked `docs/superpowers/plans/*.md` files from before this task started, which were left alone). Staged and committed exactly the 4 named files — no `git add -A` used.
- Pre-commit hooks (lint-staged eslint --fix, prettier --write, `tsc --noEmit`) ran and passed cleanly on commit; re-ran the full `shared-games` suite post-commit to confirm the hook's auto-formatting didn't introduce any behavioral drift — still 68/68 passing.

## Concerns

None blocking. Two minor, already-acknowledged-in-the-brief notes for awareness:

1. `MeepleCardGameLabels.ratingAriaLabel` and `.newWeekAriaLabel` are now dead props (accepted by the interface, passed through by `page-client.tsx`, but never read by the adapter) — this was called out explicitly in the brief's JSDoc as intentional, deferred to a future prune to avoid churning the page-client in this task. Confirmed still true post-implementation.
2. The `EntityBadge` top-left badge (rendering `entityIcon.game` = 🎲 next to "Game") is new visible surface area on the `/shared-games` card compared to the old standalone renderer, which had no such top-left badge. This is expected/desired per the task's stated goal ("normalizes the look to the canonical card — accepted product decision") but is worth a visual sanity-check by whoever reviews the PR, since it wasn't explicitly enumerated in the brief's test list (it only showed up as a side effect while investigating the 🎲 test ambiguity).

## Final-review fixes

Final whole-branch review of #2858 flagged a Critical (C-1) finding: routing `/shared-games` through `GridCard` `href` renders the card content inside a Next `<Link>` (`<a>`). That content included 4 interactive `<button>`s nested inside the anchor — `MenuPlaceholder`'s always-rendered "Azioni" button, plus one do-nothing `<button>` per count-only `ConnectionChip` (toolkit/agent/kb, 3 chips on the base fixture). This is invalid HTML and an axe `nested-interactive` (WCAG 4.1.2) violation.

### What was changed

1. **`apps/web/src/components/ui/data-display/meeple-card/parts/ConnectionChip.tsx`** — added an `isStatic` branch (`!disabled && !loading && !hasItems && !hasCreate && !hasOnClick && !href`) immediately before the `buttonEl` declaration. A static chip (pure count/empty display, no interactive affordance) now renders `<span role="img" aria-label={ariaLabel}>` instead of `<button>`. Interactive chips (items/create/onClick/href) and disabled chips are untouched — same `<button>`/`<Link>`/popover paths as before.
2. **`apps/web/src/components/ui/data-display/meeple-card/variants/GridCard.tsx`** — `MenuPlaceholder` is now gated on `!href` in addition to the existing `(!showQuickActions || actions.length === 0)` check, so the decorative 3-dot placeholder is omitted on anchor-rooted cards. Non-anchor (`onClick`/plain) card consumers are unaffected.
3. **`apps/web/src/components/ui/shared-games/meeple-card-game.test.tsx`** — added a regression guard (`emits no interactive element nested inside the card anchor (WCAG 4.1.2)`) asserting `link.querySelectorAll('button, a')` has length 0 on the base fixture (non-zero toolkit/agent/kb counts), proving the count chips are non-interactive and no MenuPlaceholder button leaks into the anchor.
4. **`apps/web/eslint-rules/no-standalone-card-renderer.test.js`** — the canonical-dir-exemption valid case (`GridCard.tsx` importing `Cover`) previously used the relative path `'../parts/Cover'`, which never matches the rule's `PART_IMPORT_RE` (`/\/meeple-card\/(?:parts|variants)\//`), so it passed for the wrong reason (import-boundary regex never triggered). Changed the import source to the absolute path `'@/components/ui/data-display/meeple-card/parts/Cover'` so the case actually exercises the part-path regex and is exempted only by the in-canonical-dir check (`INSIDE_MEEPLE_CARD_RE`).

### Test blast radius from Fix 1 — 3 tests updated to the corrected behavior

Running `pnpm exec vitest run src/components/ui/data-display/meeple-card` after Fix 1 (before test updates) surfaced exactly 3 failures, all asserting `getByRole('button')`/`getAllByRole('button')` on chips that are now static `<span role="img">`. Updated each to query the corrected role — no interactive-chip test was touched or weakened:

- `apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/ConnectionChip.test.tsx:81-87` (`has aria-label including count and entity label`, `entityType="session" count={5}`, no href/items/onCreate/onClick → static) — changed `screen.getByRole('button')` → `screen.getByRole('img')`.
- `apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/ConnectionChip.test.tsx:104-110` (`uses "99 or more" in aria-label when count exceeds 99`, `entityType="session" count={150}`, static) — changed `screen.getByRole('button')` → `screen.getByRole('img')`.
- `apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/ConnectionChipStrip.test.tsx:12-23` (`renders a chip per connection (footer variant)`, both connections are count-only/static) — changed `screen.getAllByRole('button')` → `screen.getAllByRole('img')`.

All interactive-chip tests (click handlers, popovers, `onCreate`/`onClick`/`href` combinations, disabled state) were left unchanged and continued to pass unmodified — confirming Fix 1 only affects the genuinely non-interactive static case.

### Verification runs (all green)

**1. `pnpm exec vitest run src/components/ui/data-display/meeple-card`**
```
Test Files  40 passed (40)
     Tests  302 passed (302)
```

**2. `pnpm exec vitest run src/components/ui/shared-games`**
```
✓ src/components/ui/shared-games/skeleton-card.test.tsx (6 tests)
✓ src/components/ui/shared-games/shared-games-hero.test.tsx (6 tests)
✓ src/components/ui/shared-games/error-state.test.tsx (6 tests)
✓ src/components/ui/shared-games/empty-state.test.tsx (8 tests)
✓ src/components/ui/shared-games/contributors-sidebar.test.tsx (8 tests)
✓ src/components/ui/shared-games/shared-games-filters.test.tsx (12 tests)
✓ src/components/ui/shared-games/shared-games-grid.test.tsx (7 tests)
✓ src/components/ui/shared-games/meeple-card-game.test.tsx (16 tests)

Test Files  8 passed (8)
     Tests  69 passed (69)
```
(16 tests in `meeple-card-game.test.tsx`, up from 15 — the new WCAG 4.1.2 regression guard.)

**3. `node --test eslint-rules/no-standalone-card-renderer.test.js`**
```
TAP version 13
# Subtest: no-standalone-card-renderer (import-boundary)
ok 1 - no-standalone-card-renderer (import-boundary)
# tests 1
# pass 1
# fail 0
```

**4. `pnpm typecheck`**
```
> @meepleai/web@ typecheck D:\Repositories\meepleai-monorepo-main\apps\web
> tsc --noEmit
```
(clean exit, no output — no type errors.)

### Files changed (staged for this fix commit)

- `apps/web/src/components/ui/data-display/meeple-card/parts/ConnectionChip.tsx`
- `apps/web/src/components/ui/data-display/meeple-card/variants/GridCard.tsx`
- `apps/web/src/components/ui/shared-games/meeple-card-game.test.tsx`
- `apps/web/eslint-rules/no-standalone-card-renderer.test.js`
- `apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/ConnectionChip.test.tsx`
- `apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/ConnectionChipStrip.test.tsx`

### Concerns

None blocking. Note for the PR reviewer: the `EntityBadge` icon glyph (🎲 for `entity="game"`) is a `role="img"`-bearing SVG inside `EntityBadge` itself and was already non-interactive pre-fix (not a `ConnectionChip`) — unaffected by this change, mentioned here only to avoid confusion with the newly-static count chips that also now carry `role="img"`.
