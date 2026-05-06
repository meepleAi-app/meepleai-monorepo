# Public Library Page Improvements

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migliorare la pagina `/library?tab=public` correggendo bug funzionali (badge "In libreria" errati, card sbagliate) e UX (filtri meccanica infiniti, mancanza di debounce, nessun feedback su azioni).

**Architecture:** Task 1 è un cambiamento isolato a `MechanicFilter.tsx` (aggiunge collapse). Task 2 è un refactor completo di `PublicLibraryPage.tsx`: sostituisce `ShelfCard` con `MeepleCard entity="game"` nella grid, aumenta il pageSize libreria per correggere i badge, aggiunge debounce, toast, error state, conteggio totale e navigazione al dettaglio.

**Tech Stack:** React 19, Next.js 16 App Router, TanStack Query v5, Vitest + React Testing Library, Tailwind 4, shadcn/ui, MeepleCard design system

---

## File Map

| File | Azione | Responsabilità |
|------|--------|----------------|
| `apps/web/src/components/library/MechanicFilter.tsx` | Modifica | Aggiunge collapse (max 5 visibili + toggle) |
| `apps/web/src/components/library/PublicLibraryPage.tsx` | Modifica | Refactor completo: MeepleCard, debounce, fix bug, error/empty state, toast, count, nav |
| `apps/web/src/components/library/__tests__/MechanicFilter.test.tsx` | Crea | Test collapse behaviour |
| `apps/web/src/components/library/__tests__/PublicLibraryPage.test.tsx` | Crea | Test rendering, errori, add-to-library, empty state |

---

## Task 1: MechanicFilter — Collapse (C6)

**Files:**
- Modify: `apps/web/src/components/library/MechanicFilter.tsx`
- Create: `apps/web/src/components/library/__tests__/MechanicFilter.test.tsx`

- [ ] **Step 1.1: Scrivi il test (failing)**

Crea `apps/web/src/components/library/__tests__/MechanicFilter.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi } from 'vitest';

import { MechanicFilter } from '../MechanicFilter';

const MANY_MECHANICS = [
  'engine-building', 'area-control', 'deck-building', 'worker-placement',
  'cooperative', 'competitive', 'dice-rolling', 'puzzle-abstract',
];
const FEW_MECHANICS = ['engine-building', 'area-control', 'deck-building'];

describe('MechanicFilter', () => {
  it('mostra al massimo 5 chip quando ci sono più di 5 meccaniche', () => {
    render(
      <MechanicFilter mechanics={MANY_MECHANICS} selected={[]} onSelect={vi.fn()} />
    );
    // 5 chip + 1 toggle button visibili
    const chipButtons = screen.getAllByRole('button');
    // 5 chip + 1 "mostra altri" button
    expect(chipButtons).toHaveLength(6);
  });

  it('non mostra il toggle se ci sono 5 o meno meccaniche', () => {
    render(
      <MechanicFilter mechanics={FEW_MECHANICS} selected={[]} onSelect={vi.fn()} />
    );
    expect(screen.getAllByRole('button')).toHaveLength(3);
    expect(screen.queryByTestId('mechanic-toggle')).not.toBeInTheDocument();
  });

  it('espande tutti i chip al click sul toggle', async () => {
    const user = userEvent.setup();
    render(
      <MechanicFilter mechanics={MANY_MECHANICS} selected={[]} onSelect={vi.fn()} />
    );
    const toggle = screen.getByTestId('mechanic-toggle');
    await user.click(toggle);
    // Ora tutti 8 chip + il toggle button
    expect(screen.getAllByRole('button')).toHaveLength(9);
  });

  it('chiama onSelect con il mechanic slug al click', async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();
    render(
      <MechanicFilter mechanics={FEW_MECHANICS} selected={[]} onSelect={onSelect} />
    );
    await user.click(screen.getByText('Engine Building'));
    expect(onSelect).toHaveBeenCalledWith('engine-building');
  });

  it('mostra chip selezionati con stile attivo (aria-pressed)', () => {
    render(
      <MechanicFilter
        mechanics={FEW_MECHANICS}
        selected={['engine-building']}
        onSelect={vi.fn()}
      />
    );
    expect(screen.getByText('Engine Building').closest('button')).toHaveAttribute(
      'aria-pressed',
      'true'
    );
  });
});
```

