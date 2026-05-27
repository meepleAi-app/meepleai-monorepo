# Library games-tab wireup — design (#1566)

**Status**: draft (2026-05-27)
**Owner**: aaron (DegrassiAaron)
**Related issues**: #1566 (this), #1521 (`/games` → `/library` redirect), #1567 (PR scrapping `GamesLibraryView`), #1618 (Phase 2a hybrid hub), #633 (Wave B.1 mockup-faithful Games* components)
**Mockup source**: `admin-mockups/design_files/sp4-games-index.{html,jsx}`

---

## 1. Problem statement

PR **#1567** (merged 2026-05-26 07:44Z) replaced `/games/page.tsx` with `redirect('/library')` and removed `GamesLibraryView` — the orchestrator that consumed the 5 `features/games/*` shelf-ready components implementing the `sp4-games-index` mockup (Wave B.1 #633). The 5 components remained tested (46 unit tests green) but **orphaned**: no page consumed them.

Issue **#1566** was opened the same day asking to "Refactor `LibraryHub` to consume the 5 shelf-ready components (replacing its older bespoke UI)".

**Complication**: PR **#1618** (merged 2026-05-26 ~12:30Z) reimplemented `LibraryHub` as a multi-entity hybrid hub orchestrator (6 tabs: `all / games / agents / kb / sessions / chat`), using `useHybridHubItems` + `HybridHubItem[]` + `LibraryHybridGrid`. The "older bespoke UI" the issue meant to replace no longer exists; replacing the hybrid hub wholesale would undo the work of #1618.

**Reconciled scope**: surgical wireup limited to **the `games` tab of `LibraryHub`**. The hybrid hub multi-tab architecture stays intact for `all / agents / kb / sessions / chat`. The `games` tab body is rebuilt to be **mockup-faithful** with the shelf-ready components.

---

## 2. Constraints (from user directive)

1. **Mockup-strict**: the `games` tab body must follow `sp4-games-index` mockup. No deviations beyond what the multi-tab context strictly requires.
2. **No scope creep into other issues**: do not implement features that belong to other tracked issues (e.g. `GamesRecentRail` is part of game-night flow G3 design, not `sp4-games-index`).
3. **Preserve #1618**: the hybrid hub orchestration (other tabs, cross-entity hero, recent activity rail) is untouched.

---

## 3. Architecture

### 3.1 Composition

```
LibraryHub  (apps/web/src/app/(authenticated)/library/_components/LibraryHub.tsx)
├── LibraryHeroDesktop                        (KEEP — cross-entity hub header from #1618)
├── LibraryTabs                                (KEEP — 6 tabs from #1618)
│
├── if (tab === 'games')                       ◀── NEW BRANCH
│   ├── GamesFiltersInline                    (NEW — replaces inline toolbar + CrossEntityFilters STATO chip in this branch)
│   └── [FSM]
│       ├── GamesEmptyState  kind="loading"   (when libraryQuery.isLoading)
│       ├── GamesEmptyState  kind="empty"     (when entries.length === 0)
│       ├── GamesEmptyState  kind="filtered-empty" (when filtered.length === 0 but entries.length > 0)
│       ├── GamesEmptyState  kind="error"     (when libraryQuery.isError)
│       └── GamesResultsGrid                  (default — UserLibraryEntry[] driven)
│
├── else                                       (tab !== 'games' — UNCHANGED from #1618)
│   ├── CrossEntityFilters                    (STATO chip — degenerates outside games tab anyway)
│   ├── inline toolbar (search/sort/view)
│   ├── LibraryHybridGrid
│   └── EmptyLibrary
│
└── RecentActivityRail                         (KEEP — cross-entity feed, side rail)
```

### 3.2 Components matrix (5 shelf-ready)

