# V2 Migration · Wave A.3b — `/shared-games` Frontend Greenfield Spec

**Issue**: #596 (parent: #579 Wave A umbrella)
**Branch**: `feature/issue-596-shared-games-fe-v2` (parent: `main-dev`)
**Mockup**: `admin-mockups/design_files/sp3-shared-games.jsx` (1108 LOC)
**Visual baseline mockup**: `apps/web/e2e/visual-mockups/baseline.spec.ts-snapshots/sp3-shared-games-mockup-baseline-{desktop,mobile}-linux.png` (PR #575 Phase 0)
**Backend prerequisite**: PR #594 squash `91eb3b4de` (Wave A.3a) — MERGED 2026-04-28
**Pilot reference**: `2026-04-27-v2-migration-wave-a-2-join.md` (PR #590 MERGED)
**Companion exec plan**: `2026-04-27-v2-migration-phase1-execution.md` §3.3
**Status**: REVIEWED 2026-04-28 — applied 4 critical + 5 important spec-panel findings

---

## 1. Goals

1. **Creare** route `/shared-games` v2 (greenfield: oggi esiste solo `/shared-games/[id]/page.tsx`, l'index è inesistente) seguendo `sp3-shared-games` mockup 1:1.
2. **Riusare workflow** validato in Wave A.1/A.2: visual-migrated + v2-states + hybrid masking + bootstrap baselines via CI.
3. **Estrarre componente generico riusabile** `useUrlHashState<T>` (hook) destinato a future wave A.4/A.5 che richiedono persistenza filtro via URL hash (riusabile per `/shared-games/[id]` tab state, futuri filtri `/discover`).
4. **Wireup full backend integration** sfruttando i nuovi DTO/filter/sort esposti dal Wave A.3a:
   - 7 DTO fields: `tkCount`, `agCount`, `kbCount`, `newWeek`, `contribCount`, `isTopRated`, `isNew`
   - 4 filter params: `hasToolkit`, `hasAgent`, `isTopRated`, `isNew`
   - 2 sort options: `contrib`, `new` (in aggiunta a `rating`, `title`)
   - 1 endpoint nuovo: `GET /api/v1/shared-games/top-contributors?limit=5`
5. **Allineare backend threshold** `SharedGameCatalog:TopRatedThreshold` 4.5 → 4.0 per coerenza con mockup (`g.rating >= 8` su scala 0-10 = stelle ≥ 4.0/5).

## 2. Non-goals

- Pagination (per Alpha: hardcoded `pageSize=100`, sufficiente per ~50 community games attesi).
- Mobile sidebar drawer "Top contributors" (sidebar resta hidden < md breakpoint).
- Refactor di `/shared-games/[id]/page.tsx` (rimane v1 fino a wave dedicata).
- Top-contributors per-game scope (resta global come da decisione A.3a).
- BGG fetch live in catalog (catalogo è community DB, BGG flow è sotto `/library`).
- Search backend full-text avanzato (Alpha: ILIKE su `Name`, sufficiente per ≤ 100 record).
- Real-time SSE updates al cambio cache (post-Alpha).

## 3. Architecture

### 3.1 File map

| Tipo | Path | Status |
|------|------|--------|
| Server page | `apps/web/src/app/(public)/shared-games/page.tsx` | **Create** (server component, metadata export, SSR initial fetch) |
| Client page | `apps/web/src/app/(public)/shared-games/page-client.tsx` | **Create** (state + filters + grid orchestration) |
| Page test | `apps/web/src/app/(public)/shared-games/__tests__/page.test.tsx` | **Create** |
| Generic hook | `apps/web/src/hooks/useUrlHashState.ts` | **Create** (riusabile A.4/A.5) |
| Hook test | `apps/web/src/hooks/__tests__/useUrlHashState.test.ts` | **Create** |
| Submit/fetch hook | `apps/web/src/hooks/useSharedGames.ts` | **Create** (TanStack Query — search + filter + sort) |
| API client | `apps/web/src/lib/api/shared-games.ts` | **Create** (`searchSharedGames`, `getTopContributors`) |
| Static data | `apps/web/src/lib/shared-games/filters.ts` | **Create** (`FILTER_CHIPS`, `GENRES`, `SORT_OPTIONS` constants) |
| Component | `apps/web/src/components/ui/v2/shared-games/shared-games-hero.tsx` | **Create** (compact mobile / full desktop) |
| Component | `apps/web/src/components/ui/v2/shared-games/shared-games-filters.tsx` | **Create** (sticky search + chip toggle + 2 selects) |
| Component | `apps/web/src/components/ui/v2/shared-games/contributors-sidebar.tsx` | **Create** (desktop ≥ md, top-5 + community stats) |
| Component | `apps/web/src/components/ui/v2/shared-games/meeple-card-game.tsx` | **Create** (variant `grid`, `<a href>` per a11y/SEO) |
| Component | `apps/web/src/components/ui/v2/shared-games/skeleton-card.tsx` | **Create** (shimmer cover + 2 text + 2 chip) |
| Component | `apps/web/src/components/ui/v2/shared-games/empty-state.tsx` | **Create** (`kind: 'empty-search' \| 'filtered-empty'`) |
| Component | `apps/web/src/components/ui/v2/shared-games/error-state.tsx` | **Create** (retry button) |
| Component | `apps/web/src/components/ui/v2/shared-games/shared-games-grid.tsx` | **Create** (dispatcher: loading / error / empty / default) |
| Component index | `apps/web/src/components/ui/v2/shared-games/index.ts` | **Create** |
| i18n IT | `apps/web/src/locales/it.json` § `pages.sharedGames` | **Add** (~35 keys) |
| i18n EN | `apps/web/src/locales/en.json` § `pages.sharedGames` | **Add** (~35 keys) |
| Visual test | `apps/web/e2e/visual-migrated/sp3-shared-games.spec.ts` | **Create** (1 desktop + 1 mobile) |
| State test | `apps/web/e2e/v2-states/shared-games.spec.ts` | **Create** (5 stati × 2 viewport = 10 PNG) |
| Baselines | `apps/web/e2e/visual-migrated/sp3-shared-games.spec.ts-snapshots/*.png` | **Create** (2 PNG via CI bootstrap) |
| Baselines | `apps/web/e2e/v2-states/shared-games.spec.ts-snapshots/*.png` | **Create** (10 PNG via CI bootstrap) |
| **Backend config** | `apps/api/src/Api/appsettings.Development.json` (+ `Production` + `Staging` se override esistono) | **Edit** `SharedGameCatalog:TopRatedThreshold` 4.5 → 4.0 |
| **Backend test** | esistente `SearchSharedGamesQueryHandlerTests.cs` | **Adjust** se test fixture-based assume 4.5 |

### 3.2 Component API

#### `useUrlHashState<T>` (generic hook)

```ts
/**
 * Bidirectional sync between component state and URL hash query string.
 * Hash format: #key1=value1&key2=value2 (URLSearchParams compatible).
 *
 * - Initial render: ALWAYS returns `defaultValue` (SSR-safe, hydration-safe).
 * - Post-mount: `useEffect` reads `window.location.hash` once and updates state
 *   (this triggers a deferred re-render — accepted: hash-driven state is non-LCP UI).
 * - hashchange listener: subscribed on mount, unsubscribed on unmount.
 * - Write: `history.replaceState` (no scroll, no history pollution).
 * - Serializer/deserializer pluggable for non-string types.
 *
 * Reusable for A.4 (`/shared-games/[id]` tab persistence), A.5 (filter UIs).
 */
export function useUrlHashState<T>(
  key: string,
  defaultValue: T,
  options?: {
    serialize?: (value: T) => string | null; // null = remove key from hash
    deserialize?: (raw: string) => T;
  }
): [T, (next: T) => void];
```

**Edge cases (binding)**:
- **SSR-safe / Hydration-safe**: server render and client first render BOTH return `defaultValue`.
  Hash read happens in `useEffect` (post-hydration) → never causes hydration mismatch warnings.
- Multi-instance: multiple components reading same key MUST stay in sync via `hashchange` event subscribe.
- Cleanup: removes listener on unmount.
- Default = no hash entry (avoid `#sort=rating` clutter when sort is default; `serialize` returns `null` if `value === defaultValue`).
- **Debounce coordination** (Risk #5): when caller debounces a value (e.g. search query 300ms),
  the debounced wrapper SHOULD call `setHash(value)` only on trailing edge — NOT on every keystroke.
  Hook itself is not aware of debounce; orchestration responsibility lies in `page-client.tsx`.

#### `SharedGamesFilters`

```ts
export interface SharedGamesFiltersProps {
  readonly query: string;
  readonly onQueryChange: (next: string) => void;
  readonly activeChips: readonly FilterChipId[]; // ('with-toolkit' | 'with-agent' | 'top-rated' | 'new')[]
  readonly onToggleChip: (id: FilterChipId) => void;
  readonly genre: string; // '' = all
  readonly onGenreChange: (next: string) => void;
  readonly sort: SortKey; // 'rating' | 'contrib' | 'new' | 'title'
  readonly onSortChange: (next: SortKey) => void;
  readonly resultCount: number;
  readonly totalCount: number;
  readonly variant: 'desktop' | 'mobile';
  readonly labels: SharedGamesFiltersLabels; // i18n bundle
}
```

**A11y contract (binding)**:
- Search input: `role="searchbox"` (implicit via `type="search"`) + `aria-label` from labels.
- Chip buttons: `aria-pressed={active}` + visible label (icon `aria-hidden`).
- Sticky positioning: `top: 56` desktop (header) / `top: 0` mobile (no header).
- Result counter `<span aria-live="polite">` per annunciare cambi count agli screen reader.
- Keyboard: chip Tab order naturale; Enter/Space toggle. ESC su input clears query.

#### `ContributorsSidebar`

```ts
export interface ContributorsSidebarProps {
  readonly contributors: readonly TopContributor[]; // exactly 5 (backend limit param)
  readonly communityStats: {
    readonly totalGames: number;
    readonly totalToolkits: number;
    readonly totalAgents: number;
  };
  readonly loading: boolean;
  readonly labels: ContributorsSidebarLabels;
}

export interface TopContributor {
  readonly userId: string;
  readonly displayName: string;
  readonly avatarUrl: string | null;
  readonly score: number; // TotalSessions + TotalWins * 2
  readonly totalSessions: number;
  readonly totalWins: number;
}
```

**A11y contract**:
- `<aside aria-labelledby={headingId}>` con heading H2 nascosto a desktop ma presente per screen reader.
- Each contributor row: focusable se cliccabile in futuro (Alpha: NON cliccabile, profili pubblici fuori scope).
- Loading skeleton: `aria-busy="true"`.
- Hidden < md breakpoint via Tailwind `hidden md:block`.

#### `MeepleCardGame` (variant `grid`)

```ts
export interface MeepleCardGameProps {
  readonly game: SharedGameDto;
  readonly compact?: boolean;
  readonly labels: MeepleCardGameLabels; // {sessionsLabel, toolkitsLabel, ...}
}
```

**Implementation deviation from mockup (binding)**:
- Mockup uses `<article tabIndex={0}>` + `cursor:pointer` + presumed click handler external.
- **Production**: replace with `<Link href={`/shared-games/${game.id}`} prefetch>` from `next/link` for:
  - Native keyboard nav (focusable, Enter activates)
  - SEO crawlability (anchor in HTML)
  - Middle-click open-in-new-tab
  - Client-side soft navigation + automatic prefetch on hover (Next.js perf primitive)
- The Next.js `<Link>` renders to an `<a>` element — root must apply `text-decoration: none; color: inherit; display: block` to neutralize default link styling and preserve mockup visual.
- Hover/focus styles preserved via CSS (`:hover` + `:focus-visible`) on the rendered anchor.
- Card content rimane invariato: cover + emoji + newWeek badge + title + meta + EntityChip footer.

#### `useSharedGames` (TanStack Query hook)

```ts
export interface UseSharedGamesArgs {
  readonly query: string;            // already debounced upstream
  readonly chips: readonly FilterChipId[];
  readonly genre: string;            // '' = no filter
  readonly sort: SortKey;
  readonly initialData?: SearchSharedGamesResponse; // SSR seed
}

export interface UseSharedGamesResult {
  readonly data: readonly SharedGameDto[];
  readonly totalCount: number;       // server-reported total (pre-pagination)
  readonly isLoading: boolean;       // first fetch, no initialData consumed
  readonly isFetching: boolean;      // background refetch
  readonly isError: boolean;
  readonly error: Error | null;
  readonly refetch: () => Promise<void>;
}

export function useSharedGames(args: UseSharedGamesArgs): UseSharedGamesResult;
```

**Contract (binding)**:
- Backed by `useQuery({ queryKey: ['shared-games', query, chips, genre, sort], queryFn, initialData, staleTime: 60_000 })`.
- `queryFn` calls `lib/api/shared-games.ts#searchSharedGames({ q: query || undefined, hasToolkit, hasAgent, isTopRated, isNew, genre: genre || undefined, sort, pageSize: 100 })`.
- Filter chip → param mapping uses `FILTER_CHIPS[i].backendParam` (single source of truth).
- Error mapping: network failure / non-2xx → `isError: true` → page renders `<ErrorState onRetry={refetch}/>`.
- Empty result mapping (responsibility of `page-client.tsx`, NOT hook):
  - `query !== '' && data.length === 0` → `kind: 'empty-search'`
  - `chips.length > 0 || genre !== '' && data.length === 0` → `kind: 'filtered-empty'`
  - both empty → ambiguous, defaults to `'empty-search'` (rare: no query + no filter + 0 results means catalog truly empty, acceptable copy).

#### `EmptyState`

```ts
export interface EmptyStateProps {
  readonly kind: 'empty-search' | 'filtered-empty';
  readonly query?: string; // shown in 'empty-search' message
  readonly onResetFilters: () => void;
  readonly labels: EmptyStateLabels;
}
```

#### `ErrorState`

```ts
export interface ErrorStateProps {
  readonly onRetry: () => void;
  readonly labels: ErrorStateLabels;
}
```

### 3.3 Data shape

```ts
// lib/shared-games/filters.ts
export type FilterChipId = 'with-toolkit' | 'with-agent' | 'top-rated' | 'new';
export type SortKey = 'rating' | 'contrib' | 'new' | 'title';

export interface FilterChipDef {
  readonly id: FilterChipId;
  readonly icon: string;
  readonly entityToken: 'toolkit' | 'agent' | 'game' | 'event'; // entityHsl key
  readonly i18nKey: string; // e.g. 'pages.sharedGames.filters.chips.withToolkit'
  readonly backendParam: 'hasToolkit' | 'hasAgent' | 'isTopRated' | 'isNew';
}

export const FILTER_CHIPS: readonly FilterChipDef[] = [
  { id: 'with-toolkit', icon: '🧰', entityToken: 'toolkit', i18nKey: '...', backendParam: 'hasToolkit' },
  { id: 'with-agent',   icon: '🤖', entityToken: 'agent',   i18nKey: '...', backendParam: 'hasAgent'   },
  { id: 'top-rated',    icon: '⭐', entityToken: 'game',    i18nKey: '...', backendParam: 'isTopRated' },
  { id: 'new',          icon: '✨', entityToken: 'event',   i18nKey: '...', backendParam: 'isNew'      },
] as const;

export const GENRES = [
  'Astratto', 'Famiglia', 'Engine', 'Economico', 'Strategico', 'Coop',
  'Avventura', 'Drafting', 'Asimmetrico', 'Politico', 'Worker',
  'Bag-build', 'Investigativo',
] as const;

export const SORT_OPTIONS: readonly { key: SortKey; i18nKey: string; icon: string }[] = [
  { key: 'rating',  icon: '⭐',  i18nKey: '...' },
  { key: 'contrib', icon: '🧰', i18nKey: '...' },
  { key: 'new',     icon: '✨', i18nKey: '...' },
  { key: 'title',   icon: '🔤', i18nKey: '...' },
] as const;
```

### 3.4 i18n keys (~35 × 2)

```jsonc
"pages": {
  "sharedGames": {
    "metadata": { "title": "Catalogo community — MeepleAI", "description": "..." },
    "hero": {
      "badge": "Catalogo community",
      "title": "Giochi condivisi",
      "subtitle": "Esplora toolkit, agenti e knowledge base creati dalla community"
    },
    "filters": {
      "searchPlaceholder": "Cerca per titolo, autore, editore…",
      "searchAriaLabel": "Cerca giochi",
      "genreAriaLabel": "Filtra per genere",
      "genreAll": "Tutti i generi",
      "sortAriaLabel": "Ordina",
      "chips": {
        "withToolkit": "Con toolkit",
        "withAgent": "Con agente",
        "topRated": "Top rated",
        "new": "Nuovi"
      },
      "sortOptions": {
        "rating": "Top rated",
        "contrib": "Più toolkit",
        "new": "Più recenti",
        "title": "A–Z"
      },
      "resultCounter": "{result} di {total} giochi"
    },
    "sidebar": {
      "contributorsTitle": "Top contributors",
      "contributorsSubtitle": "Più sessioni + vittorie",
      "communityStatsTitle": "Statistiche community",
      "stats": {
        "games": "Giochi",
        "toolkits": "Toolkit",
        "agents": "Agenti"
      }
    },
    "card": {
      "ariaLabel": "{title} — gioco condiviso",
      "newBadge": "+{count}",
      "tkLabel": "tk",
      "agLabel": "ag",
      // NOTE: intentional asymmetry — mockup line 270 shows kb count without label
      // (only the count number is rendered next to the kb chip dot). No `kbLabel` key.
      "metaSeparator": " · ",
      "playersUnit": "pl"
    },
    "states": {
      "emptySearch": {
        "icon": "🔍",
        "title": "Nessun risultato per «{query}»",
        "subtitle": "Prova a cercare con altri termini",
        "resetCta": "Pulisci ricerca"
      },
      "filteredEmpty": {
        "icon": "🎯",
        "title": "Nessun gioco corrisponde ai filtri",
        "subtitle": "Prova a rimuovere alcuni filtri",
        "resetCta": "Reset filtri"
      },
      "error": {
        "icon": "🛑",
        "title": "Impossibile caricare il catalogo",
        "subtitle": "Controlla la connessione e riprova",
        "retryCta": "Riprova"
      }
    }
  }
}
```

### 3.5 Server / Client split

```ts
// page.tsx (server)
export const revalidate = 60; // ISR — 1 min, aligns with HybridCache TTL backend
export const metadata: Metadata = { title, description, openGraph };

export default async function SharedGamesPage() {
  let initial: SearchSharedGamesResponse | null = null;
  let initialContributors: readonly TopContributor[] = [];
  try {
    // Parallel SSR fetch — Promise.allSettled tolerates partial failures
    const [gamesResult, contributorsResult] = await Promise.allSettled([
      searchSharedGames({ pageSize: 100 }),
      getTopContributors(5),
    ]);
    if (gamesResult.status === 'fulfilled') initial = gamesResult.value;
    if (contributorsResult.status === 'fulfilled') initialContributors = contributorsResult.value;
  } catch (err) {
    // Defense in depth — in pratica catch è no-op perché allSettled non rilancia.
    // Logging server-side via observability (Pino/Sentry).
    logger.warn('SSR initial fetch threw outside Promise.allSettled', err);
  }
  return <SharedGamesPageClient initial={initial} contributors={initialContributors} />;
}

// page-client.tsx ('use client')
'use client';
export default function SharedGamesPageClient({ initial, contributors }: Props) {
  const [query, setQuery] = useUrlHashState<string>('q', '');
  const [chips, setChips] = useUrlHashState<FilterChipId[]>('chips', [], {
    serialize: v => (v.length ? v.join(',') : null),
    deserialize: raw => raw.split(',').filter(isFilterChipId),
  });
  const [genre, setGenre] = useUrlHashState<string>('genre', '');
  const [sort, setSort] = useUrlHashState<SortKey>('sort', 'rating');

  const debouncedQuery = useDebouncedValue(query, 300); // hash sync only after 300ms settle
  const result = useSharedGames({
    query: debouncedQuery, chips, genre, sort,
    initialData: initial ?? undefined,
  });

  // Render <SharedGamesHero/> + <SharedGamesFilters/> + <ContributorsSidebar contributors={contributors}/> + <SharedGamesGrid result={result}/>
}
```

**Rationale SSR**: Greenfield route → no v1 SEO loss to recover, BUT mockup `sp3-shared-games` ha hero leggero + 24+ card sopra-the-fold; LCP critico per first-impression Alpha. SSR initial fetch evita FOUC e migliora Core Web Vitals.

**Failure tolerance (Risk #2)**: `Promise.allSettled` ensures one failed dependency (e.g., `top-contributors` 503) doesn't fail the whole page. Client hook re-fetches lazily; sidebar shows skeleton + empty fallback if `contributors=[]` post-mount.

**Hash sync timing (Risk #5)**: query state updates immediately on every keystroke (controlled input UX), but hash write happens via the **debounced value** post-300ms settle. This avoids spamming `replaceState` and prevents `hashchange` event storms in multi-instance scenarios.

### 3.6 Backend config alignment

```jsonc
// apps/api/src/Api/appsettings.Development.json (+ Production se presente)
"SharedGameCatalog": {
  "TopRatedThreshold": 4.0,  // was 4.5 in PR #594, aligned to mockup g.rating >= 8 (stars ≥ 4.0/5)
  "NewWindowDays": 7
}
```

**Test impact**: search any `SearchSharedGamesQueryHandlerTests` fixture asserting `IsTopRated=true` for rating 4.0-4.4 stars (was failing under 4.5 threshold). Adjust expectations.

## 4. Test plan (TDD red phase)

### 4.1 Visual contract (RED first)

`e2e/visual-migrated/sp3-shared-games.spec.ts`:
- 1 test desktop @1440 width, 1 test mobile @375 width.
- Renders `/shared-games` against bootstrapped baseline.
- Mask zones via `data-dynamic`: contributor avatars (random gradients), result counter (varies by seed).
- Stub backend via Playwright route: `GET /api/v1/shared-games*` → fixture JSON 24 games + `GET /shared-games/top-contributors` → fixture 5 contributors.

### 4.2 State coverage (RED first)

`e2e/v2-states/shared-games.spec.ts`:
- 5 states × 2 viewports = 10 PNG.
- States: `default` (24 games + sidebar), `loading` (skeleton 9-card grid + sidebar loading), `empty-search` (query="zzznotfound", 0 result), `filtered-empty` (chip filters yield 0), `api-error` (route fulfill 500).
- Drive via Playwright route mocking (no `stateOverride` prop — production-safe, no test-only escape hatch).

### 4.3 Unit tests (Vitest + Testing Library + jest-axe)

- `useUrlHashState.test.ts`: SSR-safe (initial render returns defaultValue under `typeof window === 'undefined'` mock), post-mount hash read via `useEffect`, no hydration mismatch (assert renderToString output), set updates hash, `hashchange` event syncs, multi-instance sync, unmount cleanup, custom serialize/deserialize, null serialize removes key.
- `shared-games-filters.test.tsx`: chip `aria-pressed` toggle, query callback (no internal debounce — caller responsibility), genre/sort change, result counter `aria-live="polite"`. **a11y test**: `expect(await axe(container)).toHaveNoViolations()` via `jest-axe` (already installed v10).
- `contributors-sidebar.test.tsx`: 5 contributors rendered, loading skeleton, hidden < md (CSS class assertion via `toHaveClass('hidden', 'md:block')`). **a11y test** via jest-axe.
- `meeple-card-game.test.tsx`: renders `<a href="/shared-games/${id}">` (next/link → anchor), EntityChip count threshold (only render if > 0), newWeek badge appears at `>= 2`, focus-visible, aria-label localized. **a11y test** via jest-axe.
- `empty-state.test.tsx`: kind switch (icon + copy), reset button callback, query interpolation.
- `error-state.test.tsx`: retry callback.
- `lib/shared-games/filters.test.ts`: FILTER_CHIPS contains 4 entries, SORT_OPTIONS contains 4 entries, all `backendParam` map to valid `SearchSharedGamesQuery` fields.
- `useSharedGames.test.ts`: fetch on mount with initialData (no loading flash), refetch on filter change, queryKey serialization stable across param order, error → `isError: true`, success path returns typed `SharedGameDto[]`. Use `QueryClientProvider` test wrapper.

E2E a11y check via `@axe-core/playwright` (already installed v4.11.1) inside `e2e/v2-states/shared-games.spec.ts` after each state render — separate from visual snapshot, asserts zero serious/critical violations.

### 4.4 Backend tests adjustment

- Verify if any `SearchSharedGamesQueryHandlerTests` test asserts `IsTopRated` for ratings in [4.0, 4.5). If yes, expectations flip from `false` → `true`.
- New unit test: threshold 4.0 explicit (rating 4.0 → true, rating 3.9 → false).
- No Testcontainers integration test changes expected (filter behavior agnostic to threshold value, only result set differs).

## 5. Acceptance criteria (DoD — 15 items from issue #596)

- [ ] `/shared-games` route renders in `(public)` segment, no auth required
- [ ] Mockup `sp3-shared-games` 1:1 visual fidelity (desktop + mobile)
- [ ] 4 filter chips functional with `aria-pressed` and entity colors
- [ ] 13 genres select + 4 sort options + search debounced 300ms
- [ ] URL hash persistence via `useUrlHashState` (refresh preserves state)
- [ ] `MeepleCardGame` uses `<a href>` for native keyboard nav + SEO
- [ ] EntityChip footer shows tk/ag/kb counts only if > 0
- [ ] `newWeek >= 2` badge visible on cover top-right
- [ ] ContributorsSidebar desktop ≥ md, hidden mobile (no drawer)
- [ ] 5 states implemented (default / loading / empty-search / filtered-empty / api-error)
- [ ] visual-migrated CI green (route prod ≈ bootstrapped baseline within Playwright tolerance)
- [ ] 10 v2-states baselines committed (Linux x86-64 via `gh workflow run`)
- [ ] `pnpm test` 100% pass; new component coverage ≥ 90%
- [ ] axe-core: zero violations on default/empty-search/filtered-empty/error states
- [ ] Backend config `TopRatedThreshold` 4.5 → 4.0 with related test adjustments
- [ ] Bundle delta < +45 KB (target ~35 KB: 9 components + hook + API client + i18n)
- [ ] PR squash body includes `Closes #596`

## 6. Risks & mitigations

| # | Risk | Mitigation |
|---|------|-----------|
| 1 | `useUrlHashState` race con Next.js router (prefetch / soft nav muta `window.location.hash` from outside hook) | Subscribe `hashchange` event (covers all external mutations); test multi-instance sync |
| 2 | SSR initial fetch fails (backend down at build/render time) → 500 page | Try/catch in server component → render with `initial=null`, client fetches lazily; preserve UX |
| 3 | Greenfield route NOT covered by "Migrated Routes Baseline" workflow (no v1 to compare) | Use bootstrap mode for v2-states + visual-migrated; mockup baseline as design contract anyway |
| 4 | Threshold flip 4.5 → 4.0 affects existing prod data (more games marked `isTopRated`) | Acceptable — Alpha launch hasn't happened yet; staging snapshot will reflect new threshold |
| 5 | Search debounce + URL hash sync interaction (user types fast → many hashchange events) | Debounce updates URL hash too (single trailing write); test rapid typing scenario |
| 6 | `<a href>` deviation from mockup may break visual baseline (link styling) | Reset link styles via `text-decoration: none; color: inherit` on the anchor; visual diff < tolerance |
| 7 | Contributors sidebar hidden mobile may surprise users wanting community insight | Acceptable Alpha trade-off — stats accessible via separate `/community` page (post-Alpha); document in DoD |
| 8 | i18n EN keys not vetted by native speaker | Same posture as Wave A.2 — Alpha is IT-first, EN best-effort |

## 7. Out of scope (explicit)

- `/shared-games/[id]` detail page redesign (separate wave)
- Pagination / infinite scroll (Alpha hardcoded `pageSize=100`)
- Mobile drawer for ContributorsSidebar (post-Alpha)
- BGG live fetch in catalog
- Real-time SSE updates on contributors leaderboard
- Per-game contributors scope (decision A.3a: global only)
- Top-contributors profile pages (`/users/[id]/profile` is separate stream)
- Admin tooling to feature/curate games (admin section unchanged)

## 8. Sequencing (5 commits, monolithic PR)

1. **Commit 1**: `feat(web): add useUrlHashState generic hook + SharedGames API client`
   - `useUrlHashState.ts` + tests (RED→GREEN: 8 cases)
   - `lib/api/shared-games.ts` (`searchSharedGames`, `getTopContributors`) typed against backend DTOs
   - `lib/shared-games/filters.ts` constants + tests
2. **Commit 2**: `feat(web): add v2 components for /shared-games index`
   - 9 components in `components/ui/v2/shared-games/*` + index barrel
   - Behavioral unit tests per component (RED→GREEN)
3. **Commit 3**: `feat(web): wire /shared-games page (server+client split) + i18n IT/EN`
   - `page.tsx` (server, metadata, SSR initial fetch) + `page-client.tsx` (orchestrator)
   - `useSharedGames` hook (TanStack Query mutation)
   - i18n keys IT + EN (~35 each)
   - Page-level integration test
4. **Commit 4**: `chore(api): align SharedGameCatalog:TopRatedThreshold 4.5 → 4.0`
   - `appsettings.{Development,Production,Staging}.json` updates
   - `SearchSharedGamesQueryHandlerTests` expectation flips (if any)
   - New unit test asserting threshold boundary 4.0/3.9
5. **Commit 5**: `test(web): visual baselines + state coverage for /shared-games`
   - `e2e/visual-migrated/sp3-shared-games.spec.ts` (RED first)
   - `e2e/v2-states/shared-games.spec.ts` (5 states × 2 viewport, RED first)
   - Bootstrap 12 PNG via `gh workflow run 266963272 --ref feature/issue-596-shared-games-fe-v2 -f mode=bootstrap -f project_filter=both`
   - Download artifact + commit binaries

## 9. Bootstrap baseline workflow (post-impl, pre-merge)

> **Workflow ID `266963272`** = "Visual Regression — Migrated Routes" (verified active 2026-04-28 via `gh workflow list`). Same workflow used in Wave A.1 (PR #586) and Wave A.2 (PR #590) bootstraps.

```bash
# After commits 1-4 merged locally to feature branch + push
gh workflow run 266963272 \
  --ref feature/issue-596-shared-games-fe-v2 \
  -f mode=bootstrap \
  -f project_filter=both

# Wait for run completion
gh run watch <run-id>

# Download artifact
gh run download <run-id> -n visual-shared-games-baselines

# Commit + push
git add apps/web/e2e/visual-migrated/sp3-shared-games.spec.ts-snapshots/*.png
git add apps/web/e2e/v2-states/shared-games.spec.ts-snapshots/*.png
git commit -m "chore(visual): bootstrap /shared-games baselines (Linux x86-64)"
git push
```

Subsequent `verify` mode runs on PR open will validate route prod ≈ committed baseline within Playwright tolerance.

---

**End of DRAFT**. Awaits user approval before TDD red phase begins.