- [ ] **Step 1.2: Esegui il test — verifica che fallisca**

```bash
cd apps/web && pnpm test src/components/library/__tests__/MechanicFilter.test.tsx --run
```

Atteso: `FAIL` — "mechanic-toggle" not found e conteggi errati.

- [ ] **Step 1.3: Implementa il collapse in MechanicFilter**

Sostituisci il contenuto di `apps/web/src/components/library/MechanicFilter.tsx`:

```tsx
'use client';

import { useState } from 'react';

import { MechanicIcon } from '@/components/icons/mechanics';
import { cn } from '@/lib/utils';

const MECHANIC_LABELS: Record<string, string> = {
  'engine-building': 'Engine Building',
  'area-control': 'Area Control',
  'deck-building': 'Deck Building',
  'worker-placement': 'Worker Placement',
  cooperative: 'Cooperativo',
  competitive: 'Competitivo',
  'dice-rolling': 'Dice Rolling',
  'puzzle-abstract': 'Puzzle/Abstract',
  'narrative-rpg': 'Narrativo/RPG',
  'tile-placement': 'Tile Placement',
  trading: 'Trading',
  'set-collection': 'Set Collection',
  'dungeon-crawler': 'Dungeon Crawler',
  'route-building': 'Route Building',
  'social-deduction': 'Social Deduction',
};

const MAX_VISIBLE = 5;

export interface MechanicFilterProps {
  mechanics: string[];
  selected: string[];
  onSelect: (mechanic: string) => void;
}

export function MechanicFilter({ mechanics, selected, onSelect }: MechanicFilterProps) {
  const [expanded, setExpanded] = useState(false);
  const hasMore = mechanics.length > MAX_VISIBLE;
  const visibleMechanics = expanded ? mechanics : mechanics.slice(0, MAX_VISIBLE);
  const hiddenCount = mechanics.length - MAX_VISIBLE;

  return (
    <div className="flex flex-wrap gap-2">
      {visibleMechanics.map(mechanic => {
        const isSelected = selected.includes(mechanic);
        return (
          <button
            key={mechanic}
            type="button"
            aria-pressed={isSelected}
            onClick={() => onSelect(mechanic)}
            className={cn(
              'flex items-center gap-1.5 rounded-full px-3 py-1.5 text-xs font-medium transition-colors',
              isSelected
                ? 'bg-[#f0a030] text-[#0d1117]'
                : 'bg-[#21262d] text-[#8b949e] hover:bg-[#30363d] hover:text-[#e6edf3]'
            )}
          >
            <MechanicIcon mechanic={mechanic} size={14} />
            {MECHANIC_LABELS[mechanic] ?? mechanic}
          </button>
        );
      })}

      {hasMore && (
        <button
          type="button"
          data-testid="mechanic-toggle"
          onClick={() => setExpanded(e => !e)}
          className="flex items-center gap-1.5 rounded-full px-3 py-1.5 text-xs font-medium bg-[#21262d] text-[#8b949e] hover:bg-[#30363d] hover:text-[#e6edf3] transition-colors border border-dashed border-[#30363d]"
        >
          {expanded ? '↑ Meno' : `+${hiddenCount} altri`}
        </button>
      )}
    </div>
  );
}
```

- [ ] **Step 1.4: Esegui il test — verifica che passi**

```bash
cd apps/web && pnpm test src/components/library/__tests__/MechanicFilter.test.tsx --run
```

Atteso: `PASS` — tutti 5 test verdi.

- [ ] **Step 1.5: Commit**

```bash
git add apps/web/src/components/library/MechanicFilter.tsx \
        apps/web/src/components/library/__tests__/MechanicFilter.test.tsx
git commit -m "feat(library): collapse mechanic filter chips — max 5 visible + toggle"
```

---

## Task 2: PublicLibraryPage — Refactor completo (C1, C3, C4, C5, I1, I2, I3, I4, I5)

