# Library games-tab wireup — Implementation Plan (#1566)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire the 3 mockup-faithful shelf-ready components (`GamesFiltersInline`, `GamesResultsGrid`, `GamesEmptyState`) into the `games` tab of `LibraryHub`, replacing the hybrid-hub generic UI in that tab only.

**Architecture:** Branch `LibraryHub` rendering on `tab === 'games'`. The games branch derives `UserLibraryEntry[]` from a dedicated `useLibrary()` call (TanStack-deduped against `useHybridHubItems`'s internal call), applies pure filter/sort/search functions, and feeds the 3 Games* components. All other tabs keep the #1618 hybrid hub path verbatim.

**Tech Stack:** Next.js 16 (App Router), React 19, TanStack Query, react-intl, Vitest + Testing Library, Playwright (smoke + a11y axe-core).

**Design spec:** `docs/superpowers/specs/2026-05-27-1566-library-games-tab-wireup-design.md`

**Pre-requisite:** PR #1619 (smoke + a11y fix for #1612) should be merged to `main-dev` before Task 6 to avoid conflicts on `games.smoke.spec.ts` and `games-library.spec.ts`. If not merged, rebase those two files at Task 6.

---

## File Structure

| File | Action | Responsibility |
|---|---|---|
| `apps/api/src/Api/BoundedContexts/UserLibrary/Application/DTOs/UserLibraryEntryDto.cs` | **Modify** | Add `TimesPlayed` (int) + `LastPlayed` (DateTime?) to the list DTO record. |
| `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Queries/GetUserLibraryQueryHandler.cs` | **Modify** | Populate the 2 new fields from `entry.Stats` in both DTO-construction branches. |
| `apps/web/src/lib/api/schemas/library.schemas.ts` | **Modify** | Add `timesPlayed` + `lastPlayed` to `UserLibraryEntrySchema` (FE zod). |
| `tests/Api.Tests/.../GetUserLibraryQueryHandler*Tests.cs` | **Modify/Create** | Integration test asserting the list returns `TimesPlayed`/`LastPlayed`. |
| `apps/web/src/lib/library/games-tab-filters.ts` | **Create** | Pure filter/sort/search functions over `UserLibraryEntry[]` for the games tab. No React, no IO — unit-testable in isolation. |
| `apps/web/src/lib/library/__tests__/games-tab-filters.test.ts` | **Create** | Unit tests for the pure functions above. |
| `apps/web/src/locales/it.json` | **Modify** | Add `pages.library.gamesTab.*` keys (Italian). |
| `apps/web/src/locales/en.json` | **Modify** | Add `pages.library.gamesTab.*` keys (English). |
| `apps/web/src/app/(authenticated)/library/_components/LibraryHub.tsx` | **Modify** | Add `useLibrary` call, games-tab state, label memos, and the `tab === 'games'` render branch. |
| `apps/web/src/app/(authenticated)/library/_components/__tests__/LibraryHub.test.tsx` | **Modify** | Add 8 tests for the games-tab branch; add a `useLibrary` mock override helper. |
| `apps/web/e2e/smoke-real-backend/games.smoke.spec.ts` | **Modify** | Extend post-redirect assertion to switch to `?tab=games` and verify `GamesResultsGrid`. |
| `apps/web/e2e/a11y/games-library.spec.ts` | **Modify** | Unskip + retarget URL to `/library?tab=games`. |
| `docs/for-developers/frontend/v2-migration-matrix.md` | **Modify** | Move 3 Games* rows `shelf-ready` → `done`. |

---

## Task 1A: Backend — enrich UserLibraryEntryDto with TimesPlayed/LastPlayed

**Decided 2026-05-27 (user):** real data, not proxies. Cheap because `entry.Stats.TimesPlayed`/`LastPlayed` are already materialized in the list handler (columns on `UserLibraryEntryEntity`, read by `MapToDomain` at `UserLibraryRepository.cs:397-403`; the paginated query uses `MapToDomain` at `:127`). No EF query change, no migration.

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/UserLibrary/Application/DTOs/UserLibraryEntryDto.cs`
- Modify: `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Queries/GetUserLibraryQueryHandler.cs`
- Modify: `apps/web/src/lib/api/schemas/library.schemas.ts`
- Test: existing `GetUserLibraryQueryHandler` test class under `tests/Api.Tests/` (find with `grep -rl "GetUserLibraryQueryHandler" tests/`)

- [ ] **Step 1: Add fields to the DTO record.** In `UserLibraryEntryDto.cs`, add two optional params at the end of the record (after `HasRagAccess`):

```csharp
    DateTime? OwnershipDeclaredAt = null, // (existing)
    bool HasRagAccess = false,            // (existing — add trailing comma)
    int TimesPlayed = 0,                  // #1566: gameplay count for games-tab 'played' filter
    DateTime? LastPlayed = null           // #1566: last play timestamp for games-tab 'last-played' sort
```

- [ ] **Step 2: Write a failing integration test.** Find the handler's test class (`grep -rl "GetUserLibraryQueryHandler" tests/`). Add a test that seeds a library entry with a recorded session (TimesPlayed > 0, LastPlayed set) and asserts the returned `UserLibraryEntryDto.TimesPlayed`/`.LastPlayed` match. Run it; expect FAIL (fields not populated / default 0/null). Use the existing seeding helpers in that test class — match their fixture pattern.

- [ ] **Step 3: Populate in the handler.** In `GetUserLibraryQueryHandler.cs`, add to BOTH `new UserLibraryEntryDto(...)` constructions (shared-game branch ~line 155, private-game branch ~line 194):

```csharp
                    HasRagAccess: /* existing expression */,
                    TimesPlayed: entry.Stats.TimesPlayed,
                    LastPlayed: entry.Stats.LastPlayed
```

- [ ] **Step 4: Run the integration test → PASS.** Run the specific test (the project uses xUnit + Testcontainers; run via `dotnet test --filter "FullyQualifiedName~GetUserLibraryQueryHandler"` from `apps/api/src/Api` or the test project dir). Expected: PASS.

- [ ] **Step 5: Add the fields to the FE zod schema.** In `apps/web/src/lib/api/schemas/library.schemas.ts`, inside `UserLibraryEntrySchema` (after `averageRating`), add:

```typescript
  timesPlayed: z.number().int().nonnegative().default(0),
  lastPlayed: z.string().datetime({ offset: true }).nullable().optional(),
```

- [ ] **Step 6: Verify FE typecheck + backend build.** Run: `cd apps/web && pnpm typecheck` (expect clean) and `cd apps/api/src/Api && dotnet build` (expect success).

- [ ] **Step 7: Commit.**

```bash
git add "apps/api/src/Api/BoundedContexts/UserLibrary/Application/DTOs/UserLibraryEntryDto.cs" "apps/api/src/Api/BoundedContexts/UserLibrary/Application/Queries/GetUserLibraryQueryHandler.cs" apps/web/src/lib/api/schemas/library.schemas.ts tests/
git commit -m "feat(library): #1566 expose TimesPlayed/LastPlayed on library list DTO

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 1B: Pure filter/sort/search logic

> **Correction note:** A first pass of this task was committed (`e49185670`) using `GameDetailDto` because `UserLibraryEntry` lacked `timesPlayed`/`lastPlayed`. After Task 1A those fields exist on `UserLibraryEntry`. This task **rewrites `games-tab-filters.ts` to use `UserLibraryEntry`** (the type `useLibrary()` returns and `GamesResultsGrid` consumes), replacing the `GameDetailDto` import. Update the test factory cast to `UserLibraryEntry` and add the new fields.

**Files:**
- Modify (rewrite type): `apps/web/src/lib/library/games-tab-filters.ts`
- Modify: `apps/web/src/lib/library/__tests__/games-tab-filters.test.ts`

- [ ] **Step 1: Verify the GamesStatusKey / GamesSortKey source**

Run: `grep -nE "GamesStatusKey|GamesSortKey" apps/web/src/lib/games/library-filters.ts`
Expected: both types are exported there (they are re-exported by `GamesFiltersInline.tsx:30`). If the file path differs, adjust the import in Step 3 / Step 5 accordingly.

- [ ] **Step 2: Write the failing test for `filterGamesByStatus`**

Create `apps/web/src/lib/library/__tests__/games-tab-filters.test.ts`:

```typescript
import { describe, expect, it } from 'vitest';

import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

import {
  deriveGamesTabEntries,
  filterGamesByQuery,
  filterGamesByStatus,
  sortGames,
} from '../games-tab-filters';

function entry(overrides: Partial<UserLibraryEntry>): UserLibraryEntry {
  return {
    id: overrides.id ?? 'e1',
    userId: 'u1',
    gameId: overrides.gameId ?? 'g1',
    gameTitle: overrides.gameTitle ?? 'Catan',
    gamePublisher: overrides.gamePublisher ?? 'Kosmos',
    gameYearPublished: overrides.gameYearPublished ?? 1995,
    gameDescription: '',
    gameIconUrl: '',
    gameImageUrl: '',
    minPlayers: 2,
    maxPlayers: 4,
    playTimeMinutes: 60,
    complexityRating: null,
    averageRating: overrides.averageRating ?? null,
    addedAt: overrides.addedAt ?? '2026-01-01T00:00:00Z',
    notes: null,
    isFavorite: false,
    currentState: overrides.currentState ?? 'Owned',
    stateChangedAt: null,
    stateNotes: null,
    isAvailableForPlay: true,
    timesPlayed: overrides.timesPlayed ?? 0,
    lastPlayed: overrides.lastPlayed ?? null,
    winRate: 'N/A',
    avgDuration: 'N/A',
    recentSessions: [],
    checklist: [],
  } as UserLibraryEntry;
}

describe('filterGamesByStatus', () => {
  const entries = [
    entry({ id: 'a', currentState: 'Owned', timesPlayed: 3 }),
    entry({ id: 'b', currentState: 'Wishlist', timesPlayed: 0 }),
    entry({ id: 'c', currentState: 'Nuovo', timesPlayed: 0 }),
    entry({ id: 'd', currentState: 'InPrestito', timesPlayed: 1 }),
  ];

  it('all → returns every entry', () => {
    expect(filterGamesByStatus(entries, 'all').map(e => e.id)).toEqual(['a', 'b', 'c', 'd']);
  });

  it('owned → excludes Wishlist', () => {
    expect(filterGamesByStatus(entries, 'owned').map(e => e.id)).toEqual(['a', 'c', 'd']);
  });

  it('wishlist → only Wishlist', () => {
    expect(filterGamesByStatus(entries, 'wishlist').map(e => e.id)).toEqual(['b']);
  });

  it('played → only timesPlayed > 0', () => {
    expect(filterGamesByStatus(entries, 'played').map(e => e.id)).toEqual(['a', 'd']);
  });
});
```

- [ ] **Step 3: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/lib/library/__tests__/games-tab-filters.test.ts`
Expected: FAIL — `Failed to resolve import "../games-tab-filters"`.

- [ ] **Step 4: Create the module with `filterGamesByStatus`**

Create `apps/web/src/lib/library/games-tab-filters.ts`:

```typescript
/**
 * Pure filter/sort/search helpers for the LibraryHub `games` tab (#1566).
 *
 * No React, no IO — deterministic transforms over `UserLibraryEntry[]`.
 * Maps the `sp4-games-index` mockup STATUS_OPTS / SORT_OPTS to entry fields.
 * See docs/superpowers/specs/2026-05-27-1566-library-games-tab-wireup-design.md §3.4.
 */

import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';
import type { GamesSortKey, GamesStatusKey } from '@/lib/games/library-filters';

export function filterGamesByStatus(
  entries: readonly UserLibraryEntry[],
  status: GamesStatusKey
): readonly UserLibraryEntry[] {
  switch (status) {
    case 'owned':
      return entries.filter(e => e.currentState !== 'Wishlist');
    case 'wishlist':
      return entries.filter(e => e.currentState === 'Wishlist');
    case 'played':
      return entries.filter(e => e.timesPlayed > 0);
    case 'all':
    default:
      return entries;
  }
}
```

- [ ] **Step 5: Run test to verify `filterGamesByStatus` passes**

Run: `cd apps/web && pnpm vitest run src/lib/library/__tests__/games-tab-filters.test.ts`
Expected: PASS (4 tests). The `filterGamesByQuery` / `sortGames` / `deriveGamesTabEntries` imports resolve to `undefined` but are not yet exercised, so no failure.

- [ ] **Step 6: Append failing tests for query + sort + derive**

Append to `apps/web/src/lib/library/__tests__/games-tab-filters.test.ts`:

```typescript
describe('filterGamesByQuery', () => {
  const entries = [
    entry({ id: 'a', gameTitle: 'Catan' }),
    entry({ id: 'b', gameTitle: 'Wingspan' }),
    entry({ id: 'c', gameTitle: 'Brass: Birmingham' }),
  ];

  it('empty query → unchanged', () => {
    expect(filterGamesByQuery(entries, '   ').map(e => e.id)).toEqual(['a', 'b', 'c']);
  });

  it('case-insensitive substring match on title', () => {
    expect(filterGamesByQuery(entries, 'wing').map(e => e.id)).toEqual(['b']);
  });

  it('no match → empty', () => {
    expect(filterGamesByQuery(entries, 'xyznotfound')).toEqual([]);
  });
});

describe('sortGames', () => {
  it('title → A-Z collated', () => {
    const e = [
      entry({ id: 'w', gameTitle: 'Wingspan' }),
      entry({ id: 'b', gameTitle: 'brass' }),
      entry({ id: 'c', gameTitle: 'Catan' }),
    ];
    expect(sortGames(e, 'title').map(x => x.id)).toEqual(['b', 'c', 'w']);
  });

  it('rating → desc, nulls last', () => {
    const e = [
      entry({ id: 'lo', averageRating: 6 }),
      entry({ id: 'na', averageRating: null }),
      entry({ id: 'hi', averageRating: 9 }),
    ];
    expect(sortGames(e, 'rating').map(x => x.id)).toEqual(['hi', 'lo', 'na']);
  });

  it('year → desc, zeros last', () => {
    const e = [
      entry({ id: 'old', gameYearPublished: 1995 }),
      entry({ id: 'unk', gameYearPublished: 0 }),
      entry({ id: 'new', gameYearPublished: 2020 }),
    ];
    expect(sortGames(e, 'year').map(x => x.id)).toEqual(['new', 'old', 'unk']);
  });

  it('last-played → most recent first, nulls last', () => {
    const e = [
      entry({ id: 'mid', lastPlayed: '2026-03-01T00:00:00Z' }),
      entry({ id: 'never', lastPlayed: null }),
      entry({ id: 'recent', lastPlayed: '2026-05-01T00:00:00Z' }),
    ];
    expect(sortGames(e, 'last-played').map(x => x.id)).toEqual(['recent', 'mid', 'never']);
  });

  it('does not mutate input', () => {
    const e = [entry({ id: 'a', gameTitle: 'B' }), entry({ id: 'b', gameTitle: 'A' })];
    const original = e.map(x => x.id);
    sortGames(e, 'title');
    expect(e.map(x => x.id)).toEqual(original);
  });
});

describe('deriveGamesTabEntries', () => {
  it('composes status → query → sort', () => {
    const e = [
      entry({ id: 'a', gameTitle: 'Catan', currentState: 'Owned', timesPlayed: 5, averageRating: 7 }),
      entry({ id: 'b', gameTitle: 'Wingspan', currentState: 'Wishlist', timesPlayed: 0 }),
      entry({ id: 'c', gameTitle: 'Catan Junior', currentState: 'Owned', timesPlayed: 2, averageRating: 9 }),
    ];
    // status=owned removes b; query='catan' keeps a + c; sort=rating → c (9) before a (7)
    expect(deriveGamesTabEntries(e, 'owned', 'catan', 'rating').map(x => x.id)).toEqual(['c', 'a']);
  });
});
```

- [ ] **Step 7: Run test to verify the new tests fail**

Run: `cd apps/web && pnpm vitest run src/lib/library/__tests__/games-tab-filters.test.ts`
Expected: FAIL — `filterGamesByQuery is not a function` (and sort/derive).

- [ ] **Step 8: Implement query + sort + derive**

Append to `apps/web/src/lib/library/games-tab-filters.ts`:

```typescript
export function filterGamesByQuery(
  entries: readonly UserLibraryEntry[],
  query: string
): readonly UserLibraryEntry[] {
  const q = query.trim().toLowerCase();
  if (q === '') return entries;
  return entries.filter(e => e.gameTitle.toLowerCase().includes(q));
}

const titleCollator = new Intl.Collator(undefined, { sensitivity: 'base' });

export function sortGames(
  entries: readonly UserLibraryEntry[],
  sort: GamesSortKey
): readonly UserLibraryEntry[] {
  const copy = [...entries];
  switch (sort) {
    case 'rating':
      return copy.sort((a, b) => (b.averageRating ?? -1) - (a.averageRating ?? -1));
    case 'title':
      return copy.sort((a, b) => titleCollator.compare(a.gameTitle, b.gameTitle));
    case 'year':
      return copy.sort((a, b) => (b.gameYearPublished || 0) - (a.gameYearPublished || 0));
    case 'last-played':
    default:
      return copy.sort((a, b) => {
        const ta = a.lastPlayed ? Date.parse(a.lastPlayed) : 0;
        const tb = b.lastPlayed ? Date.parse(b.lastPlayed) : 0;
        return tb - ta;
      });
  }
}

export function deriveGamesTabEntries(
  entries: readonly UserLibraryEntry[],
  status: GamesStatusKey,
  query: string,
  sort: GamesSortKey
): readonly UserLibraryEntry[] {
  return sortGames(filterGamesByQuery(filterGamesByStatus(entries, status), query), sort);
}
```

- [ ] **Step 9: Run all tests + typecheck**

Run: `cd apps/web && pnpm vitest run src/lib/library/__tests__/games-tab-filters.test.ts && pnpm typecheck`
Expected: PASS (all tests) + typecheck clean.

- [ ] **Step 10: Commit**

```bash
git add apps/web/src/lib/library/games-tab-filters.ts apps/web/src/lib/library/__tests__/games-tab-filters.test.ts
git commit -m "feat(library): #1566 pure filter/sort/search for games tab

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 2: i18n keys

**Files:**
- Modify: `apps/web/src/locales/it.json`
- Modify: `apps/web/src/locales/en.json`

- [ ] **Step 1: Locate the existing `pages.library` block**

Run: `grep -n '"pages.library"\|"gamesTab"\|"hubTabs"' apps/web/src/locales/it.json | head`
Expected: find the `pages.library` object (the file uses flat or nested keys — match the existing convention you see). Inspect 10 lines around the match to confirm nested-object vs dotted-key style before editing.

- [ ] **Step 2: Add `gamesTab` keys to `it.json`**

Inside the `pages.library` object in `apps/web/src/locales/it.json`, add a `gamesTab` block (adapt nesting to the file's actual structure):

```jsonc
"gamesTab": {
  "filters": {
    "search": {
      "placeholder": "Cerca per titolo…",
      "ariaLabel": "Cerca giochi nella tua libreria",
      "clearAriaLabel": "Pulisci ricerca"
    },
    "status": {
      "label": "Stato",
      "options": { "all": "Tutti", "owned": "Posseduti", "wishlist": "Wishlist", "played": "Giocati" }
    },
    "sort": {
      "label": "Ordina",
      "options": { "last-played": "Ultima partita", "rating": "Rating", "title": "Titolo A-Z", "year": "Anno" }
    },
    "view": {
      "label": "Vista",
      "options": { "grid": "Griglia", "list": "Lista" }
    },
    "resultCount": "{count, plural, one {# gioco} other {# giochi}}"
  },
  "emptyState": {
    "empty": { "title": "Aggiungi il tuo primo gioco", "subtitle": "Costruisci la tua libreria per iniziare.", "cta": "Aggiungi gioco" },
    "filteredEmpty": { "title": "Nessun risultato", "subtitle": "Prova ad allargare i filtri o azzerarli.", "cta": "Azzera filtri" },
    "error": { "title": "Errore di caricamento", "subtitle": "Impossibile recuperare la libreria.", "cta": "Riprova" }
  }
}
```

- [ ] **Step 3: Add the same keys to `en.json`**

Inside `pages.library` in `apps/web/src/locales/en.json`:

```jsonc
"gamesTab": {
  "filters": {
    "search": {
      "placeholder": "Search by title…",
      "ariaLabel": "Search games in your library",
      "clearAriaLabel": "Clear search"
    },
    "status": {
      "label": "Status",
      "options": { "all": "All", "owned": "Owned", "wishlist": "Wishlist", "played": "Played" }
    },
    "sort": {
      "label": "Sort",
      "options": { "last-played": "Last played", "rating": "Rating", "title": "Title A-Z", "year": "Year" }
    },
    "view": {
      "label": "View",
      "options": { "grid": "Grid", "list": "List" }
    },
    "resultCount": "{count, plural, one {# game} other {# games}}"
  },
  "emptyState": {
    "empty": { "title": "Add your first game", "subtitle": "Build your library to get started.", "cta": "Add game" },
    "filteredEmpty": { "title": "No results", "subtitle": "Try widening or clearing the filters.", "cta": "Clear filters" },
    "error": { "title": "Failed to load", "subtitle": "Could not fetch your library.", "cta": "Retry" }
  }
}
```

- [ ] **Step 4: Validate JSON + i18n parity**

Run: `cd apps/web && node -e "JSON.parse(require('fs').readFileSync('src/locales/it.json','utf8')); JSON.parse(require('fs').readFileSync('src/locales/en.json','utf8')); console.log('JSON OK')"`
Expected: `JSON OK`. If the repo has an i18n-parity test (check `grep -rl "locale" src/__tests__ | head`), run it: `pnpm vitest run -t "locale"`.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/locales/it.json apps/web/src/locales/en.json
git commit -m "feat(i18n): #1566 add pages.library.gamesTab keys

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 3: LibraryHub games-tab state + data + label memos

**Files:**
- Modify: `apps/web/src/app/(authenticated)/library/_components/LibraryHub.tsx`

This task adds the plumbing (no render branch yet — Task 4/5 add the JSX). The component still compiles and existing tests still pass because the new state is unused until Task 4.

- [ ] **Step 1: Add imports**

In `LibraryHub.tsx`, add to the `@/components/features/games` import area (create the import if absent):

```typescript
import {
  GamesEmptyState,
  GamesFiltersInline,
  GamesResultsGrid,
  type GamesEmptyKind,
  type GamesEmptyStateLabels,
  type GamesFiltersInlineLabels,
  type GamesResultsView,
  type GamesSortKey,
  type GamesStatusKey,
  type GamesViewKey,
} from '@/components/features/games';
import { useLibrary } from '@/hooks/queries/useLibrary';
import { deriveGamesTabEntries } from '@/lib/library/games-tab-filters';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';
```

> Note: `useLibrary` may already be imported (the file imports `useLibraryActivity` and `useRemoveGameFromLibrary` from the same module). Merge into the existing import statement to avoid a duplicate-import lint error.

- [ ] **Step 2: Add games-tab state (after the existing `useState` block, ~line 82)**

```typescript
  // ─── games-tab local state (#1566) ───
  const [gamesStatus, setGamesStatus] = useState<GamesStatusKey>('all');
  const [gamesSort, setGamesSort] = useState<GamesSortKey>('last-played');
  const [gamesQuery, setGamesQuery] = useState('');
  const [gamesView, setGamesView] = useState<GamesViewKey>('grid');
```

- [ ] **Step 3: Add the dedicated library query + derivation (after `const hub = useHybridHubItems();`)**

```typescript
  // #1566: dedicated library fetch for the games tab. Identical key to the call
  // inside useHybridHubItems (useHybridHubItems.ts:41-46) → TanStack dedups to a
  // single network request; this is 1 fetch, 2 references (not a double fetch).
  const libraryQuery = useLibrary({
    page: 1,
    pageSize: 50,
    sortBy: 'addedAt',
    sortDescending: true,
  });
  const gameEntries = useMemo<readonly UserLibraryEntry[]>(
    () => libraryQuery.data?.items ?? [],
    [libraryQuery.data]
  );
  const gamesFiltered = useMemo<readonly UserLibraryEntry[]>(
    () => deriveGamesTabEntries(gameEntries, gamesStatus, gamesQuery, gamesSort),
    [gameEntries, gamesStatus, gamesQuery, gamesSort]
  );
  const gamesKind = useMemo<GamesEmptyKind | 'default'>(() => {
    if (libraryQuery.isLoading) return 'loading';
    if (libraryQuery.isError) return 'error';
    if (gameEntries.length === 0) return 'empty';
    if (gamesFiltered.length === 0) return 'filtered-empty';
    return 'default';
  }, [libraryQuery.isLoading, libraryQuery.isError, gameEntries.length, gamesFiltered.length]);
  // Honor the dev/visual-test `?state=` hatch for the games tab too.
  const gamesEffectiveKind: GamesEmptyKind | 'default' =
    (stateOverride as GamesEmptyKind | null) ?? gamesKind;
```

- [ ] **Step 4: Add games-tab label memos (after the existing label memos block)**

```typescript
  const gamesFiltersLabels = useMemo<GamesFiltersInlineLabels>(
    () => ({
      search: {
        placeholder: t('pages.library.gamesTab.filters.search.placeholder'),
        ariaLabel: t('pages.library.gamesTab.filters.search.ariaLabel'),
        clearAriaLabel: t('pages.library.gamesTab.filters.search.clearAriaLabel'),
      },
      status: {
        label: t('pages.library.gamesTab.filters.status.label'),
        options: {
          all: t('pages.library.gamesTab.filters.status.options.all'),
          owned: t('pages.library.gamesTab.filters.status.options.owned'),
          wishlist: t('pages.library.gamesTab.filters.status.options.wishlist'),
          played: t('pages.library.gamesTab.filters.status.options.played'),
        },
      },
      sort: {
        label: t('pages.library.gamesTab.filters.sort.label'),
        options: {
          'last-played': t('pages.library.gamesTab.filters.sort.options.last-played'),
          rating: t('pages.library.gamesTab.filters.sort.options.rating'),
          title: t('pages.library.gamesTab.filters.sort.options.title'),
          year: t('pages.library.gamesTab.filters.sort.options.year'),
        },
      },
      view: {
        label: t('pages.library.gamesTab.filters.view.label'),
        options: {
          grid: t('pages.library.gamesTab.filters.view.options.grid'),
          list: t('pages.library.gamesTab.filters.view.options.list'),
        },
      },
      resultCount: (count: number) =>
        t('pages.library.gamesTab.filters.resultCount', { count }),
    }),
    [t]
  );

  const gamesEmptyLabels = useMemo<GamesEmptyStateLabels>(
    () => ({
      empty: {
        title: t('pages.library.gamesTab.emptyState.empty.title'),
        subtitle: t('pages.library.gamesTab.emptyState.empty.subtitle'),
        cta: t('pages.library.gamesTab.emptyState.empty.cta'),
      },
      filteredEmpty: {
        title: t('pages.library.gamesTab.emptyState.filteredEmpty.title'),
        subtitle: t('pages.library.gamesTab.emptyState.filteredEmpty.subtitle'),
        cta: t('pages.library.gamesTab.emptyState.filteredEmpty.cta'),
      },
      error: {
        title: t('pages.library.gamesTab.emptyState.error.title'),
        subtitle: t('pages.library.gamesTab.emptyState.error.subtitle'),
        cta: t('pages.library.gamesTab.emptyState.error.cta'),
      },
    }),
    [t]
  );

  const handleGamesClearFilters = useCallback(() => {
    setGamesQuery('');
    setGamesStatus('all');
    if (stateOverride != null) router.push(pathname);
  }, [stateOverride, router, pathname]);

  const handleGameCardSelect = useCallback(
    (gameId: string) => router.push(`/games/${gameId}`),
    [router]
  );
```

- [ ] **Step 5: Run typecheck (no render branch yet — verifies plumbing compiles)**

Run: `cd apps/web && pnpm typecheck`
Expected: clean. ESLint may warn about unused `gamesFiltered`/`gamesEffectiveKind`/`gamesFiltersLabels`/`gamesEmptyLabels`/`handleGamesClearFilters`/`handleGameCardSelect`/`gamesView`/`setGamesView` — that is expected until Task 4 wires them. If lint is `--max-warnings=0`, append `// eslint-disable-next-line @typescript-eslint/no-unused-vars` is NOT acceptable; instead proceed directly to Task 4 in the same commit (skip the standalone commit here).

- [ ] **Step 6: Commit (only if lint passes standalone; otherwise fold into Task 4)**

```bash
git add apps/web/src/app/(authenticated)/library/_components/LibraryHub.tsx
git commit -m "feat(library): #1566 games-tab state + dedicated useLibrary derivation

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 4: Render branch — GamesFiltersInline + GamesResultsGrid + GamesEmptyState

**Files:**
- Modify: `apps/web/src/app/(authenticated)/library/_components/LibraryHub.tsx`
- Test: `apps/web/src/app/(authenticated)/library/_components/__tests__/LibraryHub.test.tsx`

- [ ] **Step 1: Add a `useLibrary` mock override helper to the test file**

In `LibraryHub.test.tsx`, replace the static `useLibrary` mock (currently `useLibrary: () => ({ data: undefined, ... })`, ~line 101) with a controllable mock:

```typescript
type MockLibraryReturn = {
  data: { items: UserLibraryEntry[] } | undefined;
  isLoading: boolean;
  isError: boolean;
  error: Error | null;
};
const libraryMock = vi.fn<[], MockLibraryReturn>(() => ({
  data: undefined,
  isLoading: false,
  isError: false,
  error: null,
}));
```

Update the `vi.mock('@/hooks/queries/useLibrary', ...)` factory so `useLibrary: () => libraryMock()`. Add the import `import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';` at the top. Add a `libraryMock.mockReset()` (re-seeding the default) inside the existing `beforeEach`.

> Why: existing tests feed games via `useHybridHubItems`. The new games-tab branch reads `useLibrary` directly. Keeping the default `data: undefined` means existing tests see an empty games tab (kind='empty') — which is fine because they assert on `LibraryHybridGrid` in non-games tabs or on the hub-level FSM, not on the games-tab body. Verify no existing test switches to the games tab AND asserts on `LibraryHybridGrid` content; if one does, give it a `libraryMock` seed.

- [ ] **Step 2: Write the failing test — games tab renders GamesFiltersInline + grid**

Add to `LibraryHub.test.tsx` (inside the top-level `describe`):

```typescript
function seedGamesLibrary(entries: UserLibraryEntry[]): void {
  libraryMock.mockReturnValue({
    data: { items: entries },
    isLoading: false,
    isError: false,
    error: null,
  });
}

function libEntry(id: string, title: string, extra: Partial<UserLibraryEntry> = {}): UserLibraryEntry {
  return {
    id, userId: 'u1', gameId: `game-${id}`, gameTitle: title, gamePublisher: 'Pub',
    gameYearPublished: 2000, gameDescription: '', gameIconUrl: '', gameImageUrl: '',
    minPlayers: 2, maxPlayers: 4, playTimeMinutes: 60, complexityRating: null,
    averageRating: null, addedAt: '2026-01-01T00:00:00Z', notes: null, isFavorite: false,
    currentState: 'Owned', stateChangedAt: null, stateNotes: null, isAvailableForPlay: true,
    timesPlayed: 0, lastPlayed: null, winRate: 'N/A', avgDuration: 'N/A',
    recentSessions: [], checklist: [], ...extra,
  } as UserLibraryEntry;
}

describe('LibraryHub — games tab (#1566)', () => {
  it('renders GamesFiltersInline + GamesResultsGrid when tab=games with entries', async () => {
    // hub mock must report a non-zero games count so the tab is reachable;
    // the games-tab body itself reads useLibrary, seeded below.
    hubMock.mockReturnValue(makeHub({ totalCounts: { games: 1, agents: 0, kb: 0, sessions: 0, chat: 0 } }));
    seedGamesLibrary([libEntry('a', 'Catan')]);
    renderHub();
    await userEvent.click(screen.getByRole('tab', { name: /giochi|games/i }));
    expect(screen.getByTestId
      ? screen.queryByTestId('games-filters-inline')
      : null).toBeDefined(); // primary assertion below uses data-slot
    expect(document.querySelector('[data-slot="games-results-grid"]')).not.toBeNull();
    expect(document.querySelector('[data-slot="games-results-grid-link"]')).not.toBeNull();
  });
});
```

> Note: `makeHub(...)` and `renderHub()` are the existing test helpers in this file — reuse them (do not redefine). Inspect their exact names near the top of the file (`grep -n "const makeHub\|function renderHub\|render(" LibraryHub.test.tsx`) and adapt the calls. The tab label regex `/giochi|games/i` matches the IntlProvider-resolved label; adjust if the seeded messages differ.

- [ ] **Step 3: Run the test to verify it fails**

Run: `cd apps/web && pnpm vitest run "src/app/(authenticated)/library/_components/__tests__/LibraryHub.test.tsx" -t "games tab"`
Expected: FAIL — `[data-slot="games-results-grid"]` is null (the games branch is not yet rendered).

- [ ] **Step 4: Implement the games-tab render branch**

In the JSX of `LibraryHub.tsx`, replace the filter+toolbar+grid region (the block currently containing `CrossEntityFilters`, the `library-toolbar` div, and the `effectiveKind === 'default' ? <LibraryHybridGrid/> : <EmptyLibrary/>` ternary) with a tab-aware branch:

```tsx
          {tab === 'games' ? (
            <>
              <GamesFiltersInline
                labels={gamesFiltersLabels}
                query={gamesQuery}
                onQueryChange={setGamesQuery}
                status={gamesStatus}
                onStatusChange={setGamesStatus}
                sort={gamesSort}
                onSortChange={setGamesSort}
                view={gamesView}
                onViewChange={setGamesView}
                resultCount={gamesFiltered.length}
              />
              {gamesEffectiveKind === 'default' ? (
                <GamesResultsGrid
                  entries={gamesFiltered}
                  view={gamesView as GamesResultsView}
                />
              ) : (
                <GamesEmptyState
                  kind={gamesEffectiveKind}
                  labels={gamesEmptyLabels}
                  onAddGame={handleAddGame}
                  onClearFilters={handleGamesClearFilters}
                  onRetry={handleRetry}
                />
              )}
            </>
          ) : (
            <>
              <CrossEntityFilters
                tab={tab}
                gameStateFilter={gameStateFilter}
                onGameStateFilterChange={setGameStateFilter}
              />
              <div
                data-slot="library-toolbar"
                className="flex flex-wrap items-center gap-3 rounded-2xl border border-border bg-card p-3 shadow-sm"
              >
                {/* ... EXISTING toolbar contents unchanged ... */}
              </div>
              {effectiveKind === 'default' ? (
                <LibraryHybridGrid
                  items={merged}
                  view={view as LibraryViewMode}
                  selectionMode={selectionMode}
                  selected={selected}
                  onCardClick={handleCardClick}
                  onLongPressEnter={handleEnterSelectMode}
                />
              ) : (
                <EmptyLibrary
                  kind={effectiveKind}
                  labels={emptyLabels}
                  onAddGame={handleAddGame}
                  onClearFilters={handleClearFilters}
                  onRetry={handleRetry}
                />
              )}
            </>
          )}
```

> Preserve the existing toolbar inner JSX verbatim (search input, sort select, view toggle, enter-select-mode button). Only the wrapping branch is new. The `tab === 'games' && selectionMode === 'browse'` enter-select button now lives in the `else` branch's toolbar, so it never shows in the games tab — this is the intended "bulk hidden in games tab" behavior from spec §3.5.

- [ ] **Step 5: Run the test to verify it passes**

Run: `cd apps/web && pnpm vitest run "src/app/(authenticated)/library/_components/__tests__/LibraryHub.test.tsx" -t "games tab"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add "apps/web/src/app/(authenticated)/library/_components/LibraryHub.tsx" "apps/web/src/app/(authenticated)/library/_components/__tests__/LibraryHub.test.tsx"
git commit -m "feat(library): #1566 wire Games* components into games tab

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 5: FSM tests (loading / empty / filtered-empty / error) + regression guard

**Files:**
- Test: `apps/web/src/app/(authenticated)/library/_components/__tests__/LibraryHub.test.tsx`

- [ ] **Step 1: Write the FSM + regression tests**

Add inside the `describe('LibraryHub — games tab (#1566)', ...)`:

```typescript
  it('renders GamesEmptyState kind=empty when library has no entries', async () => {
    hubMock.mockReturnValue(makeHub({ totalCounts: { games: 0, agents: 0, kb: 0, sessions: 0, chat: 0 } }));
    seedGamesLibrary([]);
    renderHub();
    await userEvent.click(screen.getByRole('tab', { name: /giochi|games/i }));
    const el = document.querySelector('[data-slot="games-empty-state"]');
    expect(el?.getAttribute('data-kind')).toBe('empty');
  });

  it('renders GamesEmptyState kind=filtered-empty when filter removes all', async () => {
    hubMock.mockReturnValue(makeHub({ totalCounts: { games: 1, agents: 0, kb: 0, sessions: 0, chat: 0 } }));
    seedGamesLibrary([libEntry('a', 'Catan')]);
    renderHub();
    await userEvent.click(screen.getByRole('tab', { name: /giochi|games/i }));
    // search a non-matching query via the GamesFiltersInline search box
    const search = document.querySelector('[data-slot="games-results-grid"]') ? null : null;
    void search;
    await userEvent.type(screen.getByRole('searchbox'), 'xyznotfound');
    await waitFor(() => {
      const el = document.querySelector('[data-slot="games-empty-state"]');
      expect(el?.getAttribute('data-kind')).toBe('filtered-empty');
    });
  });

  it('renders GamesEmptyState kind=error when libraryQuery.isError', async () => {
    hubMock.mockReturnValue(makeHub({ totalCounts: { games: 0, agents: 0, kb: 0, sessions: 0, chat: 0 } }));
    libraryMock.mockReturnValue({ data: undefined, isLoading: false, isError: true, error: new Error('boom') });
    renderHub();
    await userEvent.click(screen.getByRole('tab', { name: /giochi|games/i }));
    const el = document.querySelector('[data-slot="games-empty-state"]');
    expect(el?.getAttribute('data-kind')).toBe('error');
  });

  it('non-games tabs still render LibraryHybridGrid (regression guard for #1618)', async () => {
    hubMock.mockReturnValue(
      makeHub({
        totalCounts: { games: 0, agents: 0, kb: 0, sessions: 1, chat: 0 },
        // seed the sessions source so the hybrid grid has an item — reuse existing helper shape
      })
    );
    renderHub();
    await userEvent.click(screen.getByRole('tab', { name: /sessioni|sessions/i }));
    // games branch slots must be absent on a non-games tab
    expect(document.querySelector('[data-slot="games-results-grid"]')).toBeNull();
    expect(document.querySelector('[data-slot="games-empty-state"]')).toBeNull();
  });
```

> Adapt `makeHub({...})` overrides to the helper's real signature (it likely takes `Partial<UseHybridHubItemsResult>` and a `sources` override). For the regression test, seed `sources.sessions` with one `SessionHubItem` using the file's existing item factory so `LibraryHybridGrid` renders content; the assertion only checks that the games slots are absent, so a precise hybrid-grid selector is not required.

- [ ] **Step 2: Run the games-tab tests**

Run: `cd apps/web && pnpm vitest run "src/app/(authenticated)/library/_components/__tests__/LibraryHub.test.tsx" -t "games tab"`
Expected: PASS (5 tests total in the block).

- [ ] **Step 3: Run the FULL LibraryHub suite (regression check for the 62 existing tests)**

Run: `cd apps/web && pnpm vitest run "src/app/(authenticated)/library/_components/__tests__/LibraryHub.test.tsx"`
Expected: PASS (62 existing + 5 new = 67). If an existing test fails because it switched to the games tab and asserted on the old toolbar/grid, seed its `libraryMock` and update its assertion to the games-branch slots. Document any such change in the commit body.

- [ ] **Step 4: Commit**

```bash
git add "apps/web/src/app/(authenticated)/library/_components/__tests__/LibraryHub.test.tsx"
git commit -m "test(library): #1566 games-tab FSM + non-games regression guard

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 6: E2E smoke + a11y unskip

**Files:**
- Modify: `apps/web/e2e/smoke-real-backend/games.smoke.spec.ts`
- Modify: `apps/web/e2e/a11y/games-library.spec.ts`

> **Rebase note:** This task assumes PR #1619 is merged. If it is NOT, the two files below are still in their pre-#1619 state — first re-apply the #1619 changes (redirect assertion in smoke; `test.describe.skip` in a11y), then apply this task on top. Check with `git log --oneline -5 origin/main-dev | grep 1612`.

- [ ] **Step 1: Extend the smoke test to cover the games tab**

Replace `apps/web/e2e/smoke-real-backend/games.smoke.spec.ts` body with:

```typescript
import { test, expect } from '@playwright/test';

import { smokeLogin, applySessionToPage } from './_helpers/auth';

test.describe('SMOKE — /games redirect + /library?tab=games (real backend)', () => {
  test('GET /games?tab=library redirects to /library and renders LibraryHub', async ({
    page,
    request,
  }) => {
    const { cookieHeader } = await smokeLogin(request);
    await applySessionToPage(page, cookieHeader);
    await page.goto('/games?tab=library', { waitUntil: 'domcontentloaded' });
    expect(new URL(page.url()).pathname).toBe('/library');
    await page.waitForSelector('[data-slot="library-hub-v2"]', { timeout: 30_000 });
    const errorState = await page.locator('[data-state="error"]').count();
    expect(errorState).toBe(0);
  });

  test('/library?tab=games renders the GamesResultsGrid (or empty/loading state)', async ({
    page,
    request,
  }) => {
    const { cookieHeader } = await smokeLogin(request);
    await applySessionToPage(page, cookieHeader);
    await page.goto('/library?tab=games', { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('[data-slot="library-hub-v2"]', { timeout: 30_000 });
    // games tab renders either the grid (entries) or a GamesEmptyState (no entries).
    // The smoke fixture user has 1 library entry, so expect the grid; fall back to
    // empty-state if the fixture changes. Either proves the branch mounted.
    await page.waitForSelector(
      '[data-slot="games-results-grid"], [data-slot="games-empty-state"]',
      { timeout: 30_000 }
    );
    const errorState = await page
      .locator('[data-slot="games-empty-state"][data-kind="error"]')
      .count();
    expect(errorState).toBe(0);
  });
});
```

> The smoke fixture seeds a `UserLibraryEntry` for the smoke user (verified in the #1612 run diagnostics: `library detail HTTP 200`). The first `tab=games` body should therefore be the grid. The dual selector keeps the test robust if the fixture's library count changes.

- [ ] **Step 2: Unskip + retarget the a11y suite**

In `apps/web/e2e/a11y/games-library.spec.ts`:
1. Change `test.describe.skip(...)` back to `test.describe(...)`.
2. Update the header comment: replace the "SKIPPED post-#1567 / #1566 follow-up" block with a one-line note that the suite now targets `/library?tab=games`.
3. Update `gotoLibraryReady` to navigate to the library route and switch to the games tab:

```typescript
async function gotoLibraryReady(page: Page, search = ''): Promise<void> {
  await seedAuthSession(page);
  await seedCookieConsent(page);
  await mockAuthEndpoints(page);
  await page.goto(`/library?tab=games${search ? `&${search.replace(/^\?/, '')}` : ''}`, {
    waitUntil: 'domcontentloaded',
  });
  await page.waitForSelector('[data-slot="games-results-grid"]', { timeout: 30_000 });
}
```

> The `?state=filtered-empty` override (used by the second a11y test) still works because `LibraryHub` honors `stateOverride` for the games kind (Task 3 Step 3). For that test, change its call to `gotoLibraryReady(page, 'state=filtered-empty')` and keep its `[data-slot="games-empty-state"][data-kind="filtered-empty"]` assertion. The reduced-motion test keeps `[data-slot="games-results-grid-link"]`.

- [ ] **Step 3: Typecheck + lint the two specs**

Run: `cd apps/web && pnpm typecheck && pnpm exec eslint e2e/smoke-real-backend/games.smoke.spec.ts e2e/a11y/games-library.spec.ts`
Expected: clean.

- [ ] **Step 4: Commit**

```bash
git add apps/web/e2e/smoke-real-backend/games.smoke.spec.ts apps/web/e2e/a11y/games-library.spec.ts
git commit -m "test(e2e): #1566 cover /library?tab=games smoke + unskip a11y

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 7: Migration matrix + final verification + PR

**Files:**
- Modify: `docs/for-developers/frontend/v2-migration-matrix.md`

- [ ] **Step 1: Update the migration matrix**

Run: `grep -nE "GamesFiltersInline|GamesResultsGrid|GamesEmptyState|GamesHero|GamesRecentRail|shelf-ready" docs/for-developers/frontend/v2-migration-matrix.md`

For the rows `GamesFiltersInline`, `GamesResultsGrid`, `GamesEmptyState`: change `Status` from `shelf-ready` → `done` and set the `PR` column to this PR number (fill after PR creation in Step 5). For `GamesHero` and `GamesRecentRail`: keep `shelf-ready`, append a note `"not wired by #1566 (mockup/scope) — see spec §7"`.

- [ ] **Step 2: Full web test suite + typecheck + lint**

Run: `cd apps/web && pnpm typecheck && pnpm lint && pnpm vitest run src/lib/library src/app/\(authenticated\)/library`
Expected: all green. Record the exact pass counts.

- [ ] **Step 3: Commit the matrix**

```bash
git add docs/for-developers/frontend/v2-migration-matrix.md
git commit -m "docs(matrix): #1566 mark 3 Games* components done

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

- [ ] **Step 4: Push the branch**

```bash
git push -u origin feature/issue-1566-games-tab-wireup
```

- [ ] **Step 5: Open the PR to main-dev**

```bash
gh pr create --base main-dev --head feature/issue-1566-games-tab-wireup \
  --title "feat(library): #1566 wire Games* components into /library games tab" \
  --body "<summary from spec §1-3 + AC checklist from §5 + out-of-scope §7; Closes #1566; Refs #1521 #1567 #1618 #633>"
```

Then update the matrix `PR` column (Step 1) with the returned PR number and amend the matrix commit (or push a follow-up commit).

- [ ] **Step 6: Update issue #1566**

```bash
gh issue comment 1566 --repo meepleAi-app/meepleai-monorepo --body "<PR link + scope summary: 3 of 5 components wired, GamesHero/GamesRecentRail out-of-scope per spec §7>"
```

---

## Self-Review

**1. Spec coverage:**
- §3.2 (3 of 5 wired) → Task 4 ✓
- §3.3 (useLibrary dedup) → Task 3 Step 3 ✓
- §3.4 (filter/sort mapping) → Task 1 ✓
- §3.5 (bulk hidden) → Task 4 Step 4 note ✓
- §3.6 (CrossEntityFilters hidden in games) → Task 4 Step 4 (else-branch) ✓
- §3.7 (i18n) → Task 2 ✓
- §4 (FSM) → Task 3 Step 3 + Task 5 ✓
- §5 AC1-3 → Tasks 3-5; AC4-5 → Task 1; AC6 → Task 3/5; AC7 → Task 2; AC8 → Task 5 Step 3; AC9 → Task 6 Step 1; AC10 → Task 6 Step 2; AC11 → Task 7 Step 1; AC12 (DS-15) → components already compliant (#633), no new hardcoded colors introduced ✓
- §6 (tests) → Tasks 1, 5, 6 ✓

**2. Placeholder scan:** No "TBD"/"implement later". The toolbar inner JSX in Task 4 Step 4 is marked `{/* ... EXISTING ... */}` with an explicit "preserve verbatim" instruction rather than re-pasting 60 lines that already exist in the file — this is a deliberate "do not touch" marker, not a missing implementation.

**3. Type consistency:** `GamesStatusKey`/`GamesSortKey`/`GamesViewKey`/`GamesResultsView`/`GamesEmptyKind`/`GamesEmptyStateLabels`/`GamesFiltersInlineLabels` all come from `@/components/features/games` barrel (verified against `index.ts`). `deriveGamesTabEntries` signature is identical in Task 1 (definition) and Task 3 (call). `gamesEffectiveKind` is `GamesEmptyKind | 'default'`, matched against `GamesResultsGrid` (default) vs `GamesEmptyState` (the 4 kinds) in Task 4.

**Known soft spots to resolve during execution (not blockers):**
- Exact `makeHub` / `renderHub` helper signatures in `LibraryHub.test.tsx` (Task 4/5) — inspect before use.
- `it.json`/`en.json` nesting style (nested-object vs dotted-key) — inspect in Task 2 Step 1.
- Whether `useLibrary` is already imported in `LibraryHub.tsx` — merge import to avoid duplicate (Task 3 Step 1 note).
