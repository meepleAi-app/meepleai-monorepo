/**
 * PublicLibraryPage Component
 *
 * Browse experience for the shared game catalog.
 * Features:
 *   1. Centered search bar
 *   2. Trending section — horizontal ShelfRow with TavoloSection
 *   3. All games section — MechanicFilter chips + grid of ShelfCards
 *   4. Load More button for pagination
 *
 * Library membership is checked against the user's own library so each
 * ShelfCard can show "In libreria" or an "Aggiungi" button.
 */

'use client';

import { useState, useMemo, useCallback, useEffect } from 'react';

import { Search, Loader2 } from 'lucide-react';

import { TavoloSection } from '@/components/dashboard-v2/tavolo';
import { EmptyState } from '@/components/empty-state/EmptyState';
import { MechanicFilter } from '@/components/library/MechanicFilter';
import { ShelfCard } from '@/components/library/ShelfCard';
import { ShelfRow } from '@/components/library/ShelfRow';
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
// Component
// ============================================================================

/**
 * PublicLibraryPage — catalog browse page with trending, mechanic filters,
 * paginated grid, and add-to-library actions.
 */
export function PublicLibraryPage({ className }: PublicLibraryPageProps) {
  // ------------------------------------------------------------------
  // Local state
  // ------------------------------------------------------------------
  const [search, setSearch] = useState('');
  const [selectedMechanics, setSelectedMechanics] = useState<string[]>([]);
  const [page, setPage] = useState(1);

  // ------------------------------------------------------------------
  // Data fetching
  // ------------------------------------------------------------------

  // Trending row
  const { data: trendingGames, isLoading: isTrendingLoading } = useCatalogTrending(10);

  // All mechanics (for filter chips)
  const { data: mechanicsData } = useGameMechanics();

  // Catalog grid with search + mechanic filtering
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

  const { data: catalogData, isLoading: isCatalogLoading } = useSharedGames(
    {
      searchTerm: search || undefined,
      mechanicIds,
      page,
      pageSize: PAGE_SIZE,
      sortBy: 'title',
      sortDescending: false,
    },
    true
  );

  // Accumulate items across pages using state (avoids ref mutation in render)
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

  // User's library — for "inLibrary" badge
  const { data: libraryData } = useLibrary({ pageSize: 100 });
  const userGameIds = useMemo(() => {
    const ids = new Set<string>();
    for (const entry of libraryData?.items ?? []) {
      if (entry.gameId) ids.add(entry.gameId);
    }
    return ids;
  }, [libraryData]);

  const { mutate: addToLibrary } = useAddGameToLibrary();

  // ------------------------------------------------------------------
  // Available mechanic slugs for filter chips (only what catalog uses)
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

  // ------------------------------------------------------------------
  // Derived state
  // ------------------------------------------------------------------

  const hasMore = catalogData ? page * PAGE_SIZE < catalogData.total : false;

  const displayItems = accumulatedItems;

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
      <TavoloSection icon="🔥" title="Trending questa settimana">
        {isTrendingLoading ? (
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
                  !userGameIds.has(game.gameId)
                    ? () => addToLibrary({ gameId: game.gameId })
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
      </TavoloSection>

      {/* ---------------------------------------------------------------- */}
      {/* All games section                                                 */}
      {/* ---------------------------------------------------------------- */}
      <TavoloSection icon="📋" title="Tutti i giochi">
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

        {/* Grid */}
        {isCatalogLoading && page === 1 ? (
          <div
            className="flex items-center gap-2 text-xs text-muted-foreground py-8 justify-center"
            data-testid="catalog-loading"
          >
            <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
            Caricamento catalogo…
          </div>
        ) : displayItems.length === 0 ? (
          <EmptyState
            title="Nessun gioco trovato"
            description={
              search
                ? `Nessun risultato per "${search}". Prova un altro termine.`
                : 'Il catalogo è vuoto o non corrisponde ai filtri selezionati.'
            }
            variant="noData"
            data-testid="catalog-empty"
          />
        ) : (
          <div className="flex flex-wrap gap-3" data-testid="catalog-grid">
            {displayItems.map(game => (
              <ShelfCard
                key={game.id}
                title={game.title}
                subtitle={String(game.yearPublished)}
                imageUrl={game.thumbnailUrl || game.imageUrl || undefined}
                coverIcon="🎲"
                inLibrary={userGameIds.has(game.id)}
                onAdd={
                  !userGameIds.has(game.id) ? () => addToLibrary({ gameId: game.id }) : undefined
                }
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
      </TavoloSection>
    </div>
  );
}

export default PublicLibraryPage;