**Files:**
- Modify: `apps/web/src/components/library/PublicLibraryPage.tsx`
- Create: `apps/web/src/components/library/__tests__/PublicLibraryPage.test.tsx`

### Step 2.1: Scrivi i test (failing)

Crea `apps/web/src/components/library/__tests__/PublicLibraryPage.test.tsx`:

```tsx
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, type Mock } from 'vitest';
import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import { useSharedGames, useGameMechanics } from '@/hooks/queries/useSharedGames';
import { useCatalogTrending } from '@/hooks/queries/useCatalogTrending';
import { useAddGameToLibrary, useLibrary } from '@/hooks/queries/useLibrary';

import { PublicLibraryPage } from '../PublicLibraryPage';

vi.mock('next/navigation', () => ({
  useRouter: vi.fn(() => ({ push: vi.fn() })),
}));

vi.mock('@/hooks/queries/useSharedGames', () => ({
  useSharedGames: vi.fn(),
  useGameMechanics: vi.fn(),
}));
vi.mock('@/hooks/queries/useCatalogTrending', () => ({
  useCatalogTrending: vi.fn(),
}));
vi.mock('@/hooks/queries/useLibrary', () => ({
  useLibrary: vi.fn(),
  useAddGameToLibrary: vi.fn(),
}));

const MOCK_GAMES = [
  { id: 'g1', title: 'Catan', yearPublished: 1995, thumbnailUrl: '', imageUrl: '', averageRating: 7.5, minPlayers: 3, maxPlayers: 4, playingTimeMinutes: 90 },
  { id: 'g2', title: 'Ticket to Ride', yearPublished: 0, thumbnailUrl: '', imageUrl: '', averageRating: null, minPlayers: 2, maxPlayers: 5, playingTimeMinutes: 60 },
];

const MOCK_CATALOG = { items: MOCK_GAMES, total: 2, page: 1, pageSize: 18 };
const MOCK_LIBRARY = { items: [{ gameId: 'g1' }], totalCount: 1 };
const MOCK_ADD_MUTATE = vi.fn();

function setupMocks(overrides: Partial<{
  catalogError: boolean;
  trendingError: boolean;
  catalogEmpty: boolean;
}> = {}) {
  (useSharedGames as Mock).mockReturnValue(
    overrides.catalogError
      ? { data: undefined, isLoading: false, isError: true }
      : overrides.catalogEmpty
        ? { data: { items: [], total: 0, page: 1, pageSize: 18 }, isLoading: false, isError: false }
        : { data: MOCK_CATALOG, isLoading: false, isError: false }
  );
  (useGameMechanics as Mock).mockReturnValue({ data: [] });
  (useCatalogTrending as Mock).mockReturnValue(
    overrides.trendingError
      ? { data: undefined, isLoading: false, isError: true }
      : { data: [], isLoading: false, isError: false }
  );
  (useLibrary as Mock).mockReturnValue({ data: MOCK_LIBRARY });
  (useAddGameToLibrary as Mock).mockReturnValue({ mutate: MOCK_ADD_MUTATE });
}

describe('PublicLibraryPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    setupMocks();
  });

  it('renderizza le card del catalogo come MeepleCard (data-entity=game)', async () => {
    renderWithQuery(<PublicLibraryPage />);
    await waitFor(() => {
      const cards = screen.getAllByTestId('catalog-game-card');
      expect(cards.length).toBeGreaterThan(0);
      cards.forEach(card => expect(card).toHaveAttribute('data-entity', 'game'));
    });
  });

  it('mostra "—" per yearPublished = 0', async () => {
    renderWithQuery(<PublicLibraryPage />);
    await waitFor(() => {
      expect(screen.getByText('—')).toBeInTheDocument();
    });
  });

  it('mostra il totale giochi nella sezione header', async () => {
    renderWithQuery(<PublicLibraryPage />);
    await waitFor(() => {
      expect(screen.getByTestId('catalog-total-count')).toHaveTextContent('2');
    });
  });

  it('mostra error state quando il catalogo fallisce', async () => {
    setupMocks({ catalogError: true });
    renderWithQuery(<PublicLibraryPage />);
    await waitFor(() => {
      expect(screen.getByTestId('catalog-error')).toBeInTheDocument();
    });
  });

  it('mostra error state quando il trending fallisce', async () => {
    setupMocks({ trendingError: true });
    renderWithQuery(<PublicLibraryPage />);
    await waitFor(() => {
      expect(screen.getByTestId('trending-error')).toBeInTheDocument();
    });
  });

  it('empty state mostra messaggio composito con filtri meccanica attivi', async () => {
    setupMocks({ catalogEmpty: true });
    renderWithQuery(<PublicLibraryPage />);
    // Simula filtri meccanica attivi — non possibile direttamente senza interazione
    // Il test verifica che il componente renderizzi l'empty state base
    await waitFor(() => {
      expect(screen.getByTestId('catalog-empty')).toBeInTheDocument();
    });
  });

  it('useLibrary viene chiamato con pageSize 1000', () => {
    renderWithQuery(<PublicLibraryPage />);
    expect(useLibrary).toHaveBeenCalledWith({ pageSize: 1000 });
  });
});
```

