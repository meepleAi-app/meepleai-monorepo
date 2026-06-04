# #1842 — MeepleCard `headingLevel` prop for heading-order WCAG compliance

**Issue**: [#1842](https://github.com/meepleAi-app/meepleai-monorepo/issues/1842)
**Author**: brainstorming session 2026-06-04
**Branch**: `feature/issue-1842-meeple-card-heading-level` → PR target `main-dev`
**Status**: Design (pre-implementation)

---

## 1. Context & Problem

PR #1841 (#1569 jest-axe agents-index coverage) discovered a **real WCAG `heading-order` violation** on `AgentsLibraryView`: `AgentsHero` renders `<h1>` "Studio agenti", and each `MeepleCard` below renders `<h3>` for the card title. No `<h2>` between them → axe-core flags rule violation.

The suppression `rules: { 'heading-order': { enabled: false } }` was added inline as a temporary workaround. This issue closes the debt by adding `headingLevel?: 2 | 3 | 4` prop to `MeepleCard` and migrating offending consumer surfaces.

### Cross-cutting impact

Audit of all 59 `MeepleCard` consumers (non-test) reveals:
- **20 NEEDS_MIGRATION** (high confidence): grid-below-h1-hero pattern, axe violation expected
- **25 SKIP**: single-card detail / admin-only / unused / internal / modal contexts (no h1 above)
- **7 NEEDS_INSPECTION**: ambiguous (polymorphic primitive, currently unused, adapter prop-passthrough)

Stima iniziale issue body ("~10 surfaces") era **sottostima 2x**. Audit produces precise migration list (§4).

### Existing suppressions (2 known)

1. `apps/web/src/app/(authenticated)/agents/_components/__tests__/AgentsLibraryView.test.tsx:359` — added 2026-06-02 sessione 29 #1841 (P164 pattern: rule suppression + follow-up issue tracking)
2. `apps/web/src/components/features/gamebook/__tests__/LibroGameDetailView.test.tsx:160` — added precedentemente per cluster #1481

Entrambe vanno rimosse post-fix.

---

## 2. Decisions

Locked in brainstorming session 2026-06-04 + AskUserQuestion responses:

| ID | Decision | Rationale |
|---|---|---|
| **DEC-1** | Audit sistematico 59 consumers + categorize | Issue body sottostima scope ("10 surfaces"). Audit produces precise NEEDS_MIGRATION/SKIP/INSPECTION table. Zero false-negatives. |
| **DEC-2** | Approach A — `headingLevel?: 2 \| 3 \| 4` prop additivo, default `3` | Issue body raccomandazione + Fowler clean code (backwards compat). Alternative Option B (demote hero h1 → h2 + sr-only page h1) era rejected come più invasiva. |
| **DEC-3** | Per-consumer migration (NON default change to `2`) | Default change a `2` rompe consumer detail/single-card legittimi. Esplicito su 20 surfaces > implicit blanket change. Boilerplate noise accettabile. |
| **DEC-4** | 5-8 nuovi axe scan key surfaces | Re-enable 2 esistenti (AgentsLibraryView + LibroGameDetailView) + add new scans su: `LibraryHub.test`, `HubToolkitsBody.test` (esistente sostituito), `PlayersLibraryView.test`, `GamesResultsGrid.test`, `CollectionGameGrid.test`, `GameNightPlanningLayout.test`. Bilancia coverage vs effort. |
| **DEC-5** | DOM structure assertion only (NO visual regression) | Memoria sessione 31 DEC-6 — Visual Gate REMOVED 2026-05-20 high false-positive. Vitest test confirma `tagName === 'H2'` + className/style invariati. |
| **DEC-6** | Defer NEEDS_INSPECTION 7 files | `entity-list-view` polymorphic primitive + `shared-games-grid` passthrough + adapters (MeepleChatCard via HomeFeed) → tracked in PR follow-up comment. Non blocca chiusura #1842. |
| **DEC-7** | Variants in scope: GridCard + ListCard + FeaturedCard + HeroCard (4/6) — FocusCard già `<h2>` (no migration needed), CompactCard usa `<span>` (no heading element) | Audit: 4 variants render `<h3>` per il title (GridCard:78, ListCard:52, FeaturedCard:62, HeroCard:81). FocusCard:50 usa `<h2>` (design intent: full-focus page-hero card). CompactCard:30 usa `<span>` (compact ticker, no semantic heading). FocusCard accetta prop con default `2` per consistency; CompactCard skipped (no heading element to migrate). |

---

## 3. Architecture

Modifichiamo il **primitive MeepleCard** aggiungendo prop additiva `headingLevel?: 2 | 3 | 4` su `MeepleCardProps`. Ogni variant (`GridCard.tsx`, `ListCard.tsx`, `CompactCard.tsx`, `FeaturedCard.tsx`, `FocusCard.tsx`, `HeroCard.tsx`) consuma il prop e renderizza l'heading tag corrispondente.

Il prop è **type-safe** via TypeScript literal union `2 | 3 | 4` — TS impedisce passing valori invalid a compile-time.

Dynamic tag rendering pattern (React idiomatic):

```tsx
const HeadingTag = `h${headingLevel ?? 3}` as const;
return <HeadingTag className={...}>{title}</HeadingTag>;
```

Backwards compat: `headingLevel={undefined}` → default `3` (current behavior). Tutti i 25 SKIP consumer + 7 NEEDS_INSPECTION continuano render `<h3>` senza modifiche.

**Per-consumer migration** (20 NEEDS_MIGRATION): aggiungi `headingLevel={2}` al call site. Tipicamente 1 line change per consumer.

---

## 4. Components & file map

### Primitive changes (1 file types + 6 variants)

| File | Change | What |
|---|---|---|
| `apps/web/src/components/ui/data-display/meeple-card/types.ts` | **edit** | Add `headingLevel?: 2 \| 3 \| 4` to `MeepleCardProps` with JSDoc explaining DEC-3 (per-consumer migration). Default 3 documented. |
| `apps/web/src/components/ui/data-display/meeple-card/variants/GridCard.tsx` | **edit** | Destructure `headingLevel`. Render `<HeadingTag>` instead of hardcoded `<h3>` (line 78). |
| `apps/web/src/components/ui/data-display/meeple-card/variants/ListCard.tsx` | **edit** | Same dynamic tag pattern. Line 52: `<h3>` → `<HeadingTag>`. |
| `apps/web/src/components/ui/data-display/meeple-card/variants/FeaturedCard.tsx` | **edit** | Same dynamic tag pattern. Line 62: `<h3>` → `<HeadingTag>`. |
| `apps/web/src/components/ui/data-display/meeple-card/variants/HeroCard.tsx` | **edit** | Same dynamic tag pattern. Line 81: `<h3 className="text-2xl ...">` → `<HeadingTag className="text-2xl ...">`. |
| `apps/web/src/components/ui/data-display/meeple-card/variants/FocusCard.tsx` | **edit** | Already renders `<h2>` (line 50) hardcoded. Migrate to `<HeadingTag>` with `default 2` (preserve current visual behavior). |
| ~~CompactCard~~ | **skip** | Uses `<span>` (line 30), no heading element. Compact-ticker variant intentionally non-semantic. |

### Consumer migration (20 NEEDS_MIGRATION files)

| File | Action |
|---|---|
| `apps/web/src/components/features/agents/AgentsResultsGrid.tsx` | Pass `headingLevel={2}` to MeepleCard |
| `apps/web/src/components/features/games/GamesResultsGrid.tsx` | Pass `headingLevel={2}` |
| `apps/web/src/components/features/players/PlayersResultsGrid.tsx` | Pass `headingLevel={2}` |
| `apps/web/src/components/features/library/LibraryHybridGrid.tsx` | Pass `headingLevel={2}` |
| `apps/web/src/components/features/home/HomeFeed.tsx` | Pass `headingLevel={2}` |
| `apps/web/src/components/library/MeepleLibraryGameCard.tsx` | Pass `headingLevel={2}` |
| `apps/web/src/components/library/MeepleUserLibraryCard.tsx` | Pass `headingLevel={2}` |
| `apps/web/src/components/catalog/MeepleGameCatalogCard.tsx` | Pass `headingLevel={2}` |
| `apps/web/src/components/wishlist/MeepleWishlistCard.tsx` | Pass `headingLevel={2}` |
| `apps/web/src/app/(authenticated)/library/wishlist/page.tsx` | Pass `headingLevel={2}` (via MeepleWishlistCard prop) |
| `apps/web/src/app/(public)/library/shared/[token]/page.tsx` | Pass `headingLevel={2}` |
| `apps/web/src/app/(authenticated)/gamebook/upload/_components/GamebookUploadClient.tsx` | Pass `headingLevel={2}` |
| `apps/web/src/components/chat/entry/AgentSelector.tsx` | Pass `headingLevel={2}` |
| `apps/web/src/components/chat/entry/GameSelector.tsx` | Pass `headingLevel={2}` |
| `apps/web/src/components/chat-unified/AgentCreationWizard.tsx` | Pass `headingLevel={2}` |
| `apps/web/src/components/game-night/MeepleEventCard.tsx` | Pass `headingLevel={2}` |
| `apps/web/src/components/game-night/planning/MeepleGameNightCard.tsx` | Pass `headingLevel={2}` |
| `apps/web/src/components/game-night/planning/MeepleDealtGameCard.tsx` | Pass `headingLevel={2}` |
| `apps/web/src/components/game-night/planning/MeepleAISuggestionCard.tsx` | Pass `headingLevel={2}` |
| `apps/web/src/components/library/private-game-detail/MeeplePausedSessionCard.tsx` | Pass `headingLevel={2}` |

### Test re-enable (2 existing suppressions removed)

| File | Action |
|---|---|
| `apps/web/src/app/(authenticated)/agents/_components/__tests__/AgentsLibraryView.test.tsx:359` | Remove `rules: { 'heading-order': { enabled: false } }` override. Comment cleanup. |
| `apps/web/src/components/features/gamebook/__tests__/LibroGameDetailView.test.tsx:160` | Same. |

### New axe scans (5-8 key surfaces)

| Test file | What |
|---|---|
| `apps/web/src/components/features/library-hub/__tests__/LibraryHub.test.tsx` | Add jest-axe scan with `heading-order` enabled (assumes file exists; otherwise create) |
| `apps/web/src/components/features/library-hub/__tests__/HubToolkitsBody.test.tsx` | Same |
| `apps/web/src/components/features/players/__tests__/PlayersResultsGrid.test.tsx` | Same (verify exists; create if missing) |
| `apps/web/src/components/features/games/__tests__/GamesResultsGrid.test.tsx` | Same |
| `apps/web/src/components/collection/__tests__/CollectionGameGrid.test.tsx` | Same |
| `apps/web/src/components/game-night/planning/__tests__/GameNightPlanningLayout.test.tsx` | Same |

### DOM smoke (visual stability)

| Test file | What |
|---|---|
| `apps/web/src/components/ui/data-display/meeple-card/__tests__/heading-level.smoke.test.tsx` | NEW. Render each variant with `headingLevel={2}` vs default `3` → assert `tagName` swap correct + computed `font-family` / `font-size` / `font-weight` unchanged. 1 test per 6 variants. |

---

## 5. Data flow

```
Consumer (e.g. AgentsResultsGrid) → <MeepleCard headingLevel={2} {...props} />
                ↓
         MeepleCardImpl (variant default 'grid')
                ↓
            GridCard ({ headingLevel, title, ... })
                ↓
        const HeadingTag = `h${headingLevel ?? 3}` as const;
        return (
          ...
          <HeadingTag className={...}>{title}</HeadingTag>
          ...
        );
```

TypeScript literal union `2 | 3 | 4` → enforces compile-time safety. Default fallback `?? 3` covers `undefined` (backwards compat) e edge case TS-bypass.

---

## 6. Error handling

| Scenario | Behavior |
|---|---|
| `headingLevel` undefined | Default `3` (current behavior preserved) |
| `headingLevel` invalid (e.g. `1` o `5`) | TypeScript blocks at compile-time (literal union) |
| `headingLevel` invalid runtime bypass (e.g. via `as any`) | `as const` template literal infers `h${number}` → React renders correctly if valid HTML heading; else React warning. Acceptable since TS already guards. |
| Multiple cards in same page | Each renders its own `<h2>` independently — multiple h2 siblings in same parent is WCAG-valid (heading-order ≥ siblings same level OK). |

---

## 7. Testing strategy

### TDD per-variant (5 variants — CompactCard skipped)

Per ogni variant edit (`GridCard`, `ListCard`, `FeaturedCard`, `HeroCard`, `FocusCard`):

1. **Failing test**: assert `<h2>` rendered when `headingLevel={2}` passed; assert `<h3>` when omitted (for `GridCard/ListCard/FeaturedCard/HeroCard`); assert `<h2>` when omitted (for `FocusCard` — different default).
2. **Implementation**: dynamic `HeadingTag = \`h${headingLevel ?? defaultLevel}\`` where `defaultLevel = 3` for grid/list/featured/hero, `2` for FocusCard.
3. **Verify passing**.

`CompactCard` is **out of scope** for TDD (no heading element).

### Re-enable 2 existing suppressions

Remove `rules: { 'heading-order': { enabled: false } }` from:
- `AgentsLibraryView.test.tsx` (assumes `AgentsResultsGrid` passes `headingLevel={2}` post-migration)
- `LibroGameDetailView.test.tsx` (assumes that view's MeepleCard passes `headingLevel={2}` — verify se è uno dei SKIP o NEEDS_MIGRATION)

### Add 5-8 new axe scans (DEC-4)

For each key surface, render with jest-axe scan **with heading-order rule ENABLED** (default axe config, no override). Verify 0 violations post-migration.

### DOM structure smoke (DEC-5)

`__tests__/heading-level.smoke.test.tsx` — 6 tests (1 per variant):

```tsx
it('GridCard renders h2 when headingLevel={2} (visual props unchanged)', () => {
  const { rerender, getByText } = render(
    <MeepleCard entity="game" title="X" variant="grid" />
  );
  const h3 = getByText('X');
  expect(h3.tagName).toBe('H3');
  const h3Classes = h3.className;

  rerender(<MeepleCard entity="game" title="X" variant="grid" headingLevel={2} />);
  const h2 = getByText('X');
  expect(h2.tagName).toBe('H2');
  expect(h2.className).toBe(h3Classes); // identical styling
});
```

---

## 8. Out of scope

- ❌ **NEEDS_INSPECTION 7 files** — `entity-list-view` polymorphic primitive prop-passthrough, `shared-games-grid` passthrough, `MeepleChatCard` adapter, `MeepleContributorCard` deep-section, `MeepleParticipantCard` Scoreboard label, `PrivateGamesClient` unused-h1-context, `LibraryPanel` unused. Tracked as follow-up comment on PR.
- ❌ **25 SKIP consumer** — single-card detail / admin / unused / internal / modal. No change needed.
- ❌ **Visual regression test infra** — Visual Gate REMOVED 2026-05-20 (DEC-5).
- ❌ **Option B alternative** — demote hero h1 → h2 + sr-only page h1 (issue body rejected).
- ❌ **BE changes** — purely FE.
- ❌ **`headingLevel={4}`** — out of scope, included only in type signature for future flexibility (h2/h3/h4 supported).

---

## 9. Rollout & risk

### Risk matrix

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Visual regression on 20 migrated cards | Medium | Low | DEC-5 DOM smoke assertions verify className/font props unchanged; designer review post-preview-deploy (manual gate). |
| `<h2>` font-size diverges from `<h3>` default in some Tailwind setups | Low | Low | Tailwind v4 doesn't apply default heading styles unless `@tailwindcss/typography` is enabled. Our v2 tokens use explicit `font-[var(--font-quicksand)] text-[0.95rem] font-bold` on the heading — same className regardless of tagName. |
| axe-core still flags violations post-migration | Medium | Medium | New axe scans (DEC-4) catch this in CI before merge. If any surface still fails, re-audit + classify NEEDS_MIGRATION expansion. |
| NEEDS_INSPECTION 7 files surface later as violations | Low | Medium | Follow-up PR; tracked in PR body comment. |
| Adapter components (MeepleChatCard) miss the migration | Low | Low | DEC-6 documented; follow-up issue if axe re-enable on HomeFeed.test surfaces it. |

### Rollback plan

- Revert PR → primitive restored to current state, `<h3>` everywhere
- Database: zero changes (FE-only)
- No breaking changes for ANY consumer (additive prop, default backward-compat)

---

## 10. References

- Issue: [#1842](https://github.com/meepleAi-app/meepleai-monorepo/issues/1842)
- Source PR (suppression added): [#1841](https://github.com/meepleAi-app/meepleai-monorepo/pull/1841)
- Origin issue (jest-axe coverage): [#1569](https://github.com/meepleAi-app/meepleai-monorepo/issues/1569)
- Memory pattern P164: axe-rule-suppression-with-tracked-followup (documented sessione 29)
- Audit completed: 2026-06-04 (20 NEEDS_MIGRATION + 25 SKIP + 7 NEEDS_INSPECTION)
- Primitive: `apps/web/src/components/ui/data-display/meeple-card/`
- Spec author: brainstorming session 2026-06-04