| Component | In #1566? | Rationale |
|---|---|---|
| `GamesFiltersInline` | ✅ wired | Mockup has `GameFilters` (status/sort/search/view) inline above grid. Replaces inline toolbar + CrossEntityFilters STATO chip in the `games` tab. |
| `GamesResultsGrid` | ✅ wired | Mockup has `GameCardGrid` (4-col MeepleCard grid). Replaces `LibraryHybridGrid` in the `games` tab. |
| `GamesEmptyState` | ✅ wired | Mockup has `LoadingState` (8 skeleton cards) + `EmptyLibrary` (`kind: empty/filtered`) + `ErrorState`. Replaces `EmptyLibrary` in the `games` tab. |
| `GamesHero` | ❌ **NOT wired** | Mockup has `LibraryHero` (game-only header), but `LibraryHeroDesktop` (cross-entity, from #1618) is the canonical header of the multi-tab hub. Stacking two heroes would break the hub layout. **Decision**: `GamesHero` stays shelf-ready; track removal in a follow-up if `LibraryHeroDesktop` proves sufficient long-term. |
| `GamesRecentRail` | ❌ **NOT wired** | `GamesRecentRail` was created for **game-night user flow G3** (spec `2026-05-09-game-night-user-flow-design.md`), not `sp4-games-index`. Including it here is scope creep per constraint 2. Stays shelf-ready for the G3 flow. |

### 3.3 Data flow

```typescript
// Inside LibraryHub (extends existing state)
const libraryQuery = useLibrary({
  page: 1,
  pageSize: 50,
  sortBy: 'addedAt',
  sortDescending: true,
});
const entries: readonly UserLibraryEntry[] = libraryQuery.data?.items ?? [];
```

**Why a second `useLibrary` call is safe**: `useHybridHubItems` already calls `useLibrary` with the identical key (verified at `apps/web/src/hooks/queries/useHybridHubItems.ts:41-46`). TanStack Query dedups requests with matching keys → one real HTTP fetch, two reference points. Pattern is explicit and self-documenting; the alternative (an inverse `GameHubItem → UserLibraryEntry`-like mapper) would be lossy and add maintenance burden.

#### 3.3.1 Backend prerequisite (decided 2026-05-27) — Task 1A

The list DTO `UserLibraryEntryDto` (`apps/api/src/Api/BoundedContexts/UserLibrary/Application/DTOs/UserLibraryEntryDto.cs`) does **not** currently expose `TimesPlayed`/`LastPlayed` — only the *detail* DTO `GameDetailDto` does. The mockup's `played` filter and `last-played` sort need those fields on every list row.

**Decision (user, 2026-05-27): enrich the list DTO with real data** rather than use proxies. This is cheap because the data is already loaded:

- `UserLibraryEntryEntity` persists `TimesPlayed` + `LastPlayed` as **direct columns** (confirmed: `UserLibraryRepository.cs:608-611` writes them; `:397-403` reads them in `MapToDomain`).
- `GetUserLibraryPaginatedAsync` builds entries via `MapToDomain` (`UserLibraryRepository.cs:127`), so `entry.Stats.TimesPlayed` / `entry.Stats.LastPlayed` are **already materialized** on each domain entry in the list handler — no `Include(Sessions)`, no N+1, no migration.

Task 1A therefore: (a) add `TimesPlayed` (int) + `LastPlayed` (DateTime?) to the `UserLibraryEntryDto` record, (b) populate both from `entry.Stats` in **both** branches of `GetUserLibraryQueryHandler` (shared-game ~line 155, private-game ~line 194), (c) add `timesPlayed: z.number()` + `lastPlayed: z.string().datetime().nullable()` to `UserLibraryEntrySchema` (FE zod), (d) integration test asserting the list endpoint returns the fields.

### 3.4 Filter logic (games tab)

Mockup `STATUS_OPTS = ['all', 'owned', 'wishlist', 'played']` maps to `UserLibraryEntry.currentState` (enum `'Nuovo' | 'InPrestito' | 'Wishlist' | 'Owned'`) and `timesPlayed`:

| `GamesStatusKey` | Predicate |
|---|---|
| `all` | no filter |
| `owned` | `entry.currentState !== 'Wishlist'` (i.e. `Nuovo` ∪ `InPrestito` ∪ `Owned`) |
| `wishlist` | `entry.currentState === 'Wishlist'` |
| `played` | `entry.timesPlayed > 0` |

Mockup `SORT_OPTS = ['last-played', 'rating', 'title', 'year']`:

| `GamesSortKey` | Sort field (on `UserLibraryEntry`) |
|---|---|
| `last-played` | `entry.lastPlayed` desc (nulls last) |
| `rating` | `entry.averageRating` desc (nulls last) |
| `title` | `entry.gameTitle` asc (Intl.Collator) |
| `year` | `entry.gameYearPublished` desc (zeros last) |

Query (search): substring match on `entry.gameTitle` (case-insensitive). Mockup also mentions "autore, anno" in the placeholder — out of scope MVP because `entry` exposes `gamePublisher` but not author, and year-as-string is awkward. Tracked in §7.

### 3.5 Bulk selection in the `games` tab

The mockup `sp4-games-index` **does not include bulk selection**. The current `LibraryHub` bulk mode (`BulkSelectionBar` + `selectionMode` + "Selezione" button in toolbar) is game-scoped (`tab === 'games'` guard) and inherited from #1618.

**Decision**: in the `games` tab body post-#1566, the inline toolbar is replaced by `GamesFiltersInline` which **has no `Selezione` button**. The selection mode UI entry point disappears from the `games` tab. The underlying state (`selectionMode`, `selected`) and `BulkSelectionBar` rendering condition remain in `LibraryHub` so:
- (a) re-introducing bulk in a follow-up only needs to add the entry button somewhere,
- (b) the `useRemoveGameFromLibrary` mutation and its plumbing are not orphaned in this PR.

This is **strict mockup conformity**, not feature removal — the mutation and infra remain available, only the UI affordance is hidden.

### 3.6 `CrossEntityFilters` in the `games` tab

`CrossEntityFilters` exposes the STATO chip (game-state filters `Nuovo / Played / Loaned / withKb`). Post-#1566 the chip is **hidden** when `tab === 'games'` because `GamesFiltersInline` owns the filter UI. For `tab !== 'games'`, `CrossEntityFilters` already degenerates to "nothing visible" (per #1618 implementation), so this is consistent.

### 3.7 i18n

New keys under `pages.library.gamesTab.*` in `apps/web/src/locales/{it,en}.json`:

```jsonc
{
  "pages.library.gamesTab": {
    "filters.search.placeholder":  "Cerca per titolo, autore, anno…",      // mockup line 367
    "filters.search.ariaLabel":    "Cerca giochi nella tua libreria",
    "filters.search.clearAriaLabel": "Pulisci ricerca",
    "filters.status.label":        "Stato",
    "filters.status.options.all":      "Tutti",                            // mockup STATUS_OPTS
    "filters.status.options.owned":    "Posseduti",
    "filters.status.options.wishlist": "Wishlist",
    "filters.status.options.played":   "Giocati",
    "filters.sort.label":          "Ordina",                               // mockup line 414
    "filters.sort.options.last-played": "Ultima partita",
    "filters.sort.options.rating":      "Rating",
    "filters.sort.options.title":       "Titolo A-Z",
    "filters.sort.options.year":        "Anno",
    "filters.view.label":          "Vista",
    "filters.view.options.grid":   "Griglia",
    "filters.view.options.list":   "Lista",
    "filters.resultCount":         "{count, plural, one {# gioco} other {# giochi}}",
    "emptyState.empty.title":      "Aggiungi il tuo primo gioco",
    "emptyState.empty.subtitle":   "Costruisci la tua libreria…",
    "emptyState.empty.cta":        "Aggiungi gioco",
    "emptyState.filteredEmpty.title":    "Nessun risultato",
    "emptyState.filteredEmpty.subtitle": "Prova ad allargare i filtri…",
    "emptyState.filteredEmpty.cta":      "Azzera filtri",
    "emptyState.error.title":      "Errore di caricamento",
    "emptyState.error.subtitle":   "Impossibile recuperare la libreria…",
    "emptyState.error.cta":        "Riprova"
  }
}
```

English mirror in `en.json` with same structure. Exact strings to be finalized during implementation (proposed text above is a starting point).

---

## 4. State / event model

```
[LibraryHub state — additions]
gamesStatus: GamesStatusKey       (default: 'all')
gamesSort:   GamesSortKey         (default: 'last-played' to match mockup)
gamesQuery:  string               (default: '')
gamesView:   GamesViewKey         (default: 'grid')

[derivations]
entries          = libraryQuery.data?.items ?? []
filtered         = applyFilter(entries, gamesStatus) then applySearch(., gamesQuery) then applySort(., gamesSort)
gamesKind        = derive('loading' | 'empty' | 'filtered-empty' | 'error' | 'default')
                   from (libraryQuery.isLoading, libraryQuery.isError, entries.length, filtered.length)
                   respecting stateOverride hatch when STATE_OVERRIDE_ENABLED

[guards]
gamesKind derivation only runs when tab === 'games'; otherwise existing effectiveKind from #1618 applies.
```

The existing hub-level `effectiveKind` (from #1618) keeps driving the non-games tabs. Inside `tab === 'games'` we derive a games-specific kind from the dedicated `libraryQuery` (no dependency on `useHybridHubItems.allFailed`).

---

## 5. Acceptance criteria

- [ ] **(Task 1A — backend)** `UserLibraryEntryDto` exposes `TimesPlayed` (int) + `LastPlayed` (DateTime?), populated from `entry.Stats` in both branches of `GetUserLibraryQueryHandler`. FE `UserLibraryEntrySchema` mirrors the two fields. Integration test asserts the `GET /api/v1/library` list returns them. No EF query change (columns already materialized via `MapToDomain`).
- [ ] When `tab === 'games'`, `LibraryHub` renders `GamesFiltersInline` + `GamesResultsGrid` (or `GamesEmptyState`) in place of `CrossEntityFilters` + inline toolbar + `LibraryHybridGrid` + `EmptyLibrary`.
- [ ] When `tab !== 'games'`, `LibraryHub` renders unchanged from #1618 (hybrid hub branch).
- [ ] `useLibrary({page:1, pageSize:50, sortBy:'addedAt', sortDescending:true})` feeds `UserLibraryEntry[]` directly to `GamesResultsGrid`; no `HybridHubItem → UserLibraryEntry` adapter.
- [ ] Filter mapping per §3.4 is implemented and unit-tested.
- [ ] Sort mapping per §3.4 is implemented and unit-tested.
- [ ] FSM per §4 covers all 5 kinds and respects the `?state=` override hatch under `STATE_OVERRIDE_ENABLED`.
- [ ] i18n keys added under `pages.library.gamesTab.*` in both `it.json` and `en.json`. Orchestrator resolves labels and injects via props.
- [ ] No regression in existing `LibraryHub.test.tsx` (62 tests). Add 6-8 new tests covering the games-tab branch.
- [ ] Update `e2e/smoke-real-backend/games.smoke.spec.ts` test: assert that after the redirect to `/library`, switching to `?tab=games` renders `GamesResultsGrid` (selector `[data-slot="games-results-grid"]`).
- [ ] Unskip `e2e/a11y/games-library.spec.ts` (was skipped in PR #1619 / #1612). Retarget URL to `/library?tab=games`. Verify axe scans pass on default + filtered-empty.
- [ ] `docs/for-developers/frontend/v2-migration-matrix.md`: move the 3 rows (`GamesFiltersInline`, `GamesResultsGrid`, `GamesEmptyState`) from `shelf-ready` → `done` with PR reference. `GamesHero` and `GamesRecentRail` stay `shelf-ready` with notes pointing to follow-up trackers (§7).
- [ ] DS-15 token compliance: no hardcoded color utilities. Components already pass (per #633); orchestrator additions must too.

---

## 6. Tests

### Unit (`LibraryHub.test.tsx`)

Additions on top of the 62 existing tests:

1. `tab='games' renders GamesFiltersInline` (not inline toolbar nor CrossEntityFilters)
2. `tab='games' renders GamesResultsGrid with UserLibraryEntry[]` (assert presence + 1 entry passed through)
3. `tab='games' status=wishlist filters to currentState='Wishlist' entries` (boundary)
4. `tab='games' status=played filters to entries with timesPlayed > 0`
5. `tab='games' renders GamesEmptyState kind='empty' when library is empty`
6. `tab='games' renders GamesEmptyState kind='filtered-empty' when filter eliminates all entries`
7. `tab='games' renders GamesEmptyState kind='error' when libraryQuery.isError`
8. `tab='all'/'sessions'/'chat' renders LibraryHybridGrid` (regression guard for #1618)

### E2E smoke (`games.smoke.spec.ts`)

Extend the existing test (already updated in #1619/#1612) to assert post-redirect that `?tab=games` renders `GamesResultsGrid`:

```typescript
await page.goto('/library?tab=games', { waitUntil: 'domcontentloaded' });
await page.waitForSelector('[data-slot="games-results-grid"]', { timeout: 30_000 });
```

### A11y (`games-library.spec.ts`)

Unskip the suite (was set to `test.describe.skip` in PR #1619). Retarget URL: `/games?tab=library` → `/library?tab=games`. Update selectors as needed (the `[data-slot="games-results-grid-link"]` / `[data-slot="games-empty-state"]` ones remain valid because the components are unchanged).

---

## 7. Out of scope (tracked separately)

| Item | Why out of scope | Tracker |
|---|---|---|
| Wire `GamesHero` (mockup `LibraryHero`) | Conflicts with cross-entity `LibraryHeroDesktop` from #1618 in the multi-tab hub | follow-up (new issue): decide if GamesHero should replace LibraryHeroDesktop in the games tab, stay shelf-ready, or be removed |
| Wire `GamesRecentRail` | Part of game-night user flow G3 (spec `2026-05-09-game-night-user-flow-design.md §G3`), not `sp4-games-index` | the existing G3 work — track its consumption there |
| Bulk selection mode in games tab | Not in `sp4-games-index` mockup | follow-up (new issue): if bulk is needed, extend the mockup first |
| Search by author / year | `UserLibraryEntry` exposes `gamePublisher` not author; year is `gameYearPublished` (number) which would need pretty-format | follow-up (P3): extend filter logic and result count tooltip |
| Loaned / withKb filters in games tab | Were added by #1618 `CrossEntityFilters` STATO chip, not in `sp4-games-index` mockup | follow-up (P3): if needed, extend the mockup with the additional filter chips |
| Removal/deprecation of `features/games/GamesHero.tsx`, `GamesRecentRail.tsx` | Out of #1566 scope (no code deletion without explicit ask) | follow-up tracker |
| Pagination beyond the first 50 entries | `useHybridHubItems` and the new `useLibrary` call both cap at `pageSize: 50` (cap from #1618). The mockup `sp4-games-index` is static (24 items) and does not specify pagination. Users with > 50 library entries will see only the first 50 in the `games` tab. | follow-up: add `useInfiniteQuery` or page controls if needed |

---

## 8. Risks and mitigation

| Risk | Mitigation |
|---|---|
| Double `useLibrary` call surprises future readers (looks like a bug at first glance) | Comment block at the call site explaining the TanStack dedup guarantee; reference `useHybridHubItems.ts:41-46` |
| Filter logic divergence from `useHybridHubItems` (different cap, different sort) | `useHybridHubItems` caps to 20 items per source for hub display; the games tab uses the full 50 from `useLibrary`. Document explicitly in §3.3. |
| i18n key collision with existing `pages.library.*` | Namespace under `gamesTab.*` to isolate. Verify via grep before commit. |
| a11y regressions on `GamesFiltersInline` tablist after re-introduction | Run jest-axe in unit + axe-playwright in e2e; `games-library.spec.ts` unskipping is part of acceptance. |
| `LibraryHub.test.tsx` flakiness with the new branch | Mock `useLibrary` directly (same pattern used for `useHybridHubItems` in existing tests). |

---

## 9. References

- Issue **#1566** (this), **#1521** (decision), **#1567** (PR scrapping GamesLibraryView), **#1618** (Phase 2a hybrid hub), **#633** (Wave B.1 mockup-faithful Games* origin), **#1619** (smoke test fix for #1612)
- Mockup: `admin-mockups/design_files/sp4-games-index.{html,jsx}`
- Spec: `docs/superpowers/specs/2026-04-29-v2-migration-wave-b-1-games.md` (Wave B.1 original)
- Code:
  - `apps/web/src/app/(authenticated)/library/_components/LibraryHub.tsx` (target — 432 LOC)
  - `apps/web/src/components/features/games/{GamesFiltersInline,GamesResultsGrid,GamesEmptyState,GamesHero,GamesRecentRail}.tsx`
  - `apps/web/src/hooks/queries/useHybridHubItems.ts` (line 41-46: dedup pattern)
  - `apps/web/src/lib/api/schemas/library.schemas.ts` (`UserLibraryEntry`, `GameStateType`)
- v2 migration matrix: `docs/for-developers/frontend/v2-migration-matrix.md`