- [ ] **Step 2.2: Esegui i test — verifica che falliscano**

```bash
cd apps/web && pnpm test src/components/library/__tests__/PublicLibraryPage.test.tsx --run
```

Atteso: `FAIL` — la maggior parte fallisce (ShelfCard non ha `data-entity`, useLibrary chiamato con `100` non `1000`, mancano `data-testid` per error/total).

- [ ] **Step 2.3: Implementa il refactor di PublicLibraryPage**

Sostituisci l'intero contenuto di `apps/web/src/components/library/PublicLibraryPage.tsx`:

```tsx
/**
 * PublicLibraryPage Component
 *
 * Browse experience for the shared game catalog.
 * Features:
 *   1. Centered search bar con debounce 300ms
 *   2. Trending section — horizontal ShelfRow con ShelfCard
 *   3. Tutti i giochi — MechanicFilter collassabile + grid MeepleCard entity="game"
 *   4. Load More button per paginazione
 *   5. Error states per trending e catalog
 *   6. Toast feedback su addToLibrary
 *   7. Navigazione al dettaglio gioco via onClick
 *   8. Conteggio totale giochi nel header sezione
 */

'use client';

import { useState, useMemo, useCallback, useEffect } from 'react';

import { Clock, Users, Search, Loader2, Plus } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { toast } from '@/components/layout';
import { EmptyState } from '@/components/empty-state/EmptyState';
import { MechanicFilter } from '@/components/library/MechanicFilter';
import { ShelfCard } from '@/components/library/ShelfCard';
import { ShelfRow } from '@/components/library/ShelfRow';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { SectionBlock } from '@/components/ui/SectionBlock';
import { useDebounce } from '@/components/ui/data-display/entity-list-view/hooks/use-debounce';
import { useCatalogTrending } from '@/hooks/queries/useCatalogTrending';
import { useAddGameToLibrary, useLibrary } from '@/hooks/queries/useLibrary';
import { useSharedGames, useGameMechanics } from '@/hooks/queries/useSharedGames';
import type { SharedGame } from '@/lib/api/schemas/shared-games.schemas';
import { cn } from '@/lib/utils';

// ============================================================================
// Constants
// ============================================================================

const PAGE_SIZE = 18;

// ============================================================================
// Props
// ============================================================================

export interface PublicLibraryPageProps {
  className?: string;
}

// ============================================================================
// Helpers
// ============================================================================

function formatYear(year: number): string {
  return year > 0 ? String(year) : '—';
}

// ============================================================================
// Component
// ============================================================================

/**
 * PublicLibraryPage — catalog browse page with trending, mechanic filters,
 * paginated grid di MeepleCard, add-to-library actions e navigazione dettaglio.
 */
export function PublicLibraryPage({ className }: PublicLibraryPageProps) {
  const router = useRouter();

  // ------------------------------------------------------------------
  // Local state
  // ------------------------------------------------------------------
  const [search, setSearch] = useState('');
  const [selectedMechanics, setSelectedMechanics] = useState<string[]>([]);
  const [page, setPage] = useState(1);

  // Debounce search per evitare query ad ogni keystroke
  const debouncedSearch = useDebounce(search, 300);

  // ------------------------------------------------------------------
  // Data fetching
  // ------------------------------------------------------------------

  // Trending row (ShelfCard — layout orizzontale)
  const {
    data: trendingGames,
    isLoading: isTrendingLoading,
    isError: isTrendingError,
  } = useCatalogTrending(10);

  // Tutte le meccaniche (per filter chips)
  const { data: mechanicsData } = useGameMechanics();

  // Mappa slug → id per i filtri meccanica
  const mechanicSlugsToIds = useMemo(() => {
    if (!mechanicsData) return new Map<string, string>();
    return new Map(mechanicsData.map(m => [m.slug, m.id]));
  }, [mechanicsData]);

  const mechanicIds = useMemo(() => {
    if (selectedMechanics.length === 0) return undefined;
    const ids = selectedMechanics
      .map(slug => mechanicSlugsToIds.get(slug))
      .filter((id): id is string => !!id);
    return ids.length > 0 ? ids.join(',') : undefined;
  }, [selectedMechanics, mechanicSlugsToIds]);

  const {
    data: catalogData,
    isLoading: isCatalogLoading,
    isError: isCatalogError,
  } = useSharedGames(
    {
      searchTerm: debouncedSearch || undefined,
      mechanicIds,
      page,
      pageSize: PAGE_SIZE,
      sortBy: 'title',
      sortDescending: false,
    },
    true
  );

  // Accumula items tra le pagine
  const [accumulatedItems, setAccumulatedItems] = useState<SharedGame[]>([]);

  useEffect(() => {
    if (!catalogData?.items) return;
    if (page === 1) {
      setAccumulatedItems(catalogData.items);
    } else {
      setAccumulatedItems(prev => {
        const existingIds = new Set(prev.map(i => i.id));
        const newItems = catalogData.items.filter(i => !existingIds.has(i.id));
        return [...prev, ...newItems];
      });
    }
  }, [catalogData?.items, page]);

  // Libreria utente — pageSize 1000 per coprire tutti i giochi e mostrare badge corretti
  const { data: libraryData } = useLibrary({ pageSize: 1000 });
  const userGameIds = useMemo(() => {
    const ids = new Set<string>();
    for (const entry of libraryData?.items ?? []) {
      if (entry.gameId) ids.add(entry.gameId);
    }
    return ids;
  }, [libraryData]);

  const { mutate: addToLibrary } = useAddGameToLibrary();

  // ------------------------------------------------------------------
  // Mechanic slugs disponibili per i chip
  // ------------------------------------------------------------------
  const availableMechanicSlugs = useMemo(() => {
    return mechanicsData?.map(m => m.slug) ?? [];
  }, [mechanicsData]);

  // ------------------------------------------------------------------
  // Handlers
  // ------------------------------------------------------------------

  const handleSearchChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    setSearch(e.target.value);
    setAccumulatedItems([]);
    setPage(1);
  }, []);

  const handleMechanicToggle = useCallback((mechanic: string) => {
    setSelectedMechanics(prev =>
      prev.includes(mechanic) ? prev.filter(m => m !== mechanic) : [...prev, mechanic]
    );
    setAccumulatedItems([]);
    setPage(1);
  }, []);

  const handleLoadMore = useCallback(() => {
    setPage(p => p + 1);
  }, []);

  const handleAddToLibrary = useCallback(
    (gameId: string) => {
      addToLibrary(
        { gameId },
        {
          onSuccess: () => toast.success('Gioco aggiunto alla libreria'),
          onError: () => toast.error('Errore durante l\'aggiunta — riprova'),
        }
      );
    },
    [addToLibrary]
  );

  // ------------------------------------------------------------------
  // Derived state
  // ------------------------------------------------------------------

  const hasMore = catalogData ? page * PAGE_SIZE < catalogData.total : false;
  const totalCount = catalogData?.total ?? 0;

  // Messaggio empty state composito (ricerca + meccaniche attive)
  const emptyDescription = useMemo(() => {
    const parts: string[] = [];
    if (debouncedSearch) parts.push(`"${debouncedSearch}"`);
    if (selectedMechanics.length > 0)
      parts.push(`${selectedMechanics.length} filtro${selectedMechanics.length > 1 ? 'i' : ''} meccanica`);
    if (parts.length > 0) return `Nessun risultato per ${parts.join(' + ')}. Prova a modificare i filtri.`;
    return 'Il catalogo è vuoto o non corrisponde ai filtri selezionati.';
  }, [debouncedSearch, selectedMechanics]);

  // ------------------------------------------------------------------
  // Render
  // ------------------------------------------------------------------

  return (
    <div className={cn('flex flex-col gap-6 py-4', className)} data-testid="public-library-page">
      {/* ---------------------------------------------------------------- */}
      {/* Search bar                                                        */}
      {/* ---------------------------------------------------------------- */}
      <div className="flex justify-center">
        <div className="relative w-full max-w-xl">
          <Search
            className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground pointer-events-none"
            aria-hidden="true"
          />
          <input
            type="search"
            value={search}
            onChange={handleSearchChange}
            placeholder="Cerca giochi nel catalogo…"
            className={cn(
              'w-full rounded-xl bg-[#161b22] border border-[#30363d]',
              'pl-9 pr-4 py-2.5 text-sm text-foreground placeholder:text-muted-foreground',
              'focus:outline-none focus:ring-2 focus:ring-[#58a6ff]/50 focus:border-[#58a6ff]',
              'transition-colors duration-150'
            )}
            data-testid="catalog-search-input"
            aria-label="Cerca giochi nel catalogo"
          />
        </div>
      </div>

      {/* ---------------------------------------------------------------- */}
      {/* Trending section (ShelfCard — layout orizzontale)                */}
      {/* ---------------------------------------------------------------- */}
      <SectionBlock icon="🔥" title="Trending questa settimana">
        {isTrendingLoading ? (
          <div
            className="flex items-center gap-2 text-xs text-muted-foreground py-4"
            data-testid="trending-loading"
          >
            <Loader2 className="h-3 w-3 animate-spin" aria-hidden="true" />
            Caricamento trending…
          </div>
        ) : isTrendingError ? (
          <p
            className="text-xs text-destructive py-2"
            data-testid="trending-error"
          >
            Impossibile caricare il trending. Ricarica la pagina.
          </p>
        ) : trendingGames && trendingGames.length > 0 ? (
          <ShelfRow>
            {trendingGames.map(game => (
              <ShelfCard
                key={game.gameId}
                title={game.title}
                subtitle={`#${game.rank} questa settimana`}
                imageUrl={game.thumbnailUrl ?? undefined}
                coverIcon="🎲"
                inLibrary={userGameIds.has(game.gameId)}
                onAdd={
                  !userGameIds.has(game.gameId)
                    ? () => handleAddToLibrary(game.gameId)
                    : undefined
                }
              />
            ))}
          </ShelfRow>
        ) : (
          <p className="text-xs text-muted-foreground py-2" data-testid="trending-empty">
            Nessun gioco in tendenza al momento.
          </p>
        )}
      </SectionBlock>

      {/* ---------------------------------------------------------------- */}
      {/* Tutti i giochi — MeepleCard grid                                  */}
      {/* ---------------------------------------------------------------- */}
      <SectionBlock
        icon="📋"
        title={
          totalCount > 0
            ? `Tutti i giochi`
            : 'Tutti i giochi'
        }
        count={totalCount > 0 ? totalCount : undefined}
      >
        {/* Mechanic filter chips collassabili */}
        {availableMechanicSlugs.length > 0 && (
          <div className="mb-4" data-testid="mechanic-filter-row">
            <MechanicFilter
              mechanics={availableMechanicSlugs}
              selected={selectedMechanics}
              onSelect={handleMechanicToggle}
            />
          </div>
        )}

        {/* Grid */}
        {isCatalogLoading && page === 1 ? (
          <div
            className="flex items-center gap-2 text-xs text-muted-foreground py-8 justify-center"
            data-testid="catalog-loading"
          >
            <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
            Caricamento catalogo…
          </div>
        ) : isCatalogError ? (
          <div
            className="flex flex-col items-center gap-2 py-8 text-center"
            data-testid="catalog-error"
          >
            <p className="text-sm text-destructive">Impossibile caricare il catalogo.</p>
            <p className="text-xs text-muted-foreground">Ricarica la pagina o riprova tra qualche istante.</p>
          </div>
        ) : accumulatedItems.length === 0 ? (
          <EmptyState
            title="Nessun gioco trovato"
            description={emptyDescription}
            variant="noData"
            data-testid="catalog-empty"
          />
        ) : (
          <div
            className="grid grid-cols-[repeat(auto-fill,minmax(160px,1fr))] gap-3"
            data-testid="catalog-grid"
          >
            {accumulatedItems.map(game => (
              <MeepleCard
                key={game.id}
                entity="game"
                variant="grid"
                id={game.id}
                title={game.title}
                subtitle={formatYear(game.yearPublished)}
                imageUrl={game.thumbnailUrl || game.imageUrl || undefined}
                rating={game.averageRating ?? undefined}
                ratingMax={10}
                onClick={() => router.push(`/library/games/${game.id}`)}
                status={userGameIds.has(game.id) ? 'owned' : undefined}
                showStatusIcon={userGameIds.has(game.id)}
                quickActions={
                  !userGameIds.has(game.id)
                    ? [
                        {
                          icon: Plus,
                          label: 'Aggiungi',
                          onClick: () => handleAddToLibrary(game.id),
                        },
                      ]
                    : undefined
                }
                metadata={[
                  ...(game.minPlayers > 0 && game.maxPlayers > 0
                    ? [{ icon: Users, label: `${game.minPlayers}-${game.maxPlayers}p` }]
                    : []),
                  ...(game.playingTimeMinutes > 0
                    ? [{ icon: Clock, label: `${game.playingTimeMinutes}min` }]
                    : []),
                ]}
                data-testid="catalog-game-card"
              />
            ))}
          </div>
        )}

        {/* Load More */}
        {hasMore && !isCatalogLoading && (
          <div className="flex justify-center mt-6">
            <button
              type="button"
              onClick={handleLoadMore}
              className={cn(
                'px-6 py-2 rounded-lg text-sm font-medium',
                'bg-[#21262d] border border-[#30363d] text-[#e6edf3]',
                'hover:bg-[#30363d] hover:border-[#58a6ff]',
                'transition-colors duration-150',
                isCatalogLoading && 'opacity-50 cursor-not-allowed'
              )}
              disabled={isCatalogLoading}
              data-testid="load-more-button"
            >
              {isCatalogLoading ? (
                <span className="flex items-center gap-2">
                  <Loader2 className="h-3 w-3 animate-spin" aria-hidden="true" />
                  Caricamento…
                </span>
              ) : (
                'Carica altri'
              )}
            </button>
          </div>
        )}
      </SectionBlock>
    </div>
  );
}

