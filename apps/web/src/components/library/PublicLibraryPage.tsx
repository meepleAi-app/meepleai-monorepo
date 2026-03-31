/**
 * PublicLibraryPage Component
 *
 * Browse experience for the shared game catalog.
 * Features:
 *   1. Centered search bar (debounced 300ms)
 *   2. Trending section — horizontal ShelfRow with SectionBlock
 *   3. All games section — MechanicFilter chips (collapsible) + MeepleCard grid
 *   4. Load More button for pagination
 *
 * Library membership is checked against the user's own library (pageSize 1000)
 * so each MeepleCard can show "owned" status or an "Aggiungi" quick action.
 */

'use client';

import { useState, useMemo, useCallback, useEffect } from 'react';

import { Plus, Search, Loader2 } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { EmptyState } from '@/components/empty-state/EmptyState';
import { toast } from '@/components/layout';
import { MechanicFilter } from '@/components/library/MechanicFilter';
import { ShelfCard } from '@/components/library/ShelfCard';
import { ShelfRow } from '@/components/library/ShelfRow';
import { useDebounce } from '@/components/ui/data-display/entity-list-view/hooks/use-debounce';
import { MeepleCard } from '@/components/ui/data-display/meeple-card/MeepleCard';
import { SectionBlock } from '@/components/ui/SectionBlock';
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
// Helpers
// ============================================================================

/** Returns the year as string, or "—" when yearPublished is 0 or falsy */
function formatYear(year: number | null | undefined): string {
  return year && year > 0 ? String(year) : '—';
}

// ============================================================================
// Props
// ============================================================================

export interface PublicLibraryPageProps {
  className?: string;
}

// ============================================================================
// Component
// ============================================================================

/**
 * PublicLibraryPage — catalog browse page with trending, mechanic filters,
 * paginated MeepleCard grid, and add-to-library actions.
 */
export function PublicLibraryPage({ className }: PublicLibraryPageProps) {
  const router = useRouter();

  // ------------------------------------------------------------------
  // Local state
  // ------------------------------------------------------------------
  const [search, setSearch] = useState('');
  const [selectedMechanics, setSelectedMechanics] = useState<string[]>([]);
  const [page, setPage] = useState(1);

  // Debounce search input so the query fires only after 300ms of inactivity
  const debouncedSearch = useDebounce(search, 300);

  // ------------------------------------------------------------------
  // Data fetching
  // ------------------------------------------------------------------

  // Trending row
  const {
    data: trendingGames,
    isLoading: isTrendingLoading,
    isError: isTrendingError,
  } = useCatalogTrending(10);

  // All mechanics (for filter chips)
  const { data: mechanicsData } = useGameMechanics();

  // Map slug → id for filter conversion
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

  // Catalog grid — use debouncedSearch to avoid query-per-keystroke
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

  // Accumulate items across pages
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

  // User's library — pageSize 1000 to reliably catch all owned games
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
  // Available mechanic slugs for filter chips
  // ------------------------------------------------------------------
  const availableMechanicSlugs = useMemo(() => {
    return mechanicsData?.map(m => m.slug) ?? [];
  }, [mechanicsData]);

  // ------------------------------------------------------------------
  // Handlers
  // ------------------------------------------------------------------

  const handleSearchChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    setSearch(e.target.value);
    setPage(1);
  }, []);

  const handleMechanicToggle = useCallback((mechanic: string) => {
    setSelectedMechanics(prev =>
      prev.includes(mechanic) ? prev.filter(m => m !== mechanic) : [...prev, mechanic]
    );
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
          onError: () => toast.error('Impossibile aggiungere il gioco'),
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

  const emptyDescription = useMemo(() => {
    const parts: string[] = [];
    if (debouncedSearch) parts.push(`"${debouncedSearch}"`);
    if (selectedMechanics.length > 0)
      parts.push(
        `${selectedMechanics.length} meccanica${selectedMechanics.length > 1 ? 'he' : ''}`
      );
    if (parts.length > 0) return `Nessun risultato per ${parts.join(' e ')}. Prova altri filtri.`;
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
      {/* Trending section                                                  */}
      {/* ---------------------------------------------------------------- */}
      <SectionBlock icon="🔥" title="Trending questa settimana">
        {isTrendingError ? (
          <p className="text-xs text-destructive py-2" data-testid="trending-error">
            Impossibile caricare i giochi in tendenza.
          </p>
        ) : isTrendingLoading ? (
          <div
            className="flex items-center gap-2 text-xs text-muted-foreground py-4"
            data-testid="trending-loading"
          >
            <Loader2 className="h-3 w-3 animate-spin" aria-hidden="true" />
            Caricamento trending…
          </div>
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
                  !userGameIds.has(game.gameId) ? () => handleAddToLibrary(game.gameId) : undefined
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
      {/* All games section                                                 */}
      {/* ---------------------------------------------------------------- */}
      <SectionBlock
        icon="📋"
        title="Tutti i giochi"
        count={totalCount}
        countTestId="catalog-total-count"
      >
        {/* Mechanic filter chips */}
        {availableMechanicSlugs.length > 0 && (
          <div className="mb-4" data-testid="mechanic-filter-row">
            <MechanicFilter
              mechanics={availableMechanicSlugs}
              selected={selectedMechanics}
              onSelect={handleMechanicToggle}
            />
          </div>
        )}

        {/* Error state */}
        {isCatalogError ? (
          <p className="text-xs text-destructive py-4 text-center" data-testid="catalog-error">
            Impossibile caricare il catalogo. Riprova più tardi.
          </p>
        ) : isCatalogLoading && page === 1 ? (
          <div
            className="flex items-center gap-2 text-xs text-muted-foreground py-8 justify-center"
            data-testid="catalog-loading"
          >
            <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
            Caricamento catalogo…
          </div>
        ) : accumulatedItems.length === 0 ? (
          <EmptyState
            title="Nessun gioco trovato"
            description={emptyDescription}
            variant="noData"
            testId="catalog-empty"
          />
        ) : (
          <div
            className="grid grid-cols-[repeat(auto-fill,minmax(160px,1fr))] gap-3"
            data-testid="catalog-grid"
          >
            {accumulatedItems.map(game => {
              const inLibrary = userGameIds.has(game.id);
              return (
                <MeepleCard
                  key={game.id}
                  entity="game"
                  variant="grid"
                  data-testid="catalog-game-card"
                  title={game.title}
                  subtitle={formatYear(game.yearPublished)}
                  imageUrl={game.thumbnailUrl || game.imageUrl || undefined}
                  rating={game.averageRating ?? undefined}
                  ratingMax={10}
                  status={inLibrary ? 'owned' : undefined}
                  showStatusIcon={inLibrary}
                  onClick={() => router.push(`/library/games/${game.id}`)}
                  entityQuickActions={
                    !inLibrary
                      ? [
                          {
                            icon: Plus,
                            label: 'Aggiungi',
                            onClick: () => handleAddToLibrary(game.id),
                          },
                        ]
                      : undefined
                  }
                />
              );
            })}
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
                'transition-colors duration-150'
              )}
              data-testid="load-more-button"
            >
              Carica altri
            </button>
          </div>
        )}
      </SectionBlock>
    </div>
  );
}

export default PublicLibraryPage;
