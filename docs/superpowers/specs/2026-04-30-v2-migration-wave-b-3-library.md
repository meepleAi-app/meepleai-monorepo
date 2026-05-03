# Spec: V2 Migration — Wave B.3 — `/library` desktop brownfield

> **Issue**: [#574](https://github.com/meepleAi-app/meepleai-monorepo/issues/574) · **Umbrella**: [#580](https://github.com/meepleAi-app/meepleai-monorepo/issues/580) · **Wave**: B (last child) · **Strategy**: 🔴 Big-Bang replacement (NO feature flag)
>
> **Author**: Claude (sonnet/opus) via `/sc:spec-panel ultrathink continua b3` · **Date**: 2026-04-30
>
> **Predecessors merged**: B.1 PR #635 (`/games?tab=library` 2026-04-30T05:51), B.2 PR #637 (`/agents` 2026-04-30T11:21)
>
> **Mockup**: [`admin-mockups/design_files/sp4-library-desktop.jsx`](../../../admin-mockups/design_files/sp4-library-desktop.jsx) (squash `a670c2c39`, PR #569)

## 1. Goals

1. **Brownfield migration** del route `/library` desktop al design v2 da mockup `sp4-library-desktop`. La pagina corrente è production-live con shell `RequireRole + LibraryMobile + LibraryContent (Suspense)` ([page.tsx:9-22](../../../apps/web/src/app/(authenticated)/library/page.tsx)) e orchestratore carousel-landing `LibraryHub` ([LibraryHub.tsx](../../../apps/web/src/app/(authenticated)/library/LibraryHub.tsx)) — **dev experience attuale = carousel landing**, **target B.3 = hybrid hub con tabs + grid + bulk bar + activity rail**.
2. **Big-Bang replacement** (esplicito in Issue #574 body): NO feature flag, NO branch-by-abstraction shim, rollback = `git revert`. Mirror Fowler PR #549/#552 pattern adattato per assenza flag.
3. **5 v2 components estratti** (mirror Wave B.1/B.2) in `apps/web/src/components/v2/library/*` per allineamento path discipline CLAUDE.md (feature compositions sotto `components/v2/`, NON `components/library/v2/`):
   - `LibraryHeroDesktop` — hero strip con stats + CTA primaria (Aggiungi)
   - `LibraryTabs` — animated underline tabs cross-entity (3 entity nello scope ridotto: `all/kb/loaned`)
   - `LibraryHybridGrid` — grid responsivo + view-mode toggle (grid/list/compact)
   - `BulkSelectionBar` — floating action bar con multi-select + bulk action (rimuovi)
   - `RecentActivityRail` — sidebar verticale 280px con activity list (empty placeholder Phase 1)
4. **MeepleCard reuse mandate** (CLAUDE.md "Card Components"): grid renders MeepleCard `entity="game"|"agent"`, variant `grid|list|compact` per view-mode dispatch. ZERO fork.
5. **useTablistKeyboardNav reuse** (PR #623 + orientation extension PR #626) per `LibraryTabs` keyboard nav (Arrow Left/Right wrap, Home/End jump). Roving tabindex + automatic activation WAI-ARIA APG.
6. **i18n IT+EN ~50 keys** namespace `pages.library.*` (hero, tabs, filters, bulk, activity rail, empty states). ZERO hard-coded strings nei v2 component.
7. **Bundle budget** Δ ≤ +30 KB target / **+50 KB hard limit** (Issue #574 AC esplicito). Più stretto vs B.1 (target +25 KB) per RecentActivityRail sidebar overhead.
8. **5-state FSM** mirror B.1/B.2: `default | loading | empty | filtered-empty | error` con discriminated UI + `?state=...` URL override gated by `IS_VISUAL_TEST_BUILD || NODE_ENV !== 'production'`.

## 2. Non-goals (explicit)

1. **`AdvancedFiltersDrawer`** mockup ha drawer con tags/rating/weight/period filters → backend `GetUserLibraryParams` ([library.schemas.ts:191-203](../../../apps/web/src/lib/api/schemas/library.schemas.ts)) supporta solo `page/pageSize/search/favoritesOnly/stateFilter[]/sortBy/sortDescending` → **defer obbligatorio** (no client-side filter su `tags` perché entry shape non li espone). Inline filters (search + status + sort + view) sufficient per AC.
2. **Cross-entity tabs full set** mockup mostra 6 tabs (all/game/agent/kb/session/chat) → backend è game-only (`GET /api/v1/library` ritorna `UserLibraryEntry[]` con `gameId`-keyed entries), **NO endpoints aggregati per agents/sessions/chat in library context**. Scope ridotto a **3 tabs** (`all/kb/loaned`) sfruttando `hasKb`/`kbCardCount` client-side derivable dalle entry esistenti. Tab `game` droppato (YAGNI: alias di `all` con backend game-only, semantic separation senza valore funzionale). Tab `archived` rinominato `loaned` per allineamento semantico col mapping `currentState='InPrestito'` (vedi §3.3). Tabs `agent/session/chat` defer a Wave B.4+ con backend extension (child issue tracking).
3. **Mobile consolidation** Issue #574 AC esplicito: "mobile esistente non regredisce". Desktop-first, mobile (`LibraryMobile` componente) resta production come oggi. Hybrid hub responsive sotto `lg:block` breakpoint, mobile mantiene branching shell.
4. **Real activity feed** RecentActivityRail mockup mostra event stream — backend NO endpoints (`GET /api/v1/library/activity` non esiste). Phase 1 = empty placeholder con skeleton + copy "Activity feed prossimamente". Backend extension child issue tracking Wave B.4+.
5. **Server Component conversion** del page principale. `page.tsx` resta `'use client'` come oggi. Server fetch optimization out-of-scope.
6. **Light/dark theme parity tests** Issue #574 AC menziona "light+dark mode entrambi testati" → out-of-scope formale per visual baselines (1 viewport × 5 states × 2 themes = 10 PNG aggiuntivi, +complessità bootstrap). Manual smoke check pre-merge sufficient. Theme audit dedicato follow-up.
7. **Backend extension** zero modifiche `apps/api/src/Api/`. Spec è frontend-only.
8. **MeepleCard extension** (variant nuovi, prop nuove). Reuse stretto API esistente.
9. **Preview route `/library/v2`** ([page.tsx](../../../apps/web/src/app/(authenticated)/library/v2/page.tsx) + LibraryV2Client) deprecation: NO redirect, NO removal in B.3. Standalone preview resta come historical reference. Removal pianificato post-Wave B umbrella close.

## 3. Architecture

### 3.1 File map

| Type | Path | Action |
|------|------|--------|
| Page (legacy → rewrite) | `apps/web/src/app/(authenticated)/library/page.tsx` | edit (~33 LOC, **preserve `<Suspense>` wrapper** — required by Next.js 16 App Router per `useSearchParams()` boundary; swap inner child `<LibraryContent>` con `<LibraryHubV2>` keeping mobile `<LibraryMobile>` branch + `lg:block` desktop gate) |
| Page client (legacy) | `apps/web/src/app/(authenticated)/library/_content.tsx` | edit (rimuove `LibraryHub` import, importa `LibraryHubV2`) |
| **Orchestrator (NEW)** | `apps/web/src/app/(authenticated)/library/_components/LibraryHubV2.tsx` | create (~250 LOC) |
| Orchestrator test | `apps/web/src/app/(authenticated)/library/_components/__tests__/LibraryHubV2.test.tsx` | create (~15 test, 5-state FSM × `?state=` override matrix) |
| Component | `apps/web/src/components/v2/library/LibraryHeroDesktop.tsx` | create (~80 LOC) |
| Component | `apps/web/src/components/v2/library/LibraryTabs.tsx` | create (~120 LOC, integra `useTablistKeyboardNav`) |
| Component | `apps/web/src/components/v2/library/LibraryHybridGrid.tsx` | create (~100 LOC) |
| Component | `apps/web/src/components/v2/library/BulkSelectionBar.tsx` | create (~140 LOC, focus trap pattern) |
| Component | `apps/web/src/components/v2/library/RecentActivityRail.tsx` | create (~90 LOC, empty placeholder + skeleton) |
| Component | `apps/web/src/components/v2/library/EmptyLibrary.tsx` | create (~60 LOC, 4 kind: empty/filtered-empty/loading/error) |
| Barrel | `apps/web/src/components/v2/library/index.ts` | create |
| Component tests | `apps/web/src/components/v2/library/__tests__/*.test.tsx` | create (5 file, ~50 test totali) |
| Helpers | `apps/web/src/lib/library/library-filters.ts` | create (pure FSM helpers + entity tab derivation) |
| Helpers test | `apps/web/src/lib/library/__tests__/library-filters.test.ts` | create (~20 test) |
| Visual fixture | `apps/web/src/lib/library/visual-test-fixture.ts` | create (sentinel pattern A.4/B.1/B.2, ~12 entries miste: 6 default + 4 hasKb + 2 `currentState='InPrestito'` per `loaned` tab) |
| i18n IT | `apps/web/src/locales/it.json` | edit (+~50 keys namespace `pages.library`) |
| i18n EN | `apps/web/src/locales/en.json` | edit (+~50 keys namespace `pages.library`) |
| E2E visual | `apps/web/e2e/visual-migrated/sp4-library-desktop.spec.ts` | create |
| E2E states | `apps/web/e2e/v2-states/library.spec.ts` | create (5-state coverage × 2 viewport) |
| E2E a11y | `apps/web/e2e/a11y/library.spec.ts` | create (axe-core WCAG 2.1 AA + reduced-motion) |
| Baselines | `apps/web/e2e/visual-migrated/sp4-library-desktop.spec.ts-snapshots/*-linux.png` | bootstrap (2 PNG: desktop + mobile) |
| Baselines | `apps/web/e2e/v2-states/library.spec.ts-snapshots/*-linux.png` | bootstrap (8 PNG: 4 stati × 2 viewport, error escluso visual mirror B.2) |
| Matrix update | `docs/frontend/v2-migration-matrix.md` | edit (5 row pending → done, PR field) |
| **DELETE legacy** | `apps/web/src/app/(authenticated)/library/LibraryHub.tsx` | delete (carousel-landing orchestrator superato) |
| **DELETE legacy** | `apps/web/src/app/(authenticated)/library/sections/*.tsx` | delete (7 file: CatalogCarouselSection, ContinuePlayingSection, LibraryFilterBar, LibraryHeader, LibraryHubCarousel, PersonalLibrarySection, WishlistCarouselSection) |
| **DELETE Phase-0 stub** | `apps/web/src/components/library/v2/{LibraryDesktop,LibraryFilterTabs,GameDrawerContent}.tsx` | delete (3 stub primitivi non allineati col mockup target) |
| **PRESERVE** | `apps/web/src/components/library/v2/LibraryViewToggle.tsx` | preserve as `lib/library/use-library-view.ts` move (riusa `useLibraryView` hook con localStorage persistence) |
| **PRESERVE** | `apps/web/src/components/library/v2/LibraryMobile.tsx` | preserve unchanged (mobile branch invariato) |
| **PRESERVE** | `apps/web/src/app/(authenticated)/library/AddGameDrawer.tsx` | preserve unchanged (driven by `?action=add` URL param Issue #5168) |

**File totali toccati**: ~37 file ops — 26 create/edit (1 page edit + 1 _content.tsx edit + 1 LibraryHubV2 + 1 orchestrator test + 6 v2 components + 1 barrel + 5 component tests + 1 library-filters helper + 1 library-filters test + 1 visual-test-fixture + 1 use-library-view move + 2 i18n locales + 3 E2E specs + 1 matrix update), 11 delete (1 LibraryHub.tsx + 7 sections/*.tsx + 3 stub library/v2/*.tsx). Plus 10 PNG baselines (2 visual-migrated + 8 v2-states across 2 viewport) bootstrap su CI runner workflow + 2 preserve unchanged (LibraryMobile + AddGameDrawer).

### 3.2 Component API

#### `LibraryHubV2` (orchestrator)

```tsx
export interface LibraryHubV2Props {
  /** Override 5-state FSM for visual-test fixture or e2e specs (gated env). */
  initialState?: 'default' | 'loading' | 'empty' | 'filtered-empty' | 'error';
}
```

**State tree**:
```ts
const [tab, setTab] = useState<LibraryEntityKey>('all'); // all|kb|loaned
const [view, setView] = useLibraryView(); // grid|list|compact (localStorage persist)
const [search, setSearch] = useState(''); // 300ms debounce
const [selectionMode, setSelectionMode] = useState<'browse' | 'select'>('browse');
const [selected, setSelected] = useState<Set<string>>(new Set()); // bulk multi-select

const { data, isLoading, error } = useLibrary({
  page: 1,
  pageSize: 50,
  sortBy: 'addedAt',
  sortDescending: true,
});
```

**Selection mode state machine** (C4):
- `browse` (default) — card click → `onOpenEntry(id)` (drill-into detail, drawer/route).
- `select` — entered via toolbar button "Seleziona" o long-press card. Card click → `onToggleSelect(id)` (additive); aria-pressed overlay visible.
- Transition `browse → select`: explicit user action (no implicit on first select). Bulk bar mounts.
- Transition `select → browse`: Esc keyboard, Annulla button in BulkSelectionBar, o `onClearSelection` resets `selected` + flips mode. Bulk bar unmounts.

Razionale: separa intent navigazione vs intent bulk-action, evita ambiguous click semantica. Mirror `mailbox-style multi-select pattern` (Gmail/Inbox).

**5-state FSM derivation** (pure helper `lib/library/library-filters.ts`):
```ts
export function deriveLibraryUiState(args: {
  isLoading: boolean;
  error: unknown | null;
  totalCount: number;
  filteredCount: number;
  override?: 'default'|'loading'|'empty'|'filtered-empty'|'error';
}): 'default'|'loading'|'empty'|'filtered-empty'|'error' {
  if (args.override) return args.override;
  if (args.isLoading) return 'loading';
  if (args.error) return 'error';
  if (args.totalCount === 0) return 'empty';
  if (args.filteredCount === 0) return 'filtered-empty';
  return 'default';
}
```

**Override gating**:
```ts
const STATE_OVERRIDE_ENABLED =
  process.env.NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED === '1' ||
  process.env.NODE_ENV !== 'production';
```

#### `LibraryHeroDesktop`

```tsx
export interface LibraryHeroStat {
  readonly key: 'totalGames' | 'kbReady' | 'wishlist' | 'loaned';
  readonly value: number;
  readonly labelKey: string; // i18n key
}
export interface LibraryHeroDesktopProps {
  readonly stats: ReadonlyArray<LibraryHeroStat>;
  readonly onAddGame: () => void; // primary CTA → opens AddGameDrawer via ?action=add
  readonly compact?: boolean; // mobile collapse
}
```

Layout: semantic `<header>` con `<dl>` per stats (mirror B.1 GamesHero pattern), CTA primaria `Aggiungi gioco`. 4 stats: Giochi totali / KB indicizzati (sum `kbIndexedCount`) / Wishlist (count `currentState='Wishlist'`) / In prestito (count `currentState='InPrestito'`, vedi §3.3 mapping).

#### `LibraryTabs`

```tsx
export type LibraryEntityKey = 'all' | 'kb' | 'loaned';
export interface LibraryTabConfig {
  readonly key: LibraryEntityKey;
  readonly labelKey: string; // i18n key
  readonly count: number;
}
export interface LibraryTabsProps {
  readonly tabs: ReadonlyArray<LibraryTabConfig>;
  readonly active: LibraryEntityKey;
  readonly onChange: (next: LibraryEntityKey) => void;
}
```

WAI-ARIA APG horizontal tablist con `useTablistKeyboardNav<LibraryEntityKey>({ orderedKeys, onChange })`. Animated underline (CSS transition gated da `prefers-reduced-motion`). Roving tabindex automatic activation pattern.

#### `LibraryHybridGrid`

```tsx
export type LibraryViewMode = 'grid' | 'list' | 'compact';
export type LibrarySelectionMode = 'browse' | 'select';
export interface LibraryHybridGridProps {
  readonly entries: ReadonlyArray<UserLibraryEntry>;
  readonly view: LibraryViewMode;
  readonly selectionMode: LibrarySelectionMode;
  readonly selected: ReadonlySet<string>;
  readonly onCardClick: (entryId: string) => void; // dispatch by selectionMode
  readonly onLongPressEnter?: (entryId: string) => void; // mobile/touch enter select-mode
}
```

**Click dispatch contract** (orchestrator wires):
```ts
const handleCardClick = (id: string) => {
  if (selectionMode === 'select') {
    setSelected(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id); else next.add(id);
      return next;
    });
  } else {
    onOpenEntry(id); // browse mode → drill-into detail
  }
};
```

Mapping entry → MeepleCard:
```tsx
<button
  type="button"
  aria-pressed={selectionMode === 'select' ? selected.has(entry.id) : undefined}
  data-selection-mode={selectionMode}
  onClick={() => onCardClick(entry.id)}
>
  <MeepleCard
    entity="game"
    variant={view === 'compact' ? 'compact' : view === 'list' ? 'list' : 'grid'}
    title={entry.gameTitle}
    subtitle={entry.gamePublisher ?? undefined}
    imageUrl={entry.gameImageUrl ?? entry.gameIconUrl ?? undefined}
    rating={entry.averageRating ?? undefined}
    ratingMax={10}
  />
  {selectionMode === 'select' && selected.has(entry.id) && (
    <span aria-hidden className="absolute right-3 top-3 ..."><CheckIcon /></span>
  )}
</button>
```

Layout: `grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4` (grid mode), `flex flex-col gap-2` (list), `grid grid-cols-2 sm:grid-cols-4 lg:grid-cols-6` (compact).

**Visual states**:
- `selectionMode='browse'` → card hover/click feedback normale, no aria-pressed.
- `selectionMode='select'` → tutte le card mostrano slot per check overlay (vuoto se non selected). Aria-pressed sempre presente (ARIA toggle button pattern). Visual outline subtle on `select` mode active per signaling.

#### `BulkSelectionBar`

```tsx
export interface BulkSelectionBarProps {
  readonly selectedCount: number;
  readonly onExitSelectMode: () => void; // resets selected + flips selectionMode → 'browse'
  readonly onArchive: () => Promise<void>; // post-confirm: calls bulkRemoveFromLibrary
  readonly disabled?: boolean;
}
```

Mounted only when `selectionMode === 'select'` (mounted even if `selectedCount === 0` to provide explicit Annulla affordance and avoid layout flash). Floating bar bottom-center, full-width on mobile.

**Confirm dialog** (Radix `<AlertDialog>` accessible-by-default):
- Trigger: click "Archivia" button.
- Title: `t('bulk.confirmArchive', { count })`.
- Cancel + Confirm buttons. Confirm → `await onArchive()`, close dialog, exit select mode on success.

**Keyboard contract**:
- Esc → `onExitSelectMode()` (close bar + reset selected + flip to `browse`).
- Tab cycle restricted to bar buttons (focus trap via `<FocusScope>` o equivalente).
- Enter su button → trigger button action (no custom handling, native).

ARIA: `role="region" aria-label={t('bulk.label', { count })} aria-live="polite" aria-atomic="true"`. Dialog: Radix sets `role="alertdialog"` automatic.

Slide-in animation gated by `prefers-reduced-motion: reduce` → collapse a 0.01ms (mirror B.2 contract).

#### `RecentActivityRail`

```tsx
export type ActivityKind = 'play' | 'add' | 'kb-indexed' | 'rating-changed';
export interface ActivityItem {
  readonly id: string;
  readonly kind: ActivityKind;
  readonly entityTitle: string;
  readonly timestamp: string; // ISO 8601
}
export interface RecentActivityRailProps {
  readonly items: ReadonlyArray<ActivityItem>; // empty array Phase 1
  readonly isLoading?: boolean;
}
```

**Phase 1 contract**: `items.length === 0` always (no real backend). Empty placeholder con skeleton 3 lines (loading) o copy "Activity feed prossimamente" (empty). Phase 2+ wire-up real `GET /api/v1/library/activity` endpoint (child issue).

Sidebar 280px width desktop (`lg:w-72 lg:block`), nascosto sotto `lg` breakpoint.

#### `EmptyLibrary` (4 kind)

```tsx
export type EmptyLibraryKind = 'empty' | 'filtered-empty' | 'loading' | 'error';
export interface EmptyLibraryProps {
  readonly kind: EmptyLibraryKind;
  readonly onClearFilters?: () => void; // required for filtered-empty
  readonly onRetry?: () => void;        // required for error
  readonly onAddGame?: () => void;      // required for empty
}
```

Discriminated UI mirror B.1 `GamesEmptyState` / B.2 `EmptyAgents`: 8 skeleton cards (mobile=4) per loading, IllustratedEmptyState con CTA per empty (`Aggiungi il primo gioco`), filtered-empty CTA (`Azzera filtri`), error retry button.

### 3.3 Data shape — Status filter derivation + entity tab mapping

**Backend DTO `UserLibraryEntry`** ([source](../../../apps/web/src/lib/api/schemas/library.schemas.ts:32)):
```ts
{
  id: string,
  gameId: string,
  gameTitle: string,
  currentState: 'Nuovo' | 'InPrestito' | 'Wishlist' | 'Owned',
  hasKb: boolean,            // ✅ tab "kb" client-side derivable
  kbCardCount: number,       // ✅ stats hero
  kbIndexedCount: number,    // ✅ stats hero
  averageRating?: number,
  // ❌ NO playCount, NO lastPlayedAt, NO totalSessions (B.1 lesson)
}
```

**Mapping mockup entity tabs → backend** (3-tab scope post-C2/C3):
| Tab key (B.3 scope) | UI label IT/EN | Filter predicate | Backend dependency |
|---|---|---|---|
| `all` | Tutto / All | `() => true` | none |
| `kb` | KB / Knowledge | `e => e.hasKb || e.kbCardCount > 0` | ✅ esistente |
| `loaned` | In prestito / Loaned | `e => e.currentState === 'InPrestito'` | ✅ esistente (`currentState` enum) |

> **Decisione semantica C2 (`archived` → `loaned`)**: backend non ha enum `Archived`. Tab key e i18n label rinominati `loaned` per allineamento diretto col mapping `currentState='InPrestito'` — eliminato gap semantico mockup-vs-backend, no proxy translation layer. Se in futuro il backend introduce enum `Archived`, filed nuovo tab/issue invece di swap silenzioso.
>
> **Decisione semantica C3 (drop `game` tab)**: tab `game` droppato (era alias di `all` nel current scope con backend game-only). YAGNI compliance — semantic separation senza valore funzionale, riapribile in future Wave se backend introduce entity-class diversification.

**Sort mapping** (UI dropdown, mirror B.1):
| Sort key | UI label | Comparator | Notes |
|---|---|---|---|
| `recent` | Recenti / Recent | `addedAt` desc | ✅ default |
| `title` | Titolo A-Z / Title A-Z | `localeCompare('it')` | ✅ |
| `rating` | Rating / Rating | `(b.averageRating ?? 0) - (a.averageRating ?? 0)` | ✅ |
| `state` | Stato / State | order: Owned > Nuovo > Wishlist > InPrestito | ✅ |

**Search**: client-side filter su `gameTitle + gamePublisher` substring case-insensitive. Debounce 300ms trailing-edge (mirror B.1 GamesFiltersInline).

### 3.4 Backend dependencies & verification (BLOCKER pre-impl)

**Pre-implementation verification step (BLOCKING for impl commit 1)**:

```bash
# Backend running (make dev-core)
curl -s "http://localhost:8080/api/v1/library?page=1&pageSize=5" \
  -H "Cookie: $(cat ~/.meepleai/dev-session-cookie)" | jq '.items[0] | {currentState, hasKb, kbCardCount, kbIndexedCount}'

# Atteso (current): currentState in {Nuovo,InPrestito,Wishlist,Owned}, hasKb boolean, kbCardCount/kbIndexedCount integer
```

**Branching decision tree**:
- **Caso A — backend HA exact field shape**: usare diretto, no action.
- **Caso B — `hasKb` o `kbCardCount` mancante** (improbabile, già usato in `LibraryHub.tsx:38` esistente):
  - Hide tab "kb" da `LibraryTabs.tabs`. AC-2 ridotto a 2 tabs (`all`/`loaned`).
  - Filed child issue tracking opzione backend extension.
- **Caso C — futuro enum `currentState='Archived'`** existing already:
  - File nuovo issue per aggiungere tab `archived` separato (non swap silenzioso del predicate `loaned`). Mantiene semantic clarity tra "loaned out" (temporary) e "archived" (permanent removal).

**Verification gate**: PRIMA di iniziare impl (commit 1), eseguire curl sopra. Documentare risultato in PR description sezione "Backend verification". Se Caso B, applicare hide tab + creare child issue.

### 3.5 i18n keys (`pages.library.*`)

Estratto preview (full ~50 keys in commit 1):

```jsonc
{
  "pages": {
    "library": {
      "title": "La tua libreria",
      "subtitle": "Esplora, filtra e gestisci i tuoi giochi e le knowledge base.",
      "hero": {
        "stats": {
          "totalGames": "Giochi",
          "kbReady": "KB pronti",
          "wishlist": "Wishlist",
          "loaned": "In prestito"
        },
        "ctaAdd": "Aggiungi gioco"
      },
      "tabs": {
        "all": "Tutto",
        "kb": "Knowledge",
        "loaned": "In prestito"
      },
      "selectionMode": {
        "enter": "Seleziona",
        "exit": "Annulla",
        "ariaActive": "Modalità selezione attiva, premi Esc per uscire"
      },
      "filters": {
        "searchPlaceholder": "Cerca per titolo, autore…",
        "searchClear": "Pulisci",
        "sortLabel": "Ordina",
        "sort": {
          "recent": "Recenti",
          "title": "Titolo A-Z",
          "rating": "Rating",
          "state": "Stato"
        },
        "viewLabel": "Vista",
        "view": { "grid": "Griglia", "list": "Lista", "compact": "Compatta" },
        "resultCount": "{{count}} elementi"
      },
      "bulk": {
        "label": "{{count}} selezionati",
        "actionArchive": "Archivia",
        "actionClear": "Azzera selezione",
        "confirmArchive": "Confermi rimozione di {{count}} giochi dalla libreria?"
      },
      "activityRail": {
        "title": "Attività recente",
        "empty": "Activity feed prossimamente.",
        "loading": "Caricamento attività…"
      },
      "emptyState": {
        "empty": {
          "title": "La tua libreria è vuota",
          "subtitle": "Aggiungi il primo gioco per iniziare a tracciare partite, caricare regolamenti e creare agenti AI esperti.",
          "cta": "Aggiungi un gioco"
        },
        "filteredEmpty": {
          "title": "Nessun risultato",
          "subtitle": "Nessun elemento corrisponde ai filtri attuali. Prova a modificare la ricerca o azzera i filtri.",
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
```

EN mirror in `apps/web/src/locales/en.json`. Total ~50 keys × 2 locales.

### 3.6 Server/client split

`/library/page.tsx` resta `'use client'` come oggi (existing brownfield). NO server component split per B.3 — out-of-scope.

`LibraryHubV2` is client-side, riceve `searchParams` via `useSearchParams()` per `?state=` override.

**Reasoning**: stessa motivazione B.1 — splittare il page in server+client richiederebbe estendere lo scope. Server fetch è candidate per Wave B.4+ ottimizzazione.

### 3.7 Bundle budget (Wiegers + Nygard)

**Target**: Δ +20-30 KB First Load JS per `/library` rispetto a baseline pre-PR.
**Hard limit**: +50 KB (gate `frontend-bundle-size` PR fail) — Issue #574 AC esplicito.

**Strategia**:
- Riuso `MeepleCard` v1 (zero duplicate). Riuso `useTablistKeyboardNav` + `useLibraryView` hook (zero new lib).
- 5 v2 component nuovi inline-sized (no external deps). Stima: ~12 KB gzipped.
- i18n keys: ~50 × 2 locales ≈ 2.5 KB ciascuno.
- Skeleton + EmptyState reuse `Skeleton` v1.
- Delete legacy `LibraryHub.tsx` + 7 `sections/*.tsx` + 3 stub `library/v2/*.tsx` = **net negative ~8-10 KB before adding new components**.

**Mitigazione overflow**:
- Se >35 KB: code-split `RecentActivityRail` con `next/dynamic` (sidebar lazy, primary content non-blocking).
- Se >40 KB: code-split `BulkSelectionBar` (mounted only when `selectedCount > 0`).
- Se >50 KB: investigate MeepleCard re-tree-shake — escalation (block PR).

## 4. Test plan

### 4.1 Visual baselines (Playwright `visual-migrated` + `v2-states`)

| Spec | Coverage | PNG count | Viewport |
|------|----------|-----------|----------|
| `visual-migrated/sp4-library-desktop.spec.ts` | Default state pixel-match con mockup baseline | 2 | desktop 1440 + mobile 375 |
| `v2-states/library.spec.ts` | 4 stati FSM (default/loading/empty/filtered-empty); error escluso visual (mirror B.2 — `error` deriva da `useLibrary().error` non riproducibile via URL override, coperto unit-side) | 8 | desktop 1440 + mobile 375 |

**Bootstrap workflow**: `visual-regression-migrated.yml` mirror Wave B.1/B.2:
1. Set env `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1`
2. Set env `NEXT_PUBLIC_LOCALE=it` (default)
3. `pnpm build`
4. Run Playwright → genera baselines via `--update-snapshots` su CI runner Linux
5. Upload artifact `visual-migrated-baselines` per inspect locale
6. PR review owner downloads artifact, copia PNG in `*.spec.ts-snapshots/`, commit `chore(visual): bootstrap canonical baselines for /library`

**Auth bypass triple-helper** (mirror Wave B.2):
```ts
await seedAuthSession(page);
await seedCookieConsent(page);
await mockAuthEndpoints(page);
await page.goto('/library', { waitUntil: 'domcontentloaded' });
```

`PLAYWRIGHT_AUTH_BYPASS=true` settato dal webServer di `playwright.config.ts:434`. Triple-helper soddisfa client-side gates senza DB session reale.

### 4.2 Unit tests (Vitest)

| File | Coverage |
|------|----------|
| `lib/library/__tests__/library-filters.test.ts` | Pure helpers: `filterByEntity(entries, key)` × 3 tabs (`all/kb/loaned`), `filterBySearch(entries, q)` substring match, `sortLibraryEntries(entries, sortKey)` × 4 sort, `deriveLibraryUiState(args)` × 5 stati × override matrix, `deriveStats(entries)` cardinality (4 stats: totalGames/kbReady/wishlist/loaned) + edge case empty. ~20 test |
| `components/v2/library/__tests__/LibraryHeroDesktop.test.tsx` | Render con stats array (4 stats: totalGames/kbReady/wishlist/loaned), CTA onAddGame fired, compact prop layout switch. ~6 test |
| `components/v2/library/__tests__/LibraryTabs.test.tsx` | Render tabs (3 entity: `all/kb/loaned`), active highlight, `useTablistKeyboardNav` integration (Arrow/Home/End wrap), animated underline class presence, roving tabindex. ~12 test |
| `components/v2/library/__tests__/LibraryHybridGrid.test.tsx` | Mapping entries → MeepleCard props × view-mode dispatch (grid/list/compact), `selectionMode='browse'` card click → onCardClick(id) invoked once (open intent), `selectionMode='select'` card click → onCardClick(id) invoked + aria-pressed toggle + check overlay mount, empty entries no render. ~12 test |
| `components/v2/library/__tests__/BulkSelectionBar.test.tsx` | Mounted iff `selectionMode='select'` (mounted at count=0), state machine entry/exit (`onExitSelectMode` flips browse + clears Set), count badge render, "Archivia" → Radix `<AlertDialog>` opens with i18n `confirmArchive` count, Confirm → `await onArchive()` resolves + dialog closes, Cancel → dialog closes no-op, Esc → `onExitSelectMode`, focus trap Tab cycle, prefers-reduced-motion 0.01ms transitions. ~10 test |
| `components/v2/library/__tests__/RecentActivityRail.test.tsx` | Empty state copy render, isLoading skeleton 3 lines render, items.length>0 (Phase 2 readiness) renders list item by kind. ~5 test |
| `app/(authenticated)/library/_components/__tests__/LibraryHubV2.test.tsx` | FSM derivation: 5 stati × override matrix; useLibrary mock; `?state=` URL override env-gated; clear filters resets q+tab; selectionMode browse↔select state machine transitions; bulk select cross-tab preserve. ~17 test |

**Total target**: ~82 unit tests, all green pre-PR.

### 4.3 A11y test (Playwright + axe-core)

`apps/web/e2e/a11y/library.spec.ts`:

```ts
test('default state passes axe-core scan WCAG 2.1 AA', async ({ page }) => {
  await seedAuthSession(page);
  await seedCookieConsent(page);
  await mockAuthEndpoints(page);
  await page.goto('/library', { waitUntil: 'domcontentloaded' });
  await page.waitForSelector('[data-slot="library-hub-v2"]', { timeout: 30_000 });
  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
    // EntityBadge color-contrast pre-existing debt MeepleCard family — Issue #636
    .exclude('[data-slot="meeple-card-entity-badge"]')
    // StatusBadge color-contrast pre-existing debt — Issue #636 (extended B.2)
    .exclude('[data-slot="meeple-card-status-badge"]')
    .analyze();
  expect(results.violations).toEqual([]);
});

test('filtered-empty state passes axe-core scan', /* idem ?state=filtered-empty */ });
test('bulk selection bar focus trap + aria-live region', /* keyboard trap test */ });
test('prefers-reduced-motion: tab underline + bulk bar slide collapse to sub-ms', /* mirror B.2 transition-duration assert */ });
```

CI gate `frontend-a11y` (PR #602) **MUST PASS** prima merge.

### 4.4 Behavior scenarios (Given/When/Then) — pre-impl reference

I 10 GWT scenarios per AC coverage:

1. **Tab switch entity preservation** — Given user con search query attiva (`selectionMode='browse'`), When user clicca tab `kb`, Then la search query resta + grid filtra by `hasKb` predicate
2. **Enter select mode via toolbar (C4)** — Given `selectionMode='browse'` con grid popolata, When user clicca toolbar button "Seleziona", Then `selectionMode` flip a `'select'` + BulkSelectionBar mounted (count=0) + ogni MeepleCard wrapped in button con `aria-pressed='false'`
3. **Card click dispatch contract (C4)** — Given `selectionMode='browse'`, When user clicca card, Then `onOpenEntry(entryId)` invoked (navigate al detail). Given `selectionMode='select'`, When user clicca stessa card, Then `onToggleSelect(entryId)` invoked + `aria-pressed` toggle + check overlay mount
4. **Exit select mode via Esc (C4)** — Given `selectionMode='select'` con N selezionati, When user preme Esc su BulkSelectionBar focused, Then `onExitSelectMode()` flip a `'browse'` + `selected` cleared + bar unmounts (animato sub-ms se reduced-motion)
5. **Bulk archive flow con confirm (C4)** — Given 3 entries selezionate in select-mode, When user clicca "Archivia", Then Radix `<AlertDialog>` opens con count `t('bulk.confirmArchive', { count: 3 })`, When user clicca Confirm, Then `await onArchive()` resolves + dialog closes + selection cleared + flip a `'browse'`
6. **Bulk select cross-tab restrictive** — Given 5 game entries selezionate (mode select), When user clicca tab `kb`, Then BulkSelectionBar mostra count restricted alle entries kb-eligible visible (filter applies post-select, selected Set preserved internally)
7. **RecentActivityRail empty** — Given Phase 1 (no backend), Then rail mostra placeholder copy + skeleton inactive + sidebar visible solo desktop
8. **Drawer non-applicable hide** — Given mockup ha `AdvancedFiltersDrawer` button, When B.3 ship, Then drawer non rendered (out-of-scope §2 explicit)
9. **prefers-reduced-motion** — Given user con `prefers-reduced-motion: reduce`, When tab change o BulkSelectionBar mount/unmount, Then transitions collapse <50ms (mirror B.2 contract)
10. **Mobile non-regression** — Given viewport 375px, When goto `/library`, Then `LibraryMobile` legacy renders unchanged (NO `LibraryHubV2` mounted) — preserve `lg:hidden` branch

## 5. Acceptance criteria (13 SMART)

> **Convenzione**: AC-N · `<criterio testabile>` · evidence: `<test/snapshot/check>`

- **AC-1** · `/library` desktop renderá mockup `sp4-library-desktop` pixel-match desktop 1440 + mobile 375. · Evidence: 2 PNG `visual-migrated/sp4-library-desktop.spec.ts-snapshots/*-linux.png` PASS in CI.
- **AC-2** · `LibraryTabs` rispetta scope ridotto B.3 esattamente **3 entity** (`all/kb/loaned`). Tab `game` droppato (YAGNI, alias di `all`). Tab `archived` rinominato `loaned` (semantic alignment col mapping `currentState='InPrestito'`). Mockup tabs `agent/session/chat` defer a Wave B.4+ assenti dal render. · Evidence: `LibraryTabs.test.tsx` snapshot `tabs.length === 3` + i18n key match.
- **AC-3** · Le card riusano `MeepleCard` v1 con `entity="game"` e `variant="grid"|"list"|"compact"` (3 view-mode dispatch). ZERO fork. Card click dispatch state-machine-aware: `selectionMode='browse'` → `onOpenEntry(entryId)`, `selectionMode='select'` → `onToggleSelect(entryId)` con `aria-pressed` toggle + check overlay. · Evidence: grep `MeepleCard.*entity.*game` in `LibraryHybridGrid.tsx` matcha; assenza `// fork` o `MeepleCard.v2` import; `LibraryHybridGrid.test.tsx` covers entrambi i dispatch path.
- **AC-4** · `LibraryTabs` supporta WAI-ARIA APG keyboard nav: Arrow Left/Right (wrap), Home/End. Roving tabindex automatic activation. Reuse `useTablistKeyboardNav` (PR #623). · Evidence: `LibraryTabs.test.tsx` 12 keyboard scenarios + manual Tab→Arrow nav green.
- **AC-5** · 5 stati FSM (default/loading/empty/filtered-empty/error) renderano correttamente con discriminated UI. Override `?state=` env-gated. · Evidence: 8 PNG `v2-states/library.spec.ts-snapshots/*-linux.png` PASS + `LibraryHubV2.test.tsx` FSM coverage 100%.
- **AC-6** · `BulkSelectionBar` floating bar mounted iff `selectionMode === 'select'` (state machine, not selectedCount-conditional). Entry via toolbar button "Seleziona" → `setSelectionMode('select')`. Exit via Esc o "Annulla" → `onExitSelectMode()` flip a `'browse'` + clear `selected` Set. Confirm "Archivia" via Radix `<AlertDialog>` accessible-by-default. Focus trap Tab cycle within bar (Radix `<FocusScope>`). ARIA `role="region" aria-live="polite" aria-atomic="true"`. · Evidence: `BulkSelectionBar.test.tsx` focus-trap + state machine entry/exit + Radix AlertDialog interaction + 10 test green.
- **AC-7** · `RecentActivityRail` Phase 1 placeholder: `items.length === 0` always, copy "Activity feed prossimamente" + sidebar 280px hidden sotto `lg`. · Evidence: `RecentActivityRail.test.tsx` 5 test + visual snapshot.
- **AC-8** · `prefers-reduced-motion: reduce` collapse `LibraryTabs` animated underline + `BulkSelectionBar` slide-in transitions a sub-50ms. · Evidence: `e2e/a11y/library.spec.ts` `emulateMedia({ reducedMotion: 'reduce' })` + computed style assert (mirror B.2 reduced-motion contract).
- **AC-9** · `frontend-a11y` axe-core scan WCAG 2.1 AA: 0 violations su default + filtered-empty + bulk-active state. EntityBadge/StatusBadge exclusion ereditata da B.1+B.2 (#636 deferred). · Evidence: CI gate PASS.
- **AC-10** · `frontend-bundle-size` gate: Δ First Load JS per `/library` ≤ +50 KB hard limit (target +30 KB). · Evidence: CI gate PASS, report nel PR comment.
- **AC-11** · i18n bilingue IT+EN: 0 stringhe hard-coded nei 5 v2 component + orchestrator. Switch locale rerender label. · Evidence: grep `'Tutto'\|'Knowledge'\|'In prestito'\|'La tua libreria'\|'Seleziona'\|'Annulla'\|'Archivia'` in `components/v2/library/*.tsx` ritorna 0 match (tutto da `useTranslation()`).
- **AC-12** · Test pyramid full green pre-merge: ~82 unit (Vitest) + 6 visual snapshots (10 PNG totali) + 4 a11y E2E. · Evidence: PR CI summary tutti green.
- **AC-13** · Mobile (`LibraryMobile`) NON regredisce post Big-Bang. Manual smoke: `lg:hidden` branch renders v1 layout unchanged. Sub-routes `/library/wishlist`, `/library/[gameId]`, `/library/private`, `/library/playlists`, `/library/proposals`, `/library/propose` invariate. · Evidence: visit manual + esistenti unit test passano + `pnpm test:e2e e2e/visual-mockups/sp4-shared-game-detail.spec.ts` (Wave A.4) green.

## 6. Risks & mitigations

| ID | Risk | Probability | Impact | Mitigation |
|----|------|-------------|--------|-----------|
| R1 | Big-Bang rollback latency: post-merge user vede UX cambiata immediatamente, rollback richiede revert PR + re-deploy ciclo (~30min) | Low | High | Pre-merge rehearsal su staging via `make staging` (Issue #574 implicit). Hotfix branch ready. Rollback drill documentato in PR description. |
| R2 | Mobile (`LibraryMobile`) regressione AT impercettibile post Big-Bang | Medium | Medium | AC-13 manual smoke + esistenti `LibraryMobile.test.tsx` regression. Visual baseline `v2-states/library.spec.ts` mobile 375 viewport copre LibraryHubV2 ma `LibraryMobile` resta v1 — separate spec se serve. |
| R3 | Bundle creep > 50 KB hard: 5 component vs 4 di B.1, RecentActivityRail sidebar overhead | Medium | High | §3.7 mitigazione triple: code-split RecentActivityRail → BulkSelectionBar → MeepleCard re-tree-shake escalation. Bundle gate fail blocks PR. Net negative ~8 KB pre-component-add via legacy delete (LibraryHub + sections/*) compensa. |
| R4 | BulkSelectionBar focus trap edge case: Tab+Shift cycling, Esc when no items selected, contemporaneous mouse+keyboard interaction | Medium | Medium | `BulkSelectionBar.test.tsx` 8 test focus-trap + `act(() => keyEvent)` simulation. Manual a11y audit pre-merge via `axe DevTools`. |
| R5 | `AdvancedFiltersDrawer` deferral UX gap: utenti aspettano filter avanzati dal mockup | Low | Low | §2 explicit. Mockup-vs-impl divergence documented in PR description. Child issue tracking Wave B.4+ con backend extension. |
| R6 | `RecentActivityRail` empty UX impact: sidebar vuota desktop = wasted real estate | Low | Low | Empty placeholder copy + skeleton intentional. Phase 2 wire-up plannificato. Sidebar hidden sotto `lg` minimizza desktop-only impact. |
| R7 | Cross-entity scope expectation gap: mockup ha 6 tabs, ship ha 3 tabs (drop game alias YAGNI, defer agent/session/chat) | Medium | Medium | AC-2 esplicito. PR description "Out-of-scope tab `agent/session/chat`" + child issue Wave B.4+. Mockup divergence documented. |
| R8 | Tab `loaned` UX non-obvious: utenti aspettano "Archiviati" leggendo mockup | Low | Low | §3.3 decisione semantica C2 documented. i18n label "In prestito" allineato `currentState='InPrestito'` enum (semantic precision over mockup-loyalty). Se backend introduce enum `Archived`, file nuovo issue (AC-2 NO swap silenzioso). |
| R9 | Preview route `/library/v2` deprecation surprise (utenti che bookmarkavano preview vedono 404 dopo umbrella close) | Very Low | Low | §2 explicit: NO removal in B.3. Standalone preview resta. Removal pianificato post-Wave B umbrella close con redirect. |
| R10 | EntityBadge/StatusBadge debt #636 cascade: a11y exclusion ereditata B.1+B.2, possibili nuovi badge sites scoperti | Low | Low | exclusion chain in `e2e/a11y/library.spec.ts` mirror B.2 + `data-slot` semantic naming. Issue #636 esteso pattern (Wave B.2) per future scope expansion. Token fix separate planning. |

## 7. Out of scope (explicit)

- ❌ `AdvancedFiltersDrawer` (BLOCKER #1 deferred, Option B)
- ❌ Tabs `agent/session/chat` cross-entity (defer Wave B.4+, backend extension required)
- ❌ Real activity feed / events stream (RecentActivityRail empty placeholder Phase 1)
- ❌ Mobile consolidation (`LibraryMobile` resta production unchanged)
- ❌ Light/dark theme parity formal tests (manual smoke only)
- ❌ Server Component conversion del page principale
- ❌ Backend extension (zero modifiche `apps/api/src/Api/`)
- ❌ `MeepleCard` extension (variant nuovi, prop nuove)
- ❌ Preview route `/library/v2` removal/redirect
- ❌ Sub-routes `/library/{wishlist,playlists,private,proposals,propose,[gameId]}` migration
- ❌ Token fix EntityBadge/StatusBadge (#636 deferred, separate planning)

## 8. Sequencing — 5 commits TDD

### Commit 1 — Foundation: helpers + i18n + visual fixture

**Files**:
- `apps/web/src/lib/library/library-filters.ts` (pure helpers)
- `apps/web/src/lib/library/__tests__/library-filters.test.ts` (~20 test, all green)
- `apps/web/src/lib/library/visual-test-fixture.ts` (sentinel pattern A.4/B.1/B.2, ~12 entries miste game+kb+loaned `currentState='InPrestito'`)
- `apps/web/src/lib/library/use-library-view.ts` (move `useLibraryView` from `components/library/v2/LibraryViewToggle.tsx`)
- `apps/web/src/locales/it.json` (~50 keys aggiunte)
- `apps/web/src/locales/en.json` (~50 keys aggiunte)

**Validation**:
- `pnpm test apps/web/src/lib/library/__tests__/library-filters.test.ts` → 20/20 green
- `pnpm typecheck` → 0 errors
- **Pre-flight backend verification (§3.4)**: documentare risultato in PR description

**Commit message**: `feat(library): foundation helpers + i18n keys for /library v2 (#574)`

### Commit 2 — Component family TDD

**Files**:
- `apps/web/src/components/v2/library/LibraryHeroDesktop.tsx` (impl)
- `apps/web/src/components/v2/library/LibraryTabs.tsx` (impl con `useTablistKeyboardNav`)
- `apps/web/src/components/v2/library/LibraryHybridGrid.tsx` (impl)
- `apps/web/src/components/v2/library/BulkSelectionBar.tsx` (impl)
- `apps/web/src/components/v2/library/RecentActivityRail.tsx` (impl)
- `apps/web/src/components/v2/library/EmptyLibrary.tsx` (impl)
- `apps/web/src/components/v2/library/index.ts` (barrel)
- `apps/web/src/components/v2/library/__tests__/*.test.tsx` (5 file, ~50 test totali)

**Order TDD per ogni component**: red → green → refactor.

**Validation**:
- `pnpm test apps/web/src/components/v2/library` → ~50/50 green
- `pnpm typecheck` → 0 errors
- `pnpm lint apps/web/src/components/v2/library` → 0 errors

**Commit message**: `feat(library): 5 v2 components (Hero/Tabs/Grid/BulkBar/Rail) + EmptyLibrary (#574)`

### Commit 3 — Page integration: LibraryHubV2 orchestrator + Big-Bang delete

**Files**:
- `apps/web/src/app/(authenticated)/library/_components/LibraryHubV2.tsx` (impl orchestrator ~250 LOC)
- `apps/web/src/app/(authenticated)/library/_components/__tests__/LibraryHubV2.test.tsx` (~15 test)
- `apps/web/src/app/(authenticated)/library/page.tsx` (edit: **preserve Suspense boundary**, swap inner `<LibraryContent>` con `<LibraryHubV2>` — `<LibraryMobile>` + `lg:block` gate invariati)
- `apps/web/src/app/(authenticated)/library/_content.tsx` (edit: rimuove `LibraryHub` import)
- **DELETE**: `apps/web/src/app/(authenticated)/library/LibraryHub.tsx`
- **DELETE**: `apps/web/src/app/(authenticated)/library/sections/{CatalogCarouselSection,ContinuePlayingSection,LibraryFilterBar,LibraryHeader,LibraryHubCarousel,PersonalLibrarySection,WishlistCarouselSection}.tsx`
- **DELETE**: `apps/web/src/components/library/v2/{LibraryDesktop,LibraryFilterTabs,GameDrawerContent}.tsx` (3 stub primitivi)
- **DELETE**: `apps/web/src/components/library/v2/LibraryViewToggle.tsx` (moved to `lib/library/use-library-view.ts` in commit 1)

**Validation**:
- `pnpm test apps/web/src/app/(authenticated)/library` → ~15/15 green
- Manual smoke: `pnpm dev` → `/library` desktop mostra v2 hub layout, mobile (375px) mostra `LibraryMobile` v1 invariato
- `pnpm typecheck` → 0 errors
- Sub-routes smoke: `/library/wishlist`, `/library/[gameId]`, `/library/private` accessibili

**Commit message**: `feat(library): LibraryHubV2 orchestrator + Big-Bang legacy delete (#574)`

### Commit 4 — Visual + state + a11y E2E specs (NO baselines yet)

**Files**:
- `apps/web/e2e/visual-migrated/sp4-library-desktop.spec.ts`
- `apps/web/e2e/v2-states/library.spec.ts`
- `apps/web/e2e/a11y/library.spec.ts`

**Validation**:
- `pnpm test:e2e apps/web/e2e/a11y/library.spec.ts` → 4 axe scans + reduced-motion green
- Visual specs FAIL su CI prima bootstrap (no baseline) — atteso, fix in commit 5

**Commit message**: `test(library): visual-migrated + v2-states + a11y E2E for /library v2 (#574)`

### Commit 5 — Bootstrap canonical baselines + matrix update

**Workflow CI**: trigger `visual-regression-migrated.yml` con `--update-snapshots` su questo branch.

**Steps**:
1. Workflow run su branch genera artifact `visual-migrated-baselines-<run-id>` (10 PNG: 2 visual-migrated + 8 v2-states)
2. Download artifact local, copia PNG in:
   - `apps/web/e2e/visual-migrated/sp4-library-desktop.spec.ts-snapshots/`
   - `apps/web/e2e/v2-states/library.spec.ts-snapshots/`
3. Filter copy: NON sovrascrivere baselines di altre route (mirror pattern A.3b/A.4/B.1/B.2)
4. Update `docs/frontend/v2-migration-matrix.md`: rows LibraryHeroDesktop/LibraryTabs/LibraryHybridGrid/BulkSelectionBar/RecentActivityRail `Status: pending → done`, `PR: TBD → #<n>`. Add nota deferral per drawer (mockup `AdvancedFiltersDrawer` se row exists) + tabs `agent/session/chat`.

**Validation**:
- Re-run CI workflow su branch: visual specs green, no baseline missing
- All gates green: `frontend-a11y` ✅, `frontend-bundle-size` ✅, `Migrated Routes Baseline` ✅

**Commit message**: `chore(visual): bootstrap canonical baselines for /library v2 (#574)`

### Final — PR creation

**Title**: `feat(library): migrate /library desktop to v2 design (#574)`
**Body**: include
- `Closes #574` + `Refs #580`
- Backend verification result (§3.4)
- Bundle delta report (target +30 KB / hard +50 KB)
- Spec link `docs/superpowers/specs/2026-04-30-v2-migration-wave-b-3-library.md`
- Test summary (~82 unit + 10 visual + 4 a11y)
- Out-of-scope explicit (drawer deferred, tabs agent/session/chat deferred, mobile preserved, real activity feed deferred)
- Big-Bang rollback drill rehearsal note (staging smoke)

**Base branch**: `main-dev` (per CLAUDE.md PR Target Rule, parent branch).
**Merge strategy**: squash. Mirror Wave B.1/B.2 pattern — `--admin` flag NON necessario se codecov/patch sustained CLEAN (Wave B trend).

## 9. Bootstrap baseline workflow — recap

```bash
# Step 1: Push branch with E2E specs (commit 4)
git push -u origin feature/issue-574-library-fe-v2

# Step 2: Trigger workflow with --update-snapshots
gh workflow run visual-regression-migrated.yml \
  --ref feature/issue-574-library-fe-v2 \
  -f update_snapshots=true

# Step 3: Wait + download artifact
gh run watch <run-id>
gh run download <run-id> -n visual-migrated-baselines-<run-id> -D ./tmp-baselines

# Step 4: Copy filtered PNG (only for /library, not other routes)
cp tmp-baselines/visual-migrated/sp4-library-desktop.spec.ts-snapshots/*.png \
   apps/web/e2e/visual-migrated/sp4-library-desktop.spec.ts-snapshots/
cp tmp-baselines/v2-states/library.spec.ts-snapshots/*.png \
   apps/web/e2e/v2-states/library.spec.ts-snapshots/

# Step 5: Cleanup + commit
rm -rf tmp-baselines
git add apps/web/e2e/visual-migrated/sp4-library-desktop.spec.ts-snapshots/ \
        apps/web/e2e/v2-states/library.spec.ts-snapshots/ \
        docs/frontend/v2-migration-matrix.md
git commit -m "chore(visual): bootstrap canonical baselines for /library v2 (#574)"
git push

# Step 6: Re-run workflow → all green
gh workflow run visual-regression-migrated.yml --ref feature/issue-574-library-fe-v2
```

## 10. Spec-panel review — applied findings

### 10.1 BLOCKERs (5) — resolved

| # | Finding | Expert | Resolution |
|---|---------|--------|-----------|
| BLOCKER #1 | Component scope ambiguity: mockup top-comment dice "7", matrix canonical lista "5" | Wiegers | **Decisione: 5 components canonical da matrix** (`LibraryHeroDesktop/LibraryTabs/LibraryHybridGrid/BulkSelectionBar/RecentActivityRail`). EmptyLibrary è helper interno (non in matrix). Spec §3.1 + AC-2 esplicitano. |
| BLOCKER #2 | Pre-existing scaffolding `library/v2/*` (5 stub Phase 0 PR #573) vs greenfield `components/v2/library/*` | Fowler | **Decisione: greenfield in `components/v2/library/*`** allineato CLAUDE.md path discipline + Wave B.1/B.2 pattern. DELETE 3 stub legacy non-allineati col mockup target. PRESERVE+MOVE `useLibraryView` hook a `lib/library/`. Spec §3.1 + Big-Bang scope. |
| BLOCKER #3 | `AdvancedFiltersDrawer` ship-vs-defer: mockup ha drawer, backend `GetUserLibraryParams` no support tags/rating/weight/period | Wiegers + Adzic + Cockburn | **Decisione: defer mandatorio** (no backend, client-side filter su entry shape impossibile per fields mancanti). Spec §2 + AC NOT covering drawer + child issue tracking Wave B.4+. |
| BLOCKER #4 | Cross-entity tabs scope: mockup 6 (all/game/agent/kb/session/chat) vs backend game-only | Adzic + Nygard | **Decisione: 3 tabs scope ridotto** (`all/kb/loaned`) sfruttando `hasKb`/`kbCardCount` client-side derivable. Tab `game` droppato (YAGNI: alias di `all`). Tab `archived` rinominato `loaned` (semantic alignment col mapping `currentState='InPrestito'`). Tabs `agent/session/chat` deferred Wave B.4+. AC-2 esplicito. |
| BLOCKER #5 | RecentActivityRail data source: mockup mostra activity feed, no backend endpoint `GET /api/v1/library/activity` | Nygard + Hightower | **Decisione: empty placeholder Phase 1** con skeleton + copy "Activity feed prossimamente". Phase 2+ backend extension child issue. AC-7 + spec §3.2. |

### 10.2 Short-term (8) — applied

| # | Finding | Expert | Resolution |
|---|---------|--------|-----------|
| ST-1 | A11y `LibraryTabs` WAI-ARIA APG | Crispin | AC-4 + `useTablistKeyboardNav` mandate (PR #623+#626) + a11y E2E AC-9 |
| ST-2 | Search debounce timing not specified | Adzic | §3.2 LibraryHubV2 search 300ms trailing-edge + AC test fake timers |
| ST-3 | prefers-reduced-motion compliance per `LibraryTabs` underline + `BulkSelectionBar` slide | Crispin | AC-8 + e2e a11y `emulateMedia` (mirror B.2 reduced-motion contract) |
| ST-4 | Bundle budget hard limit Issue #574 explicit | Nygard | §3.7 + AC-10 + mitigazione triple code-split (RecentActivityRail → BulkSelectionBar → MeepleCard tree-shake) |
| ST-5 | i18n hard-coded strings risk per 5 component | Doumont | AC-11 grep-based assertion |
| ST-6 | Visual flake mitigation cookie banner | Crispin | §4.1 triple-helper auth bypass + R10 EntityBadge/StatusBadge inheritance B.1+B.2 |
| ST-7 | BulkSelectionBar focus trap | Crispin | AC-6 + `BulkSelectionBar.test.tsx` 8 test focus-trap |
| ST-8 | Big-Bang rollback drill | Nygard + Hightower | R1 mitigazione + PR description "Rollback drill rehearsal" note |

### 10.3 Long-term (4) — tracked

| # | Finding | Expert | Tracking |
|---|---------|--------|----------|
| LT-1 | Server Component conversion del page (FCP/LCP optimization) | Fowler + Newman | §2 out-of-scope. Wave B.4+ candidate. |
| LT-2 | Backend extension `playCount`/`lastPlayedAt`/`activity-stream` | Nygard | §3.4 + §3.2 RecentActivityRail Phase 2 child issue se needed. Wave B.4+ candidate. |
| LT-3 | `MeepleCard` v2 variant extension (cover gradient mockup-accurate) | Fowler | §2 out-of-scope. Spec dedicato MeepleCard v2 se needed. |
| LT-4 | Mobile `LibraryHubV2` parity (consolidation) | Cockburn + Hightower | §2 out-of-scope esplicito Issue #574 AC. Phase 2+ con responsive orchestrator unico. |

## 11. Effort estimate

- **Foundation (commit 1)**: 0.5 day (helpers + i18n + fixture + useLibraryView move)
- **Component TDD (commit 2)**: 2.5 days (5 component × 0.5 day each, includes test, BulkSelectionBar focus trap +0.25)
- **Page integration + Big-Bang delete (commit 3)**: 1 day (orchestrator + delete legacy + smoke check sub-routes)
- **E2E specs (commit 4)**: 0.5 day
- **Bootstrap baselines (commit 5)**: 0.5 day (CI roundtrip + verify + matrix update)
- **PR review + merge**: 1 day buffer (Big-Bang scope + R1 staging rehearsal)

**Total**: 6-7 days (single-engineer, including review).

## 12. References

- Spec umbrella: [`2026-04-26-v2-design-migration.md`](2026-04-26-v2-design-migration.md) §4
- Phase 1 execution: [`2026-04-27-v2-migration-phase1-execution.md`](2026-04-27-v2-migration-phase1-execution.md) §3.3
- Migration matrix (single source of truth): [`docs/frontend/v2-migration-matrix.md`](../../frontend/v2-migration-matrix.md)
- Bundle size budget guide: [`docs/frontend/bundle-size-budget.md`](../../frontend/bundle-size-budget.md)
- MeepleCard design tokens: [`docs/frontend/meeple-card-design-tokens.md`](../../frontend/meeple-card-design-tokens.md)
- **Wave B.1 spec (template canonico)**: [`2026-04-29-v2-migration-wave-b-1-games.md`](2026-04-29-v2-migration-wave-b-1-games.md)
- **Wave B.2 spec**: visible da PR #637 commit `adbc97c3f` (mirror pattern, no separate doc)
- Wave A.4 spec (visual fixture sentinel reference): [`2026-04-28-v2-migration-wave-a-4-shared-game-detail.md`](2026-04-28-v2-migration-wave-a-4-shared-game-detail.md)
- `useTablistKeyboardNav` hook: [`apps/web/src/hooks/useTablistKeyboardNav.ts`](../../../apps/web/src/hooks/useTablistKeyboardNav.ts) (PR #623 + orientation extension PR #626)
- `useLibrary` hook (data layer): [`apps/web/src/hooks/queries/useLibrary.ts`](../../../apps/web/src/hooks/queries/useLibrary.ts)
- `libraryClient` API surface: [`apps/web/src/lib/api/clients/libraryClient.ts`](../../../apps/web/src/lib/api/clients/libraryClient.ts)
- `library.schemas` Zod definitions: [`apps/web/src/lib/api/schemas/library.schemas.ts`](../../../apps/web/src/lib/api/schemas/library.schemas.ts)
- Mockup: [`admin-mockups/design_files/sp4-library-desktop.jsx`](../../../admin-mockups/design_files/sp4-library-desktop.jsx)
- Existing brownfield page: [`apps/web/src/app/(authenticated)/library/page.tsx`](../../../apps/web/src/app/(authenticated)/library/page.tsx)
- Existing carousel orchestrator (target delete): [`apps/web/src/app/(authenticated)/library/LibraryHub.tsx`](../../../apps/web/src/app/(authenticated)/library/LibraryHub.tsx)
- Issue #574: [github.com/meepleAi-app/meepleai-monorepo/issues/574](https://github.com/meepleAi-app/meepleai-monorepo/issues/574)
- Umbrella #580: [github.com/meepleAi-app/meepleai-monorepo/issues/580](https://github.com/meepleAi-app/meepleai-monorepo/issues/580)
- Issue #636 (badge a11y debt): [github.com/meepleAi-app/meepleai-monorepo/issues/636](https://github.com/meepleAi-app/meepleai-monorepo/issues/636)