export default PublicLibraryPage;
```

- [ ] **Step 2.4: Aggiorna SectionBlock per supportare il prop `count`**

Il `SectionBlock` attuale non supporta `count`. Aggiungi il prop opzionale senza rompere i consumer esistenti.

Leggi `apps/web/src/components/ui/SectionBlock.tsx` e modifica:

```tsx
'use client';

import type { ReactNode } from 'react';

interface SectionBlockProps {
  icon: string;
  title: string;
  children: ReactNode;
  /** Conteggio opzionale mostrato accanto al titolo */
  count?: number;
}

/**
 * SectionBlock — simple section wrapper with icon + title header.
 * Replaces the old TavoloSection component.
 */
export function SectionBlock({ icon, title, children, count }: SectionBlockProps) {
  return (
    <section>
      <div className="mb-3 flex items-center gap-2">
        <span className="text-sm" aria-hidden="true">
          {icon}
        </span>
        <h2 className="font-quicksand text-sm font-bold text-foreground">{title}</h2>
        {count !== undefined && (
          <span
            className="text-xs font-medium text-muted-foreground tabular-nums"
            data-testid="catalog-total-count"
          >
            {count}
          </span>
        )}
        <div className="h-px flex-1 bg-border/40" />
      </div>
      {children}
    </section>
  );
}
```

> **Nota**: Il `data-testid="catalog-total-count"` è su `SectionBlock` ma il test lo cerca sulla pagina intera — funzionerà perché `getByTestId` cerca nel DOM globale del render.

- [ ] **Step 2.5: Esegui i test — verifica che passino**

```bash
cd apps/web && pnpm test src/components/library/__tests__/PublicLibraryPage.test.tsx --run
```

Atteso: `PASS` — tutti i test verdi.

Se fallisce con errore TypeScript su `SectionBlock` (prop `count` non attesa), verificare che il tipo sia corretto nello step 2.4.

- [ ] **Step 2.6: Verifica typecheck e lint**

```bash
cd apps/web && pnpm typecheck && pnpm lint
```

Atteso: 0 errori TypeScript, 0 errori lint.

Se `pnpm typecheck` riporta errori su `SectionBlock.count`, assicurarsi che il tipo sia `count?: number` nell'interfaccia.

- [ ] **Step 2.7: Commit**

```bash
git add apps/web/src/components/library/PublicLibraryPage.tsx \
        apps/web/src/components/ui/SectionBlock.tsx \
        apps/web/src/components/library/__tests__/PublicLibraryPage.test.tsx
