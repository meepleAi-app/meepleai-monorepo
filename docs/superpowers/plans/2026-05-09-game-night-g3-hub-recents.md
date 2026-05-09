# G3 — Recent games widget in dashboard hub — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Permettere a un utente Alpha di raggiungere la chat-in-game di un gioco recente in ≤2 tap dalla dashboard, montando un nuovo widget `GamesRecentRail` in cima al Games Hub.

**Architecture:** Frontend-only PR su `apps/web`. Aggiunge un nuovo componente puro `GamesRecentRail` v2 + un nuovo hook `useRecentLibraryGames` (Zustand `useRecentsStore` filtrato a entity=game, con fallback a `useRecentlyAddedGames`). Wiring minimo nel `DashboardClient.tsx`: monta il rail in cima per i tab `library` e `catalog` senza modificare il rendering interno dei tab.

**Tech Stack:** React 19 + Next.js 16 App Router, TypeScript strict, Tailwind 4, Zustand, TanStack Query, Vitest. Pattern di riferimento: `RecentActivityRail` (PR #574), `GamesHero` (PR #635).

**Spec:** [`docs/superpowers/specs/2026-05-09-game-night-user-flow-design.md`](../specs/2026-05-09-game-night-user-flow-design.md) §G3.

**Scope reduction (post self-review)**: il wiring v2 di `/games?tab=catalog` è **rimosso da questo plan**. La spec assumeva di riusare `GamesResultsGrid` + `GamesFiltersInline` di Wave B.1, ma il review dei contratti mostra che sono **vincolati a `UserLibraryEntry`** (status `owned/wishlist/played`, sort `last-played/rating/title/year`) — incompatibili con lo schema `Game` del catalogo. Servono nuovi componenti dedicati (`GamesCatalogGrid`, `GamesCatalogFilters`) che vanno tracciati come backlog Wave 1 (riga catalog dedicata in v2-migration-matrix). Vedi §"Out of scope".

---

## File Structure

| Status | Path | Responsibility |
|---|---|---|
| Create | `apps/web/src/hooks/queries/useRecentLibraryGames.ts` | Hook che restituisce fino a N entry della libreria ordinati per recency (recents store → fallback addedAt desc) |
| Create | `apps/web/src/hooks/queries/__tests__/useRecentLibraryGames.test.ts` | Unit test hook (renderHook, 4 scenari) |
| Create | `apps/web/src/components/v2/games/GamesRecentRail.tsx` | Componente v2 puro — riceve `items[]`, label e callback; rende rail orizzontale di 1-N card |
| Create | `apps/web/src/components/v2/games/__tests__/GamesRecentRail.test.tsx` | Test rendering: empty, 1, N card, click handler, a11y region |
| Modify | `apps/web/src/components/v2/games/index.ts` | Aggiungere export di `GamesRecentRail` e tipi correlati |
| Modify | `apps/web/src/app/(authenticated)/dashboard/DashboardClient.tsx` | Inserire `<GamesRecentRail>` in cima a tutti i branch di rendering (sopra `GamesLibraryView` e sopra il blocco `HubLayout` per catalog/kb) |
| Modify | `docs/for-developers/frontend/v2-migration-matrix.md` | Aggiungere row `GamesRecentRail` nella sezione `/games` (status done) |

**Decomposition decision:** componente puro + hook separato dal wiring per facilitare testing isolato. Il rail è agnostico rispetto al `tab` attivo (mostra sempre i recent games dell'utente, in qualunque vista del hub).

---

## Pre-flight

- [ ] **Step 0.1: Crea feature branch dal parente `main-dev`**

```bash
git branch --show-current  # MUST print main-dev
git status                 # MUST show clean tree
git pull --ff-only
git checkout -b feature/issue-XXX-game-night-g3-recents-rail
git config branch.feature/issue-XXX-game-night-g3-recents-rail.parent main-dev
```

> Sostituire `XXX` con il numero issue (apri issue prima se non esiste). Il commit precedente (spec doc) deve essere già pushato/integrato in `main-dev`.

- [ ] **Step 0.2: Verifica clean build pre-modifiche**

Run:
```bash
cd apps/web && pnpm typecheck && pnpm lint
```
Expected: nessun errore. Se ci sono errori preesistenti, registrarli come baseline per non confonderli con quelli introdotti.

- [ ] **Step 0.3: Verifica shape effettiva di `useRecentsStore`**

Run: `grep -n "items\|RecentItem\|addedAt\|visitedAt\|entity" apps/web/src/stores/use-recents.ts | head -30`

Annota qui sotto la shape effettiva di un item del store (es. nome del campo timestamp: `addedAt`? `visitedAt`? Nome del campo entity-id: `id`? Forma del campo `entity`?). Questo è critico perché i test del Task 1 usano questa shape.

```
Shape effettiva (compila l'engineer dopo grep):
- Campo timestamp: ___
- Campo entity-id: ___
- Campo entity-type: ___
- Forma store API: useRecentsStore(state => state.___)
```

Se la shape differisce dai test del Task 1, **aggiorna i test nello Step 1.1 prima di proseguire**.

---

## Task 1: Hook `useRecentLibraryGames`

**Files:**
- Create: `apps/web/src/hooks/queries/useRecentLibraryGames.ts`
- Test: `apps/web/src/hooks/queries/__tests__/useRecentLibraryGames.test.ts`

**Contract** (decidi qui, congelato per resto del piano):
- Input: `limit?: number = 5`
- Output: `{ entries: readonly UserLibraryEntry[]; isLoading: boolean; isError: boolean }`
- Algoritmo:
  1. Legge `useRecentsStore` filtrando `entity === 'game'`
  2. Per ogni recent ID, prende l'entry da `useLibrary({ page: 1, pageSize: 50 })` (cached)
  3. Mantiene l'ordine dei recents (più recente prima)
  4. Filtra entries non più presenti in libreria (gioco rimosso dopo recent)
  5. Se `result.length < limit`, completa con `useRecentlyAddedGames(limit)` escludendo duplicati
  6. Tronca a `limit`
- `isLoading: true` se `useLibrary.isLoading`
- `isError: true` se `useLibrary.isError && recents.length === 0` (errore non recuperabile)

- [ ] **Step 1.1: Allinea i test alla shape effettiva (se necessario)**

In base allo Step 0.3, se i campi del recents store differiscono dal test sotto (es. campo si chiama `addedAt` invece di `visitedAt`), aggiorna il test prima di scriverlo. Il test sotto assume:
- `useRecentsStore.setState({ items: [...] })`
- Ogni item: `{ id, entity: 'game', title, href, visitedAt }`

Se differente, sostituire `visitedAt` (e `items` se serve) con i nomi reali.

- [ ] **Step 1.2: Scrivi il test failing**

Crea `apps/web/src/hooks/queries/__tests__/useRecentLibraryGames.test.ts`:

```ts
/**
 * useRecentLibraryGames — unit tests
 * Spec: docs/superpowers/specs/2026-05-09-game-night-user-flow-design.md §G3
 */
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import type { ReactNode } from 'react';

import { useRecentsStore } from '@/stores/use-recents';

vi.mock('@/hooks/queries/useLibrary', () => ({
  useLibrary: vi.fn(),
  useRecentlyAddedGames: vi.fn(),
}));

import { useLibrary, useRecentlyAddedGames } from '@/hooks/queries/useLibrary';
import { useRecentLibraryGames } from '../useRecentLibraryGames';

function wrapper({ children }: { children: ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

const entry = (id: string, addedAt = '2026-05-01T00:00:00Z') => ({
  id: `entry-${id}`,
  userId: 'u1',
  gameId: id,
  gameTitle: `Game ${id}`,
  isFavorite: false,
  currentState: 'Owned' as const,
  addedAt,
  hasKb: true,
  kbCardCount: 1,
  kbIndexedCount: 1,
  kbProcessingCount: 0,
  hasRagAccess: true,
  agentIsOwned: true,
  isPrivateGame: false,
  canProposeToCatalog: false,
});

const emptyPaginated = { items: [], page: 1, pageSize: 50, totalCount: 0, totalPages: 0, hasNextPage: false, hasPreviousPage: false };

describe('useRecentLibraryGames', () => {
  beforeEach(() => {
    useRecentsStore.setState({ items: [] });
    vi.mocked(useLibrary).mockReturnValue({
      data: emptyPaginated,
      isLoading: false,
      isError: false,
    } as any);
    vi.mocked(useRecentlyAddedGames).mockReturnValue({
      data: emptyPaginated,
      isLoading: false,
      isError: false,
    } as any);
  });

  it('returns recents-store games in order, mapped to library entries', async () => {
    const e1 = entry('aaa');
    const e2 = entry('bbb');
    vi.mocked(useLibrary).mockReturnValue({
      data: { ...emptyPaginated, items: [e1, e2], totalCount: 2, totalPages: 1 },
      isLoading: false,
      isError: false,
    } as any);
    useRecentsStore.setState({
      items: [
        { id: 'bbb', entity: 'game', title: 'Game bbb', href: '/library/games/bbb', visitedAt: '2026-05-09T10:00:00Z' },
        { id: 'aaa', entity: 'game', title: 'Game aaa', href: '/library/games/aaa', visitedAt: '2026-05-09T09:00:00Z' },
      ],
    });

    const { result } = renderHook(() => useRecentLibraryGames(5), { wrapper });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.entries.map(e => e.gameId)).toEqual(['bbb', 'aaa']);
  });

  it('falls back to recently-added when recents store empty', async () => {
    const e1 = entry('xxx', '2026-05-08T00:00:00Z');
    vi.mocked(useRecentlyAddedGames).mockReturnValue({
      data: { ...emptyPaginated, items: [e1], totalCount: 1, totalPages: 1 },
      isLoading: false,
      isError: false,
    } as any);

    const { result } = renderHook(() => useRecentLibraryGames(5), { wrapper });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.entries.map(e => e.gameId)).toEqual(['xxx']);
  });

  it('truncates to limit', async () => {
    const items = ['a', 'b', 'c', 'd', 'e', 'f'].map(id => entry(id));
    vi.mocked(useLibrary).mockReturnValue({
      data: { ...emptyPaginated, items, totalCount: 6, totalPages: 1 },
      isLoading: false,
      isError: false,
    } as any);
    useRecentsStore.setState({
      items: items.map(e => ({
        id: e.gameId, entity: 'game' as const, title: e.gameTitle,
        href: `/library/games/${e.gameId}`, visitedAt: '2026-05-09T10:00:00Z',
      })),
    });

    const { result } = renderHook(() => useRecentLibraryGames(3), { wrapper });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.entries).toHaveLength(3);
  });

  it('skips recents whose game is no longer in library', async () => {
    const e1 = entry('inlib');
    vi.mocked(useLibrary).mockReturnValue({
      data: { ...emptyPaginated, items: [e1], totalCount: 1, totalPages: 1 },
      isLoading: false,
      isError: false,
    } as any);
    useRecentsStore.setState({
      items: [
        { id: 'removed', entity: 'game', title: 'Removed', href: '/library/games/removed', visitedAt: '2026-05-09T10:00:00Z' },
        { id: 'inlib', entity: 'game', title: 'In Lib', href: '/library/games/inlib', visitedAt: '2026-05-09T09:00:00Z' },
      ],
    });

    const { result } = renderHook(() => useRecentLibraryGames(5), { wrapper });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.entries.map(e => e.gameId)).toEqual(['inlib']);
  });
});
```

- [ ] **Step 1.3: Run test, verify failure**

Run: `cd apps/web && pnpm vitest run src/hooks/queries/__tests__/useRecentLibraryGames.test.ts`
Expected: FAIL — module `../useRecentLibraryGames` not found.

- [ ] **Step 1.4: Implementa l'hook**

Crea `apps/web/src/hooks/queries/useRecentLibraryGames.ts`:

```ts
/**
 * useRecentLibraryGames — restituisce fino a N giochi della libreria ordinati
 * per recency: prima i giochi visitati di recente (Zustand recents store),
 * poi fallback a recently-added.
 *
 * Usato dal `GamesRecentRail` widget nel Dashboard hub per il flusso
 * "serata di gioco" (entry point ≤2 tap fino alla chat in-game).
 *
 * Spec: docs/superpowers/specs/2026-05-09-game-night-user-flow-design.md §G3
 */
import { useMemo } from 'react';

import { useLibrary, useRecentlyAddedGames } from '@/hooks/queries/useLibrary';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';
import { useRecentsStore } from '@/stores/use-recents';

export interface UseRecentLibraryGamesResult {
  readonly entries: readonly UserLibraryEntry[];
  readonly isLoading: boolean;
  readonly isError: boolean;
}

const LIBRARY_FETCH_SIZE = 50;

export function useRecentLibraryGames(limit: number = 5): UseRecentLibraryGamesResult {
  const libraryQuery = useLibrary({ page: 1, pageSize: LIBRARY_FETCH_SIZE });
  const recentlyAdded = useRecentlyAddedGames(limit);

  const recents = useRecentsStore(state => state.items);

  return useMemo<UseRecentLibraryGamesResult>(() => {
    const isLoading = libraryQuery.isLoading;
    const isError = libraryQuery.isError && recents.length === 0;

    const libIndex = new Map<string, UserLibraryEntry>();
    for (const entry of libraryQuery.data?.items ?? []) {
      libIndex.set(entry.gameId, entry);
    }

    const seen = new Set<string>();
    const result: UserLibraryEntry[] = [];

    for (const recent of recents) {
      if (recent.entity !== 'game') continue;
      const entry = libIndex.get(recent.id);
      if (!entry || seen.has(entry.gameId)) continue;
      result.push(entry);
      seen.add(entry.gameId);
      if (result.length >= limit) break;
    }

    if (result.length < limit) {
      for (const entry of recentlyAdded.data?.items ?? []) {
        if (seen.has(entry.gameId)) continue;
        result.push(entry);
        seen.add(entry.gameId);
        if (result.length >= limit) break;
      }
    }

    return { entries: result, isLoading, isError };
  }, [libraryQuery.data, libraryQuery.isLoading, libraryQuery.isError, recentlyAdded.data, recents, limit]);
}
```

> ⚠️ **Type alignment**: se la `useLibrary` reale richiede argomenti differenti (es. `useLibrary(params, enabled)` invece di `useLibrary({...})`), aggiusta. La signature reale è in `apps/web/src/hooks/queries/useLibrary.ts:69`.

- [ ] **Step 1.5: Run test fino a passare**

Run: `cd apps/web && pnpm vitest run src/hooks/queries/__tests__/useRecentLibraryGames.test.ts`
Expected: PASS (4/4). Se fallisce, NON modificare il test per farlo passare se il behavior atteso è documentato nel contract sopra: aggiusta l'implementazione.

- [ ] **Step 1.6: Commit**

```bash
git add apps/web/src/hooks/queries/useRecentLibraryGames.ts apps/web/src/hooks/queries/__tests__/useRecentLibraryGames.test.ts
git commit -m "feat(web): useRecentLibraryGames hook for game-night entry point (G3)"
```

---

## Task 2: Componente `GamesRecentRail`

**Files:**
- Create: `apps/web/src/components/v2/games/GamesRecentRail.tsx`
- Test: `apps/web/src/components/v2/games/__tests__/GamesRecentRail.test.tsx`
- Modify: `apps/web/src/components/v2/games/index.ts`

**Contract** (pure component, mirror del pattern Wave B.1 / B.3):
- Props:
  ```ts
  interface GamesRecentRailItem {
    readonly id: string;
    readonly title: string;
    readonly imageUrl?: string;
    readonly kbBadge: 'ready' | 'processing' | 'none';
  }
  interface GamesRecentRailLabels {
    readonly sectionTitle: string;
    readonly emptyHint: string;
    readonly viewAllHref: string; // riservato per future "vedi tutti" CTA
  }
  interface GamesRecentRailProps {
    readonly items: readonly GamesRecentRailItem[];
    readonly labels: GamesRecentRailLabels;
    readonly isLoading?: boolean;
    readonly onSelect: (id: string) => void;
    readonly className?: string;
  }
  ```
- Stati visivi: `loading` (3 skeleton card 96x160), `empty` (testo `emptyHint`), `populated` (rail orizzontale scrollabile)
- a11y: `<section role="region" aria-label={labels.sectionTitle}>`, ogni card è `<button type="button">` focusabile con `aria-label` derivato da `title`
- NO fetch, NO router, NO i18n hook (orchestrator passa label risolte e onSelect)

- [ ] **Step 2.1: Test failing**

Crea `apps/web/src/components/v2/games/__tests__/GamesRecentRail.test.tsx`:

```tsx
/**
 * GamesRecentRail — pure component tests
 * Spec: docs/superpowers/specs/2026-05-09-game-night-user-flow-design.md §G3
 */
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { GamesRecentRail } from '../GamesRecentRail';

const labels = {
  sectionTitle: 'Giochi recenti',
  emptyHint: 'Inizia a giocare per vedere qui i tuoi titoli recenti.',
  viewAllHref: '/library',
};

describe('GamesRecentRail', () => {
  it('renders skeleton when isLoading', () => {
    render(<GamesRecentRail items={[]} labels={labels} isLoading onSelect={vi.fn()} />);
    expect(screen.getAllByTestId('games-recent-rail-skeleton')).toHaveLength(3);
  });

  it('renders empty hint when no items and not loading', () => {
    render(<GamesRecentRail items={[]} labels={labels} onSelect={vi.fn()} />);
    expect(screen.getByText(labels.emptyHint)).toBeInTheDocument();
  });

  it('renders 1 to 5 cards in order', () => {
    const items = ['a', 'b', 'c'].map(id => ({
      id, title: `Game ${id}`, kbBadge: 'ready' as const,
    }));
    render(<GamesRecentRail items={items} labels={labels} onSelect={vi.fn()} />);
    const cards = screen.getAllByRole('button', { name: /Game [abc]/ });
    expect(cards.map(c => c.textContent)).toEqual([
      expect.stringContaining('Game a'),
      expect.stringContaining('Game b'),
      expect.stringContaining('Game c'),
    ]);
  });

  it('calls onSelect with game id when card clicked', () => {
    const onSelect = vi.fn();
    render(
      <GamesRecentRail
        items={[{ id: 'wingspan', title: 'Wingspan', kbBadge: 'ready' }]}
        labels={labels}
        onSelect={onSelect}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: /Wingspan/ }));
    expect(onSelect).toHaveBeenCalledWith('wingspan');
  });

  it('section has accessible name from sectionTitle', () => {
    render(<GamesRecentRail items={[]} labels={labels} onSelect={vi.fn()} />);
    expect(screen.getByRole('region', { name: labels.sectionTitle })).toBeInTheDocument();
  });
});
```

- [ ] **Step 2.2: Run test, verify failure**

Run: `cd apps/web && pnpm vitest run src/components/v2/games/__tests__/GamesRecentRail.test.tsx`
Expected: FAIL — `GamesRecentRail` not found.

- [ ] **Step 2.3: Implementa il componente**

Crea `apps/web/src/components/v2/games/GamesRecentRail.tsx`:

```tsx
/**
 * GamesRecentRail — v2 horizontal rail per il flusso "serata di gioco" (G3).
 *
 * Pure component (no fetch, no router, no i18n hook): orchestrator passa
 * items + labels + onSelect. Riusa pattern Wave B.1 (vedi GamesHero).
 *
 * Spec: docs/superpowers/specs/2026-05-09-game-night-user-flow-design.md §G3
 */
import type { ReactElement } from 'react';

import clsx from 'clsx';

import { Skeleton } from '@/components/ui/feedback/skeleton';

export type RecentKbBadge = 'ready' | 'processing' | 'none';

export interface GamesRecentRailItem {
  readonly id: string;
  readonly title: string;
  readonly imageUrl?: string;
  readonly kbBadge: RecentKbBadge;
}

export interface GamesRecentRailLabels {
  readonly sectionTitle: string;
  readonly emptyHint: string;
  readonly viewAllHref: string;
}

export interface GamesRecentRailProps {
  readonly items: readonly GamesRecentRailItem[];
  readonly labels: GamesRecentRailLabels;
  readonly isLoading?: boolean;
  readonly onSelect: (id: string) => void;
  readonly className?: string;
}

const SKELETON_COUNT = 3;

const KB_BADGE_LABEL: Record<RecentKbBadge, string> = {
  ready: '📖 KB pronta',
  processing: '⏳ KB in elaborazione',
  none: '',
};

export function GamesRecentRail({
  items,
  labels,
  isLoading = false,
  onSelect,
  className,
}: GamesRecentRailProps): ReactElement {
  return (
    <section
      role="region"
      aria-label={labels.sectionTitle}
      data-slot="games-recent-rail"
      className={clsx('w-full px-4 py-3', className)}
    >
      <div className="flex items-baseline justify-between mb-3">
        <h2 className="text-base font-semibold text-foreground">{labels.sectionTitle}</h2>
      </div>

      {isLoading ? (
        <div className="flex gap-3 overflow-x-auto pb-1">
          {Array.from({ length: SKELETON_COUNT }).map((_, i) => (
            <Skeleton
              key={i}
              data-testid="games-recent-rail-skeleton"
              className="h-24 w-40 shrink-0 rounded-xl"
            />
          ))}
        </div>
      ) : items.length === 0 ? (
        <p className="text-sm text-muted-foreground py-4">{labels.emptyHint}</p>
      ) : (
        <div className="flex gap-3 overflow-x-auto pb-1 snap-x snap-mandatory">
          {items.map(item => (
            <button
              key={item.id}
              type="button"
              onClick={() => onSelect(item.id)}
              aria-label={item.title}
              data-slot="games-recent-rail-card"
              data-kb-badge={item.kbBadge}
              className={clsx(
                'shrink-0 snap-start text-left rounded-xl border border-border bg-card',
                'h-24 w-40 px-3 py-2 transition-colors hover:bg-muted',
                'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
              )}
            >
              <span className="block text-sm font-medium text-foreground line-clamp-2">
                {item.title}
              </span>
              {item.kbBadge !== 'none' && (
                <span className="mt-1 block text-xs text-muted-foreground">
                  {KB_BADGE_LABEL[item.kbBadge]}
                </span>
              )}
            </button>
          ))}
        </div>
      )}
    </section>
  );
}
```

- [ ] **Step 2.4: Aggiungi export al barrel**

Modifica `apps/web/src/components/v2/games/index.ts` — appendi al fondo:

```ts
export { GamesRecentRail } from './GamesRecentRail';
export type {
  GamesRecentRailItem,
  GamesRecentRailLabels,
  GamesRecentRailProps,
  RecentKbBadge,
} from './GamesRecentRail';
```

- [ ] **Step 2.5: Run test fino a passare**

Run: `cd apps/web && pnpm vitest run src/components/v2/games/__tests__/GamesRecentRail.test.tsx`
Expected: PASS (5/5).

- [ ] **Step 2.6: Commit**

```bash
git add apps/web/src/components/v2/games/GamesRecentRail.tsx apps/web/src/components/v2/games/__tests__/GamesRecentRail.test.tsx apps/web/src/components/v2/games/index.ts
git commit -m "feat(web): GamesRecentRail v2 component for game-night entry point (G3)"
```

---

## Task 3: Wiring nel `DashboardClient.tsx`

**Files:**
- Modify: `apps/web/src/app/(authenticated)/dashboard/DashboardClient.tsx`

**Contract:**
- Mantieni intatto il branch `tab === 'library'` (delega `GamesLibraryView`)
- Mantieni intatto il rendering legacy per `tab === 'catalog'` e `tab === 'kb'` (HubLayout) — il loro v2 wiring va in plan separato
- Aggiungi un wrap che monta `<GamesRecentRail>` in cima a tutti i branch di rendering, con `onSelect` che naviga a `/library/games/[id]?tab=aiChat` (entry point G1)

- [ ] **Step 3.1: Identifica i blocchi da modificare**

Run: `grep -n "activeTab === 'library'\|return (" apps/web/src/app/\(authenticated\)/dashboard/DashboardClient.tsx | head -10`

Annota i numeri di riga del:
- Early return su `library` (oggi riga ~109-111)
- Return finale del component (oggi riga ~129)

- [ ] **Step 3.2: Aggiungi import**

In testa al file, dopo gli import esistenti (mantieni l'ordine alfabetico se il progetto lo segue):

```ts
import { useRouter } from 'next/navigation';
import { GamesRecentRail } from '@/components/v2/games';
import { useRecentLibraryGames } from '@/hooks/queries/useRecentLibraryGames';
```

> Se `useRouter` è già importato dal file, NON duplicare. `useMemo` dovrebbe essere già importato (verifica).

- [ ] **Step 3.3: Aggiungi il rail come variabile locale**

Subito dopo la riga `const { data: catalogData, isLoading: catalogLoading } = useGames(...)` (oggi riga ~70), aggiungi:

```ts
const router = useRouter();
const recentGames = useRecentLibraryGames(5);

const recentRailItems = useMemo(
  () => recentGames.entries.map(e => ({
    id: e.gameId,
    title: e.gameTitle,
    imageUrl: e.gameImageUrl ?? undefined,
    kbBadge: (e.kbIndexedCount > 0
      ? 'ready'
      : e.kbProcessingCount > 0
        ? 'processing'
        : 'none') as 'ready' | 'processing' | 'none',
  })),
  [recentGames.entries]
);

const recentRail = (
  <GamesRecentRail
    items={recentRailItems}
    isLoading={recentGames.isLoading}
    labels={{
      sectionTitle: 'Giochi recenti',
      emptyHint: 'Inizia a giocare per vedere qui i tuoi titoli recenti.',
      viewAllHref: '/library',
    }}
    onSelect={(id) => router.push(`/library/games/${id}?tab=aiChat`)}
  />
);
```

- [ ] **Step 3.4: Wrap del branch library**

Sostituisci:

```tsx
if (activeTab === 'library') {
  return <GamesLibraryView />;
}
```

con:

```tsx
if (activeTab === 'library') {
  return (
    <>
      {recentRail}
      <GamesLibraryView />
    </>
  );
}
```

- [ ] **Step 3.5: Wrap del return finale (catalog/kb)**

Identifica il `return ( <HubLayout ...>...</HubLayout> )` finale del component (oggi ~riga 129-164). Wrappalo in un fragment con il `recentRail` in cima. Il rendering interno dell'`HubLayout` resta invariato.

Pattern (sostituisci `<HubLayout ...>...</HubLayout>` con):

```tsx
return (
  <>
    {recentRail}
    <HubLayout
      searchPlaceholder="Cerca giochi..."
      filterChips={currentFilters}
      activeFilterId={activeFilter}
      onFilterChange={setActiveFilter}
      searchValue={search}
      onSearchChange={setSearch}
      viewMode={viewMode}
      onViewModeChange={setViewMode}
      showViewToggle
      topActions={topActions}
    >
      {/* contenuto invariato */}
    </HubLayout>
  </>
);
```

> Mantieni invariato il contenuto interno `{isLoading ? <LoadingSkeleton /> : items.length === 0 ? ... : <div ...>{items.map(...)}</div>}`.

- [ ] **Step 3.6: Run typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: nessun errore TS.

Se appare warning per import non usati o `useMemo` non importato, sistema l'import block.

- [ ] **Step 3.7: Run test esistenti per regressioni**

Run: `cd apps/web && pnpm vitest run src/app/\(authenticated\)/dashboard src/app/\(authenticated\)/games`
Expected: tutti i test passano. Se test esistenti del dashboard fanno snapshot del DOM, andranno aggiornati per includere il rail (questo è atteso, NON una regressione).

- [ ] **Step 3.8: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/dashboard/DashboardClient.tsx
git commit -m "feat(web): mount GamesRecentRail at top of dashboard hub (G3)"
```

---

## Task 4: Verifica end-to-end manuale + matrice

- [ ] **Step 4.1: Lint e typecheck full-pass**

Run: `cd apps/web && pnpm lint && pnpm typecheck`
Expected: 0 errori, 0 warning nuovi.

- [ ] **Step 4.2: Run full unit suite**

Run: `cd apps/web && pnpm test`
Expected: tutti i test passano. Annota eventuali pre-existing failure (NON risolvere in questa PR).

- [ ] **Step 4.3: Smoke test manuale**

Avvia l'ambiente:
```bash
cd infra && make dev-core
```

Apri `http://localhost:3000/dashboard` o `/games?tab=library`:
- Deve apparire `GamesRecentRail` in cima al contenuto
- Stato vuoto (utente nuovo, recents store vuoto, libreria vuota): mostra empty hint
- Stato popolato (utente con giochi in libreria): mostra fino a 5 card
- Click su una card → naviga a `/library/games/[id]?tab=aiChat`
- Naviga in `/games?tab=catalog` → il rail appare anche qui
- Naviga in `/games?tab=library` → il rail appare anche qui

Annota differenze visive (spacing, colori) come issue follow-up: NON correggere in questa PR salvo regressioni gravi (es. layout shift, overlap, scroll bloccato).

- [ ] **Step 4.4: Aggiorna v2-migration-matrix**

Modifica `docs/for-developers/frontend/v2-migration-matrix.md`. Nella sezione **"Wave 1 — Games index — `/games` — 5 components — Tier S"**, aggiungi una row sotto le 5 esistenti:

```markdown
| `sp4-games-index.jsx` (extension) | `GamesRecentRail` | `apps/web/src/components/v2/games/GamesRecentRail.tsx` | `/dashboard` · `/games` | done | #YYY | T A V |
```

Sostituisci `#YYY` con il numero PR effettivo dopo apertura.

> NB: questo è un componente NUOVO non in mockup originali — annotalo nella riga "Updated YYYY-MM-DD" in cima alla matrice se quella sezione è viva.

- [ ] **Step 4.5: Push + apri PR verso `main-dev`**

```bash
git push -u origin feature/issue-XXX-game-night-g3-recents-rail
gh pr create --base main-dev --title "feat(web): G3 — GamesRecentRail in dashboard hub" --body "$(cat <<'EOF'
## Summary
- Add `GamesRecentRail` v2 component (pure, mirror Wave B.1 pattern)
- Add `useRecentLibraryGames` hook (Zustand recents store + library fallback)
- Mount rail at top of dashboard hub (visible across `library`, `catalog`, `kb` tabs)
- Card click navigates to `/library/games/[id]?tab=aiChat` (G1 entry point)

Implements **G3** from spec `docs/superpowers/specs/2026-05-09-game-night-user-flow-design.md`.

**Scope reduction note**: original spec G3 also targeted v2 wiring of `/games?tab=catalog` (reusing `GamesResultsGrid` + `GamesFiltersInline`). Self-review during plan writing showed those components are vincolati a `UserLibraryEntry` and library-only filter keys. Catalog tab v2 wiring requires NEW dedicated components and is tracked as separate backlog (see Out of scope in plan).

## Test plan
- [ ] Unit tests pass for `useRecentLibraryGames` (4 scenari) and `GamesRecentRail` (5 scenari)
- [ ] Manual smoke at `/dashboard`, `/games?tab=library`, `/games?tab=catalog`, `/games?tab=kb`
- [ ] No regression on existing `GamesLibraryView` flow
- [ ] Card click navigates to `/library/games/[id]?tab=aiChat`
- [ ] Empty state shows hint when user has no recents/library

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 4.6: Commit di chiusura matrice (post PR-number)**

Dopo aver aperto la PR e ottenuto il numero, sostituisci `#YYY` nella matrice e committa:

```bash
git add docs/for-developers/frontend/v2-migration-matrix.md
git commit -m "docs(matrix): add GamesRecentRail row (G3)"
git push
```

---

## Out of scope (follow-up plans)

| Item | Reason | Plan target |
|---|---|---|
| `/games?tab=catalog` v2 wiring | `GamesResultsGrid`/`GamesFiltersInline` Wave B.1 sono library-only (UserLibraryEntry-coupled). Servono `GamesCatalogGrid` + `GamesCatalogFilters` nuovi | Plan separato Wave 1 finalization (con issue dedicata + row in matrix) |
| `/games?tab=kb` v2 wiring | Fuori scope G3 | Coperto parzialmente da Plan G4 |
| `AdvancedFiltersDrawer` wiring | Fuori scope G3 | Plan separato Wave 1 finalization |
| `/kb/[id]` 6 stub pending | Goal G4 | Plan G4 KB detail (separato) |
| Chat-in-game v2 (`?tab=aiChat`) | Goal G1 + G5, blocked by missing SP4 mockup | Plan G1+G5 dopo issue Claude Design |
| Fast resume audit (G2) | Goal G2 | Plan G2 audit |
| `useRecentsStore` push da `/library/games/[id]` | Probabilmente già fatto, da verificare in audit | Audit interno (vedi OQ4 in spec) |

---

## Spec Self-Review (effettuato — risultati)

**Spec coverage**: questo plan copre **solo G3** parzialmente — il widget recents (valore principale) è incluso; il wiring catalog v2 è stato spostato in plan separato dopo il review (vedi scope reduction in alto). I goal G1, G2, G4, G5 hanno plan separati.

**Placeholder scan**: rimasti solo i placeholder intenzionali `XXX` (numero issue) e `YYY` (numero PR), che vanno sostituiti dall'engineer eseguente al momento dell'apertura issue/PR. Nessun TBD/TODO/etc. nel codice.

**Type consistency** (verificata contro file reali):
- `GamesRecentRailItem.kbBadge: 'ready' | 'processing' | 'none'` — re-esportato come `RecentKbBadge` dal barrel ed usato nello stesso modo in Task 3.3. ✓
- `useRecentLibraryGames(limit)` ritorna `{ entries, isLoading, isError }` — Task 3.3 mappa `entries` su `recentRailItems` con shape compatibile con `GamesRecentRailItem`. ✓
- `UserLibraryEntry` ha campi `gameId`, `gameTitle`, `gameImageUrl`, `kbIndexedCount`, `kbProcessingCount` (verificati in `library.schemas.ts:32-63`). ✓
- `useLibrary({ page, pageSize })` segnatura — verificata in `useLibrary.ts:69`. Step 1.4 ha warning esplicito per riallineare se differente. ✓
- `useRecentsStore` shape — Step 0.3 forza l'engineer a verificarla prima di scrivere i test. ✓

**Ambiguity check**: l'unica ambiguità residua è la shape di `useRecentsStore` (campo `visitedAt` vs `addedAt`, `items` vs `entries`). Step 0.3 esiste apposta per risolverla prima di iniziare il Task 1.

**Risk noted**: lo spec a monte assumeva che i componenti v2 Wave B.1 fossero riusabili anche per il catalog tab — questa assunzione è stata invalidata dal review. Il plan riduce lo scope di conseguenza e documenta esplicitamente il follow-up necessario, senza promettere ciò che lo spec assumeva.
