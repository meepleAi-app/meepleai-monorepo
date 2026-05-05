# V2 Migration · Wave B.1 — `/games` Library Tab Brownfield Spec

**Issue**: #633 (parent: #580 Wave B umbrella · #578 Phase 1)
**Branch**: `feature/issue-633-games-fe-v2` (parent: `main-dev`)
**Mockup**: `admin-mockups/design_files/sp4-games-index.jsx` (1027 LOC)
**Pilot reference**: `2026-04-28-v2-migration-wave-a-3b-shared-games-fe.md` (PR #600 MERGED) — extended URL hash pattern
**Brownfield reference**: `2026-04-28-v2-migration-wave-a-4-shared-game-detail.md` (PR #605 MERGED) — page-client split + visual-test fixture sentinel
**Companion exec plan**: `2026-04-27-v2-migration-phase1-execution.md` §4 (Wave B)
**Wave A prerequisites** (✅ all merged): a11y CI gate (#601 → PR #602), brand-orange contrast fix (#587 → PR #602), `useTablistKeyboardNav` hook (PR #623 + orientation #626), per-route bundle-size budget (#629 → PR #631), V2 migration matrix (#573 → PR #632)
**Status**: REVIEWED 2026-04-29 — spec-panel ultrathink applied 4 BLOCKER + 6 short-term + 3 long-term findings

---

## 1. Goals

1. **Migrare la tab `Libreria` di `/games`** (route `apps/web/src/app/(authenticated)/games/page.tsx`) al pattern v2 `sp4-games-index` — primo brownfield migration di Wave B (vs greenfield A.1/A.2/A.3b/A.5b).
2. **Riusare workflow** validato in Wave A: visual-migrated + v2-states + bootstrap baselines via CI, hybrid masking, cookie-consent suppression via `addInitScript` (lesson learned A.4).
3. **Estrarre 4 componenti v2** in `apps/web/src/components/v2/games/` (tracking matrix `docs/frontend/v2-migration-matrix.md`):
   - `GamesHero` (nasce da `LibraryHero` mockup) — header con stats live `{owned, wishlist, totalEntries, kbDocs}`
   - `GamesFiltersInline` (nasce da `GameFilters` mockup) — search + status segmented tablist + sort dropdown + view toggle (desktop-only)
   - `GamesResultsGrid` (nasce da `GameCardGrid`/`GameRow` mockup) — dispatcher grid/list che riusa `MeepleCard` v1 senza fork
   - `GamesEmptyState` (nasce da `EmptyLibrary` + `ErrorState` + `SkeletonGrid` mockup) — discriminated `kind: 'empty' | 'filtered-empty' | 'error' | 'loading'`
4. **Riusare `MeepleCard` v1** con `entity="game"` e `variant="grid"|"list"` per le card — ZERO fork, mandate da CLAUDE.md "Card Components".
5. **Riusare `useTablistKeyboardNav`** (PR #623) per il status segmented tablist (orientation `horizontal`).
6. **Defer `AdvancedFiltersDrawer`** (BLOCKER #1 risolto via Option B): mockup non lo definisce, lo stub `apps/web/src/components/v2/games/AdvancedFiltersDrawer.tsx` resta placeholder per B.2 `/agents` o follow-up dedicato. Rimosso da scope B.1.
7. **i18n bilingue IT+EN** (~30 keys nel namespace `pages.games.library`) — nessuna stringa hard-coded.
8. **Bundle budget gate** (`frontend-bundle-size`): target Δ ≤ 35 KB First Load JS per `/games`, hard limit +50 KB.

## 2. Non-goals

- **Tab `Catalogo` e `Knowledge Base`**: restano v1 (HubLayout) fino a wave dedicate. La logica `?tab=` URL routing rimane invariata.
- **`AdvancedFiltersDrawer`**: deferito (Option B, BLOCKER #1) — mockup non lo definisce. B.2 `/agents` può introdurlo OR follow-up dedicato con design proper.
- **Pagination**: hardcoded `pageSize=20` (esistente). Pagination UX è out-of-scope B.1, candidate B.4+ se needed.
- **Backend changes**: nessuna modifica a `GET /api/v1/library` o `GET /api/v1/games`. Solo verifica pre-impl di campi `playCount`/`lastPlayedAt` (vedi §3.4 Data Contract).
- **HubLayout migration**: la shell layout `HubLayout` rimane invariata per la tab Libreria (mini-nav tabs Libreria/Catalogo/KB stay, top nav stay). Solo il body content cambia per la tab attiva = `library`.
- **`MeepleCard` extension**: niente prop nuove. Se serve customization (cover gradient mockup), si fa via wrapper o si defere a v2 spec dedicato `MeepleCard`.
- **Refactor di `useGames`/`useLibrary`** hooks: si riusano as-is. Eventuali estensioni vanno in B.4+ catalog.
- **Server Component conversion**: la pagina rimane `'use client'` (oggi è già client). SSR seed via TanStack Query è out-of-scope (Wave A.3b ha già il pattern, applicabile in followup se needed).

## 3. Architecture

### 3.1 File map

| Tipo | Path | Status |
|------|------|--------|
| Page (existing) | `apps/web/src/app/(authenticated)/games/page.tsx` | **Edit** (split tab `library` body in `<GamesLibraryView>` component, mantenere `?tab=` routing) |
| New view | `apps/web/src/app/(authenticated)/games/_components/GamesLibraryView.tsx` | **Create** (orchestrator: state FSM + filtered data + view dispatch) |
| View test | `apps/web/src/app/(authenticated)/games/_components/__tests__/GamesLibraryView.test.tsx` | **Create** |
| Component | `apps/web/src/components/v2/games/GamesHero.tsx` | **Edit** (stub → impl) |
| Component | `apps/web/src/components/v2/games/GamesFiltersInline.tsx` | **Edit** (stub → impl) |
| Component | `apps/web/src/components/v2/games/GamesResultsGrid.tsx` | **Edit** (stub → impl) |
| Component | `apps/web/src/components/v2/games/GamesEmptyState.tsx` | **Edit** (stub → impl) |
| Component | `apps/web/src/components/v2/games/AdvancedFiltersDrawer.tsx` | **Skip** (resta stub, BLOCKER #1 deferred) |
| Component index | `apps/web/src/components/v2/games/index.ts` | **Create** (barrel: hero, filters, results, empty) |
| Component tests | `apps/web/src/components/v2/games/__tests__/{GamesHero,GamesFiltersInline,GamesResultsGrid,GamesEmptyState}.test.tsx` | **Create** (4 test files) |
| Status filter helper | `apps/web/src/lib/games/library-filters.ts` | **Create** (pure helpers: `filterByStatus`, `sortLibraryEntries`, `deriveStats`) |
| Helper test | `apps/web/src/lib/games/__tests__/library-filters.test.ts` | **Create** (pure unit, fast) |
| Visual test fixture | `apps/web/src/lib/games/visual-test-fixture.ts` | **Create** (sentinel pattern A.4: `IS_VISUAL_TEST_BUILD` env-gated SSR-safe library mock) |
| i18n IT | `apps/web/src/locales/it.json` § `pages.games.library` | **Add** (~30 keys) |
| i18n EN | `apps/web/src/locales/en.json` § `pages.games.library` | **Add** (~30 keys) |
| Visual test | `apps/web/e2e/visual-migrated/sp4-games-index.spec.ts` | **Create** (1 desktop + 1 mobile = 2 PNG) |
| State test | `apps/web/e2e/v2-states/games-library.spec.ts` | **Create** (4 stati × 2 viewport = 8 PNG: default/loading/empty/filtered-empty) |
| A11y test | `apps/web/e2e/a11y/games-library.spec.ts` | **Create** (axe-core scan default + filtered-empty) |
| Baselines | `apps/web/e2e/visual-migrated/sp4-games-index.spec.ts-snapshots/*.png` | **Create** (2 PNG via CI bootstrap) |
| Baselines | `apps/web/e2e/v2-states/games-library.spec.ts-snapshots/*.png` | **Create** (8 PNG via CI bootstrap) |
| Matrix update | `docs/frontend/v2-migration-matrix.md` | **Edit** (rows GamesHero/GamesFiltersInline/GamesResultsGrid/GamesEmptyState: `Status: pending → done`, `PR: TBD → #<n>`. AdvancedFiltersDrawer resta `pending` con nota "deferred B.1, see spec §6.1") |

### 3.2 Component API

#### `GamesLibraryView` (orchestrator, internal `_components/`)

```ts
import type { ReactElement } from 'react';

export interface GamesLibraryViewProps {
  readonly initialState?: 'default' | 'loading' | 'empty' | 'filtered-empty' | 'error';
  // initialState non-undefined ONLY in dev/test via ?state= URL param; production path = undefined
}

export function GamesLibraryView(props: GamesLibraryViewProps): ReactElement;
```

**Stato interno** (5-state FSM derivata):
- `loading` ← `useLibrary()` `isLoading === true`
- `error` ← `useLibrary()` `error !== null`
- `empty` ← `data.items.length === 0` (libreria proprio vuota, no entries)
- `filtered-empty` ← `data.items.length > 0` && `filtered.length === 0` (filtri attivi non matchano)
- `default` ← `data.items.length > 0` && `filtered.length > 0`

**State override** (escape hatch per Playwright visual tests):
- Guard `process.env.NODE_ENV !== 'production'` (mirror A.2/A.3b/A.4 pattern, dead-code-eliminated in prod build).
- `?state=` URL param via `useSearchParams()`.
- Quando override attivo: bypassa `useLibrary()` data, usa `tryLoadVisualTestFixture()` per mock data deterministico (sentinel `IS_VISUAL_TEST_BUILD`).

#### `GamesHero`

```ts
export interface GamesHeroStat {
  readonly label: string; // i18n-resolved (no key passed in)
  readonly value: number;
}

export interface GamesHeroProps {
  readonly stats: readonly GamesHeroStat[]; // 4 items: Giochi/Partite/Posseduti/Wishlist
  readonly compact?: boolean; // mobile breakpoint (caller-driven, no internal media query)
}

export function GamesHero(props: GamesHeroProps): ReactElement;
```

**A11y**:
- `<header role="banner">` (or implicit if rendered as `<header>` direct child of main)
- `<h1>` con i18n title `pages.games.library.title`
- Stats: `<dl>` semantic (term/description) for screen-reader association

#### `GamesFiltersInline`

```ts
export type GamesStatusKey = 'all' | 'owned' | 'wishlist' | 'played';
export type GamesSortKey = 'last-played' | 'rating' | 'title' | 'year';

export interface GamesFiltersInlineProps {
  readonly query: string;
  readonly onQueryChange: (next: string) => void;
  readonly status: GamesStatusKey;
  readonly onStatusChange: (next: GamesStatusKey) => void;
  readonly sort: GamesSortKey;
  readonly onSortChange: (next: GamesSortKey) => void;
  readonly view: 'grid' | 'list';
  readonly onViewChange: (next: 'grid' | 'list') => void;
  readonly resultCount: number;
  readonly compact?: boolean; // mobile: hide view toggle, show count chip
}

export function GamesFiltersInline(props: GamesFiltersInlineProps): ReactElement;
```

**A11y / WAI-ARIA**:
- Status segmented: `role="tablist"` + 4 `role="tab"` con `aria-selected`. Roving tabindex via `useTablistKeyboardNav<GamesStatusKey>` (orientation `horizontal`, wrap).
- Sort: native `<select>` with `<label>` `Ordina` (i18n `pages.games.library.filters.sortLabel`).
- View toggle: `role="group" aria-label="Vista"` con 2 button `aria-pressed`.
- Search: `<input type="search">` con `<label>` (`<span class="sr-only">`) e icon `aria-hidden`.
- Search debounce: 300ms (mirror A.3b precedent), trailing edge.

#### `GamesResultsGrid`

```ts
export type GamesResultsView = 'grid' | 'list';

export interface GamesResultsGridProps {
  readonly entries: readonly UserLibraryEntry[];
  readonly view: GamesResultsView;
  readonly compact?: boolean; // mobile: force grid, hide list view dispatcher
}

export function GamesResultsGrid(props: GamesResultsGridProps): ReactElement;
```

**Mapping a `MeepleCard`**:
```ts
// Per ogni entry → MeepleCard props
{
  entity: 'game',
  variant: view === 'list' ? 'list' : 'grid',
  id: entry.id,
  title: entry.gameTitle,
  subtitle: entry.gamePublisher ?? undefined,
  imageUrl: entry.gameImageUrl ?? undefined,
  rating: entry.averageRating ?? undefined,
  ratingMax: 10,
  href: `/games/${entry.gameId}`,
}
```

**Layout responsive**:
- Mobile (`compact=true`): `grid grid-cols-2 gap-3 px-4`
- Desktop grid view: `grid grid-cols-3 lg:grid-cols-4 gap-4 px-8`
- Desktop list view: `flex flex-col gap-2 px-8`

#### `GamesEmptyState`

```ts
export type GamesEmptyKind = 'empty' | 'filtered-empty' | 'error' | 'loading';

export interface GamesEmptyStateProps {
  readonly kind: GamesEmptyKind;
  readonly onClearFilters?: () => void; // required when kind === 'filtered-empty'
  readonly onRetry?: () => void; // required when kind === 'error'
}

export function GamesEmptyState(props: GamesEmptyStateProps): ReactElement;
```

**Discriminated UI**:
- `kind="loading"` → 8 skeleton cards (mobile=4) via `<Skeleton>` from `@/components/ui/feedback/skeleton`
- `kind="empty"` → mockup `EmptyLibrary` cfg `library` (icon 🎲, CTA "Aggiungi un gioco" → `/games/new`)
- `kind="filtered-empty"` → mockup `EmptyLibrary` cfg `filtered` (icon 🔎, CTA "Azzera filtri" → `onClearFilters()`)
- `kind="error"` → mockup `ErrorState` (icon ⚠, CTA "Riprova" → `onRetry()`)

### 3.3 Data shape — Status filter derivation

Backend DTO `UserLibraryEntry` ([source](../../../apps/web/src/lib/api/schemas/library.schemas.ts:32)):
```ts
{
  id: string,
  userId: string,
  gameId: string,
  gameTitle: string,
  currentState: 'Nuovo' | 'InPrestito' | 'Wishlist' | 'Owned', // ✅ esistente
  // ... averageRating, gameImageUrl, etc.
  // ❌ NO playCount, NO lastPlayedAt, NO totalSessions
}
```

**Mapping mockup `STATUS_OPTS` → backend `currentState`**:
| Mockup status key | UI label IT/EN | Filter predicate | Backend dependency |
|-------------------|----------------|-------------------|---------------------|
| `all` | Tutti / All | `() => true` | none |
| `owned` | Posseduti / Owned | `e => e.currentState === 'Owned'` | ✅ esistente |
| `wishlist` | Wishlist / Wishlist | `e => e.currentState === 'Wishlist'` | ✅ esistente |
| `played` | Giocati / Played | ⚠️ vedi §3.4 BLOCKER #3 verification step | requires backend extension OR derivation |

**Sort mapping**:
| Mockup sort key | UI label IT/EN | Sort comparator | Notes |
|-----------------|----------------|-----------------|-------|
| `last-played` | Ultima partita / Recently played | proxy: `addedAt` desc | ⚠️ vedi §3.4 — fallback se no `lastPlayedAt` |
| `rating` | Rating / Rating | `b.averageRating - a.averageRating` (null = 0) | ✅ |
| `title` | Titolo A-Z / Title A-Z | `a.gameTitle.localeCompare(b.gameTitle, 'it')` | ✅ |
| `year` | Anno / Year | `(b.gameYearPublished ?? 0) - (a.gameYearPublished ?? 0)` | ✅ |

### 3.4 Backend dependencies & verification (BLOCKER #3 resolution)

**Pre-implementation verification step (BLOCKING for impl)**:

Eseguire (con backend running, e.g. `make dev-core`):
```bash
# 1. Verifica esistenza endpoint library
curl -s http://localhost:8080/api/v1/library?page=1&pageSize=5 \
  -H "Cookie: $(cat ~/.meepleai/dev-session-cookie)" | jq '.items[0] | keys'

# Atteso (current): id, userId, gameId, gameTitle, currentState, averageRating, addedAt, ...
# Cerca: playCount, lastPlayedAt, totalSessions
```

**Branching decision tree**:

- **Caso A — backend HA `playCount` o `lastPlayedAt`**: usare diretto.
  - `played` filter: `e => (e.playCount ?? 0) > 0`
  - `last-played` sort: `(b.lastPlayedAt ?? 0) - (a.lastPlayedAt ?? 0)` (epoch compare)
- **Caso B — backend NON HA campi (atteso, current state)**:
  - **`played` filter**: opzioni:
    1. **Hide filter**: rimuovere `played` da `STATUS_OPTS`, mostrare solo `all/owned/wishlist`. Ridurre AC-2 da 4→3 status filtri.
    2. **Cross-API derivation**: `useGameSessions(gameId)` per ogni entry — costoso (N×1 fetch) e flake risk. **NOT RECOMMENDED**.
    3. **Backend extension** child issue (Wave B sibling, blocked-by): aggiungi `playCount` e `lastPlayedAt` su `UserLibraryEntry` DTO via session count aggregation.
  - **Decisione default (se Caso B)**: opzione 1 (hide filter), creare child issue tracking opzione 3 per Wave B.4+. Aggiornare AC-2.

**Verification gate**: PRIMA di iniziare impl (commit 1), eseguire curl sopra. Documentare risultato in PR description sezione "Backend verification". Se Caso A, no action. Se Caso B, applicare opzione 1 + creare child issue.

### 3.5 i18n keys (`pages.games.library.*`)

```jsonc
{
  "pages": {
    "games": {
      "library": {
        "title": "La tua libreria di giochi",
        "subtitle": "Esplora, filtra e apri i tuoi giochi. Ogni card mostra le connessioni attive.",
        "stats": {
          "total": "Giochi",
          "totalPlays": "Partite",
          "owned": "Posseduti",
          "wishlist": "Wishlist"
        },
        "filters": {
          "searchPlaceholder": "Cerca per titolo, autore, anno…",
          "searchClear": "Pulisci",
          "statusLabel": "Stato",
          "status": {
            "all": "Tutti",
            "owned": "Posseduti",
            "wishlist": "Wishlist",
            "played": "Giocati"
          },
          "sortLabel": "Ordina",
          "sort": {
            "lastPlayed": "Ultima partita",
            "rating": "Rating",
            "title": "Titolo A-Z",
            "year": "Anno"
          },
          "viewLabel": "Vista",
          "view": { "grid": "Griglia", "list": "Lista" },
          "resultCount": "{{count}} giochi"
        },
        "emptyState": {
          "empty": {
            "title": "La tua libreria è vuota",
            "subtitle": "Aggiungi il primo gioco per iniziare a tracciare partite, caricare regolamenti e creare agenti AI esperti.",
            "cta": "Aggiungi un gioco"
          },
          "filteredEmpty": {
            "title": "Nessun risultato",
            "subtitle": "Nessun gioco corrisponde ai filtri attuali. Prova a modificare la ricerca o azzera i filtri.",
            "cta": "Azzera filtri"
          },
          "error": {
            "title": "Impossibile caricare la libreria",
            "subtitle": "Si è verificato un errore di rete. Verifica la connessione e riprova.",
            "cta": "Riprova"
          }
        }
      }
    }
  }
}
```

EN mirror in `apps/web/src/locales/en.json`. Total ~30 keys × 2 locales.

### 3.6 Server/client split

`/games/page.tsx` rimane `'use client'` come oggi (existing brownfield, già client). NO server component split per B.1 — out-of-scope.

`GamesLibraryView` is client-side, riceve `searchParams` via `useSearchParams()` per `?state=` override e `?tab=` (esistente).

**Reasoning**: Wave A.3b/A.4 hanno introdotto pattern server+client split. Per B.1 brownfield, lo scope è limitato al body della tab `library`. Splittare il page in server+client richiederebbe refactor del routing `?tab=` e estendere lo scope. Server fetch è candidate per Wave B.4+ ottimizzazione.

### 3.7 Bundle budget (Wiegers + Nygard)

**Target**: Δ +15-25 KB First Load JS per `/games` rispetto a baseline pre-PR.
**Hard limit**: +50 KB (gate `frontend-bundle-size` PR #631 fail).
**Strategia**:
- Riuso `MeepleCard` v1 (zero duplicate). Riuso `useTablistKeyboardNav` hook (zero new lib).
- 4 v2 component nuovi inline-sized (no external deps). Stima: ~8 KB gzipped.
- i18n keys: ~30 × 2 locales ≈ 1.5 KB ciascuno.
- Skeleton + EmptyState reuse `Skeleton` v1.

**Mitigazione overflow**:
- Se >35 KB: code-split `GamesEmptyState` con `next/dynamic` (loading state già skeleton).
- Se >50 KB: investigate `MeepleCard` re-tree-shake — escalation (block PR).

## 4. Test plan

### 4.1 Visual baselines (Playwright `visual-migrated` + `v2-states`)

| Spec | Coverage | PNG count | Viewport |
|------|----------|-----------|----------|
| `visual-migrated/sp4-games-index.spec.ts` | Default state pixel-match con mockup baseline | 2 | desktop 1440 + mobile 375 |
| `v2-states/games-library.spec.ts` | 4 stati FSM (default/loading/empty/filtered-empty) | 8 | desktop 1440 + mobile 375 |

**Bootstrap workflow**: `visual-regression-migrated.yml` (Wave A pattern):
1. Set env `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1`
2. Set env `NEXT_PUBLIC_LOCALE=it` (default)
3. `pnpm build`
4. Run Playwright → genera baselines via `--update-snapshots` su CI runner Linux
5. Upload artifact `visual-migrated-baselines` per inspect locale
6. PR review owner downloads artifact, copia PNG in `*.spec.ts-snapshots/`, commit con `chore(visual): bootstrap canonical baselines for /games library`

**Cookie consent suppression** (lesson learned A.4):
```ts
// e2e/_helpers/seedCookieConsent.ts
export const seedCookieConsent = (page: Page) =>
  page.addInitScript(() => {
    localStorage.setItem('meepleai-cookie-consent', JSON.stringify({
      version: '1.0', essential: true, analytics: true, functional: true,
      timestamp: Date.now(),
    }));
  });
```
Applicare PRIMA di `page.goto()` in tutti gli spec con fullPage screenshots su mobile.

### 4.2 Unit tests (Vitest)

| File | Coverage |
|------|----------|
| `lib/games/__tests__/library-filters.test.ts` | Pure helpers: `filterByStatus(entries, key)` × 4 status, `sortLibraryEntries(entries, sortKey)` × 4 sort, `deriveStats(entries)` cardinality + edge case empty. ~15 test |
| `components/v2/games/__tests__/GamesHero.test.tsx` | Render con stats array, fallback 0 values, compact prop layout switch. ~5 test |
| `components/v2/games/__tests__/GamesFiltersInline.test.tsx` | Search input controlled, debounce trailing-edge timing (vi.useFakeTimers), status tablist keyboard nav (Arrow/Home/End wrap), sort onChange, view toggle aria-pressed. ~15 test |
| `components/v2/games/__tests__/GamesResultsGrid.test.tsx` | Mapping entries → MeepleCard props, view dispatch grid/list, compact force-grid mobile. ~8 test |
| `components/v2/games/__tests__/GamesEmptyState.test.tsx` | 4 kind variants render, `onClearFilters` callback, `onRetry` callback, missing required callback throws (dev-only assert). ~8 test |
| `app/(authenticated)/games/_components/__tests__/GamesLibraryView.test.tsx` | FSM derivation: loading/error/empty/filtered-empty/default; useLibrary mock; state override `?state=` guard; clear filters resets q+status. ~10 test |

**Total target**: ~60 unit tests, all green pre-PR.

### 4.3 A11y test (Playwright + axe-core)

`apps/web/e2e/a11y/games-library.spec.ts`:
```ts
test('default state passes axe-core scan WCAG 2.1 AA', async ({ page }) => {
  await seedCookieConsent(page);
  await page.goto('/games?tab=library');
  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
    .analyze();
  expect(results.violations).toEqual([]);
});

test('filtered-empty state passes axe-core scan', async ({ page }) => {
  await seedCookieConsent(page);
  await page.goto('/games?tab=library&state=filtered-empty');
  // ... axe scan
});
```

CI gate `frontend-a11y` (PR #602) **MUST PASS** prima merge.

### 4.4 Behavior scenarios (Given/When/Then) — pre-impl reference, NOT new test files

I 6 GWT scenarios elencati nel body issue #633 sono coperti da:
- **Search debounce**: `GamesFiltersInline.test.tsx` con fake timers
- **Empty filtered → reset**: `GamesLibraryView.test.tsx` clearFilters callback
- **Status keyboard nav**: hook `useTablistKeyboardNav` già testato (PR #623), + `GamesFiltersInline.test.tsx` integrazione
- **View toggle mobile-hidden**: `GamesFiltersInline.test.tsx` con `compact={true}`
- **Empty library**: `GamesEmptyState.test.tsx` + visual snapshot `v2-states/games-library.spec.ts`
- **prefers-reduced-motion**: `e2e/a11y/games-library.spec.ts` setta media via Playwright `emulateMedia({ reducedMotion: 'reduce' })` e verifica no animazione card hover

## 5. Acceptance criteria (13 SMART)

> **Convenzione**: AC-N · `<criterio testabile>` · evidence: `<test/snapshot/check>`

- **AC-1** · La tab `library` di `/games` rendera mockup `sp4-games-index` pixel-match desktop 1440 + mobile 375. · Evidence: 2 PNG `visual-migrated/sp4-games-index.spec.ts-snapshots/*-linux.png` PASS in CI.
- **AC-2** · I filtri inline rispecchiano il mockup esattamente: search + status segmented (Tutti/Posseduti/Wishlist/[Giocati|hidden se backend NO `playCount`]) + sort dropdown (Ultima partita/Rating/Titolo A-Z/Anno) + view toggle (desktop-only). · Evidence: `GamesFiltersInline.test.tsx` 15 test green.
- **AC-3** · Le card riusano `MeepleCard` v1 con `entity="game"` e `variant="grid"|"list"`. ZERO fork del componente. · Evidence: grep `MeepleCard.*entity.*game` in `GamesResultsGrid.tsx` matcha; assenza di `// fork` o `MeepleCard.v2` import.
- **AC-4** · Status segmented tablist supporta WAI-ARIA APG con keyboard nav: Arrow Left/Right (wrap), Home/End. Roving tabindex. Automatic activation. · Evidence: `GamesFiltersInline.test.tsx` keyboard scenarios + manual Tab→Arrow nav green.
- **AC-5** · 4 stati FSM (default/loading/empty/filtered-empty/error) renderano correttamente con discriminated UI. · Evidence: 8 PNG `v2-states/games-library.spec.ts-snapshots/*-linux.png` PASS + `GamesLibraryView.test.tsx` FSM coverage 100%.
- **AC-6** · Search input debouncing 300ms trailing-edge: digitando "x", "xy", "xyz" in <100ms si scatena UN SOLO filter cycle. · Evidence: `GamesFiltersInline.test.tsx` con `vi.useFakeTimers` verifica callback chiamato 1 volta.
- **AC-7** · Empty filtered state mostra CTA "Azzera filtri" che resetta `query=''` + `status='all'` (sort/view restano). · Evidence: `GamesLibraryView.test.tsx` clearFilters integration test.
- **AC-8** · `prefers-reduced-motion: reduce` disabilita transition card hover (`transform`, `box-shadow`). · Evidence: `e2e/a11y/games-library.spec.ts` con `emulateMedia({ reducedMotion: 'reduce' })` + computed style assert.
- **AC-9** · `frontend-a11y` axe-core scan WCAG 2.1 AA: 0 violations su default + filtered-empty. · Evidence: CI gate PASS.
- **AC-10** · `frontend-bundle-size` gate: Δ First Load JS per `/games` ≤ +50 KB hard limit (target +35 KB). · Evidence: CI gate PASS, report nel PR comment.
- **AC-11** · i18n bilingue IT+EN: 0 stringhe hard-coded nei 4 v2 component. Switch locale rerender label. · Evidence: grep `'Tutti'\|'Posseduti'\|'La tua libreria'` in `components/v2/games/*.tsx` ritorna 0 match (tutto da `useTranslation()`).
- **AC-12** · Test pyramid full green pre-merge: ~60 unit (Vitest) + 6 visual snapshots (10 PNG totali) + 2 a11y E2E. · Evidence: PR CI summary tutti green tranne codecov/patch (pattern Wave A).
- **AC-13** · Catalog tab + KB tab restano invariate (no regressioni). · Evidence: visit manual `/games?tab=catalog` + `/games?tab=kb` in dev mostra layout v1 unchanged + esistenti unit test passano.

## 6. Risks & mitigations

| ID | Risk | Probability | Impact | Mitigation |
|----|------|-------------|--------|-----------|
| R1 | Backend `currentState` enum drift (e.g. nuovo valore `'Played'`) breakka filter | Low | Medium | Schema `GameStateTypeWithFallbackSchema` già defensive (fallback `'Owned'` + log). Hidden behind helper `filterByStatus`. |
| R2 | "Giocati" filter dependency su `playCount`/`lastPlayedAt` mancante | High | Medium | §3.4 verification gate pre-impl. Default Caso B = hide filter, child issue tracking. |
| R3 | Bundle size overflow >50 KB | Low | High | §3.7 mitigazione: code-split EmptyState con `next/dynamic`. Bundle gate fail PR. |
| R4 | Visual baseline flake (cookie consent banner shifting layout) | Medium | High | `seedCookieConsent` helper in `e2e/_helpers/`. Lesson learned A.4 PR #605. |
| R5 | Strict-mode locator violation (multi-tabpanel mounted con `hidden` attr → multiple matches) | Low | Medium | Scope locator a tabpanel attivo via `data-kind` o `[hidden] >> nth=...`. Pattern A.4. |
| R6 | `useGames` hook signature change durante impl | Very Low | Low | Sniff signature in §3.3 baseline. Se cambia mid-PR, rebase. |
| R7 | i18n hot-reload stale durante dev | Low | Low | `pnpm dev` restart o `--clear-cache`. Documentato in dev guide. |
| R8 | `/games?tab=library` regressione catalog/kb | Low | High | AC-13 manual smoke + E2E spec esistente per `/games?tab=catalog`. |
| R9 | `MeepleCard` `variant="list"` non supportato → fallback grid | Medium | Low | Verify pre-impl: `grep "variant.*list" apps/web/src/components/ui/data-display/meeple-card`. Se mancante: AC-2 ridotto a grid-only, list view come follow-up. |

## 7. Out of scope (explicit)

- ❌ `AdvancedFiltersDrawer` (BLOCKER #1 deferred, Option B)
- ❌ Tab `Catalogo` migration (separate wave B.4+)
- ❌ Tab `Knowledge Base` migration (separate wave B.4+)
- ❌ Server Component conversion del page principale
- ❌ Pagination UI (pageSize 20 hardcoded)
- ❌ Real-time SSE updates su library
- ❌ BGG fetch live in Library tab (resta in `/games/new`)
- ❌ Backend extension `playCount`/`lastPlayedAt` (child issue separato se serve)
- ❌ `MeepleCard` extension (variant nuovi, prop nuove)

## 8. Sequencing — 5 commits TDD

### Commit 1 — Foundation: helpers + i18n + visual fixture

**Files**:
- `apps/web/src/lib/games/library-filters.ts` (pure helpers)
- `apps/web/src/lib/games/__tests__/library-filters.test.ts` (~15 test, all green)
- `apps/web/src/lib/games/visual-test-fixture.ts` (sentinel pattern A.4)
- `apps/web/src/locales/it.json` (~30 keys aggiunte)
- `apps/web/src/locales/en.json` (~30 keys aggiunte)
- `apps/web/e2e/_helpers/seedCookieConsent.ts` (se non esiste già)

**Validation**:
- `pnpm test apps/web/src/lib/games/__tests__/library-filters.test.ts` → 15/15 green
- `pnpm typecheck` → 0 errors
- **Pre-flight backend verification (§3.4)**: documentare risultato

**Commit message**: `feat(games): foundation helpers + i18n keys for /games library v2 (#633)`

### Commit 2 — Component family TDD

**Files**:
- `apps/web/src/components/v2/games/GamesHero.tsx` (impl)
- `apps/web/src/components/v2/games/GamesFiltersInline.tsx` (impl con `useTablistKeyboardNav`)
- `apps/web/src/components/v2/games/GamesResultsGrid.tsx` (impl)
- `apps/web/src/components/v2/games/GamesEmptyState.tsx` (impl)
- `apps/web/src/components/v2/games/index.ts` (barrel)
- `apps/web/src/components/v2/games/__tests__/*.test.tsx` (4 file, ~36 test totali)

**Order TDD per ogni component**:
1. Write failing test (red)
2. Impl minimum to green (green)
3. Refactor (no behavior change)

**Validation**:
- `pnpm test apps/web/src/components/v2/games` → ~36/36 green
- `pnpm typecheck` → 0 errors
- `pnpm lint apps/web/src/components/v2/games` → 0 errors

**Commit message**: `feat(games): GamesHero/Filters/Results/EmptyState v2 components (#633)`

### Commit 3 — Page integration: GamesLibraryView orchestrator

**Files**:
- `apps/web/src/app/(authenticated)/games/_components/GamesLibraryView.tsx` (impl)
- `apps/web/src/app/(authenticated)/games/_components/__tests__/GamesLibraryView.test.tsx` (~10 test)
- `apps/web/src/app/(authenticated)/games/page.tsx` (edit: tab `library` body → `<GamesLibraryView>`)

**Validation**:
- `pnpm test apps/web/src/app/(authenticated)/games` → ~10/10 green
- Manual smoke: `pnpm dev` → `/games?tab=library` mostra v2 layout, `/games?tab=catalog` invariato (v1)
- `pnpm typecheck` → 0 errors

**Commit message**: `feat(games): GamesLibraryView orchestrator + page integration (#633)`

### Commit 4 — Visual + state + a11y E2E specs (NO baselines yet)

**Files**:
- `apps/web/e2e/visual-migrated/sp4-games-index.spec.ts`
- `apps/web/e2e/v2-states/games-library.spec.ts`
- `apps/web/e2e/a11y/games-library.spec.ts`

**Validation**:
- `pnpm test:e2e apps/web/e2e/a11y/games-library.spec.ts` → 2 axe scans green
- Visual specs FAIL su CI prima bootstrap (no baseline) — atteso, fix in commit 5

**Commit message**: `test(games): visual-migrated + v2-states + a11y E2E for /games library (#633)`

### Commit 5 — Bootstrap canonical baselines

**Workflow CI**: trigger `visual-regression-migrated.yml` con `--update-snapshots` su questo branch.

**Steps**:
1. Workflow run su branch genera artifact `visual-migrated-baselines-<run-id>` (10 PNG: 2 visual-migrated + 8 v2-states)
2. Download artifact local, copia PNG in:
   - `apps/web/e2e/visual-migrated/sp4-games-index.spec.ts-snapshots/`
   - `apps/web/e2e/v2-states/games-library.spec.ts-snapshots/`
3. Filter copy: NON sovrascrivere baselines di altre route (mirror pattern A.3b/A.4)
4. Update `docs/frontend/v2-migration-matrix.md`: rows GamesHero/GamesFiltersInline/GamesResultsGrid/GamesEmptyState `Status: pending → done`, `PR: TBD → #<n>`. AdvancedFiltersDrawer riga: aggiungi nota "deferred B.1 — see spec §6.1".

**Validation**:
- Re-run CI workflow su branch: visual specs green, no baseline missing
- All gates green: `frontend-a11y` ✅, `frontend-bundle-size` ✅, `Migrated Routes Baseline` ✅

**Commit message**: `chore(visual): bootstrap canonical baselines for /games library v2 (#633)`

### Final — PR creation

**Title**: `feat(games): migrate /games library tab to v2 design (#633)`
**Body**: include
- Closes #633 + Refs #580 + Refs #578
- Backend verification result (Caso A or B per §3.4)
- Bundle delta report
- Spec link `docs/superpowers/specs/2026-04-29-v2-migration-wave-b-1-games.md`
- Test summary (unit count + visual baselines + a11y)
- Out-of-scope explicit (drawer deferred, catalog/kb tabs untouched)

**Base branch**: `main-dev` (per CLAUDE.md PR Target Rule, parent branch detection).
**Merge strategy**: squash + `--admin --delete-branch` (Wave A pattern, codecov/patch oscillatory flake).

## 9. Bootstrap baseline workflow — recap

```bash
# Step 1: Push branch with E2E specs (commit 4)
git push -u origin feature/issue-633-games-fe-v2

# Step 2: Trigger workflow with --update-snapshots
gh workflow run visual-regression-migrated.yml \
  --ref feature/issue-633-games-fe-v2 \
  -f update_snapshots=true

# Step 3: Wait + download artifact
gh run watch <run-id>
gh run download <run-id> -n visual-migrated-baselines-<run-id> -D ./tmp-baselines

# Step 4: Copy filtered PNG (only for /games, not other routes)
cp tmp-baselines/visual-migrated/sp4-games-index.spec.ts-snapshots/*.png \
   apps/web/e2e/visual-migrated/sp4-games-index.spec.ts-snapshots/
cp tmp-baselines/v2-states/games-library.spec.ts-snapshots/*.png \
   apps/web/e2e/v2-states/games-library.spec.ts-snapshots/

# Step 5: Cleanup + commit
rm -rf tmp-baselines
git add apps/web/e2e/visual-migrated/sp4-games-index.spec.ts-snapshots/ \
        apps/web/e2e/v2-states/games-library.spec.ts-snapshots/
git commit -m "chore(visual): bootstrap canonical baselines for /games library v2 (#633)"
git push

# Step 6: Re-run workflow → all green
gh workflow run visual-regression-migrated.yml --ref feature/issue-633-games-fe-v2
```

## 10. Spec-panel review — applied findings

### 10.1 BLOCKERs (4) — resolved

| # | Finding | Expert | Resolution |
|---|---------|--------|-----------|
| BLOCKER #1 | Drawer not in mockup, source-of-truth conflict | Wiegers + Adzic + Cockburn | **Option B**: defer drawer da B.1. Stub resta placeholder. Spec §2 + §6.1. AC-2 corretto. |
| BLOCKER #2 | Filter set inconsistency: AC dice "search+category+minPlayers" ma mockup ha "search+status+sort+view" | Adzic | AC-2 riscritto su filtri-mockup. §3.5 i18n keys allineati. |
| BLOCKER #3 | Data contract gap: backend `playCount`/`lastPlayedAt` esistenza non verificata | Nygard + Wiegers | §3.4 verification gate pre-impl con curl + decision tree Caso A/B. AC-2 condizionale. |
| BLOCKER #4 | MeepleCard reuse ambiguity (fork vs no-fork) | Fowler | AC-3 esplicito: ZERO fork. CLAUDE.md mandate. §3.2 mapping props. |

### 10.2 Short-term (6) — applied

| # | Finding | Expert | Resolution |
|---|---------|--------|-----------|
| ST-1 | A11y status tablist WAI-ARIA APG | Crispin | AC-4 + `useTablistKeyboardNav` mandate + a11y E2E AC-9 |
| ST-2 | Search debounce timing not specified | Adzic | §3.2 GamesFiltersInline 300ms trailing-edge + AC-6 fake timers test |
| ST-3 | prefers-reduced-motion compliance | Crispin | AC-8 + e2e a11y `emulateMedia` |
| ST-4 | Bundle budget hard limit | Nygard | §3.7 + AC-10 + mitigazione code-split |
| ST-5 | i18n hard-coded strings risk | Doumont | AC-11 grep-based assertion |
| ST-6 | Visual flake mitigation cookie banner | Crispin | §4.1 `seedCookieConsent` helper + R4 risk |

### 10.3 Long-term (3) — tracked

| # | Finding | Expert | Tracking |
|---|---------|--------|----------|
| LT-1 | Server Component conversion del page (FCP/LCP optimization) | Fowler + Newman | §2 out-of-scope. Wave B.4+ candidate. |
| LT-2 | `playCount`/`lastPlayedAt` backend extension | Nygard | §3.4 child issue se Caso B. Wave B.4+ candidate. |
| LT-3 | `MeepleCard` v2 variant extension (cover gradient mockup-accurate) | Fowler | §2 out-of-scope. Spec dedicato MeepleCard v2 se needed. |

## 11. Effort estimate

- **Foundation (commit 1)**: 0.5 day (helpers + i18n + fixture)
- **Component TDD (commit 2)**: 2 days (4 component × 0.5 day each, includes test)
- **Page integration (commit 3)**: 0.5 day
- **E2E specs (commit 4)**: 0.5 day
- **Bootstrap baselines (commit 5)**: 0.5 day (CI roundtrip + verify)
- **PR review + merge**: 0.5-1 day buffer

**Total**: 5-7 days (single-engineer, including review).

## 12. References

- Spec umbrella: [`2026-04-26-v2-design-migration.md`](2026-04-26-v2-design-migration.md) §4
- Phase 1 execution: [`2026-04-27-v2-migration-phase1-execution.md`](2026-04-27-v2-migration-phase1-execution.md) §3.3
- Migration matrix (single source of truth): [`docs/frontend/v2-migration-matrix.md`](../../frontend/v2-migration-matrix.md)
- Bundle size budget guide: [`docs/frontend/bundle-size-budget.md`](../../frontend/bundle-size-budget.md)
- MeepleCard design tokens: [`docs/frontend/meeple-card-design-tokens.md`](../../frontend/meeple-card-design-tokens.md)
- Wave A.3b spec (URL hash pattern reference): [`2026-04-28-v2-migration-wave-a-3b-shared-games-fe.md`](2026-04-28-v2-migration-wave-a-3b-shared-games-fe.md)
- Wave A.4 spec (brownfield + visual fixture sentinel reference): [`2026-04-28-v2-migration-wave-a-4-shared-game-detail.md`](2026-04-28-v2-migration-wave-a-4-shared-game-detail.md)
- `useTablistKeyboardNav` hook: [`apps/web/src/hooks/useTablistKeyboardNav.ts`](../../../apps/web/src/hooks/useTablistKeyboardNav.ts) (PR #623 + orientation extension PR #626)
- Mockup: [`admin-mockups/design_files/sp4-games-index.jsx`](../../../admin-mockups/design_files/sp4-games-index.jsx)
- Existing brownfield page: [`apps/web/src/app/(authenticated)/games/page.tsx`](../../../apps/web/src/app/(authenticated)/games/page.tsx)