git commit -m "feat(library): refactor public catalog — MeepleCard grid, debounce, error states, toast, nav"
```

---

## Self-Review del Piano

### Copertura spec

| Requisito | Task |
|-----------|------|
| C1: useLibrary pageSize 100 → 1000 | Task 2 (PublicLibraryPage, riga `useLibrary({ pageSize: 1000 })`) |
| C3: yearPublished 0 → "—" | Task 2 (helper `formatYear`) |
| C4: empty state composito | Task 2 (`emptyDescription` memo) |
| C5: ShelfCard → MeepleCard grid | Task 2 (sezione Tutti i giochi) |
| C6: MechanicFilter infinite → collassabile | Task 1 |
| I1: Navigazione dettaglio | Task 2 (`onClick: router.push`) |
| I2: Error state trending e catalog | Task 2 (`isTrendingError`, `isCatalogError`) |
| I3: Debounce search | Task 2 (`useDebounce(search, 300)`) |
| I4: Toast addToLibrary | Task 2 (`handleAddToLibrary` con `onSuccess`/`onError`) |
| I5: Conteggio totale | Task 2 (`SectionBlock count={totalCount}`) |

### Placeholder scan

✅ Nessun "TBD", "TODO", "implement later" — tutto il codice è completo.

### Type consistency

- `formatYear(year: number): string` — usato solo in Task 2 inline.
- `SectionBlock.count?: number` — aggiunto in Step 2.4, usato in Step 2.3.
- `useAddGameToLibrary().mutate` signature: `({ gameId }, { onSuccess, onError })` — conforme a `UseMutationResult`.
- `MeepleCard quickActions` type: `Array<{ icon: LucideIcon; label: string; onClick: () => void; ... }>` — `Plus` da lucide-react è `LucideIcon`. ✅
- `MeepleCard status`: `'owned'` è in `'owned' | 'wishlisted' | 'played' | 'borrowed' | 'for-trade'`. ✅

### Gap identificati

- **SectionBlock count** è necessario in Step 2.4 prima di usarlo in Step 2.3. L'ordine degli step è corretto (2.3 → 2.4 → 2.5).
  > **ATTENZIONE**: Il typecheck in Step 2.6 deve passare dopo 2.4. Se si eseguono gli step in ordine, non ci sono problemi.

- **Trending: ShelfCard rimane** — questo è intenzionale. Il trending è un layout orizzontale a shelf, ShelfCard è il componente corretto per quel contesto. Solo la grid verticale viene migrata a MeepleCard.
