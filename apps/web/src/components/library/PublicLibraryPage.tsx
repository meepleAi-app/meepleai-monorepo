/**
 * PublicLibraryPage Component
 *
 * Browse experience for the shared game catalog.
 * Features:
 *   1. Centered search bar (debounced 300ms)
 *   2. Trending section — horizontal ShelfRow with MeepleCard
 *   3. Mechanic filter button + Popover with checkboxes + active chips
 *   4. All games section — responsive MeepleCard grid
 *   5. Load More button for pagination
 *
 * Library membership is checked against the user's own library (pageSize 1000)
 * so each MeepleCard can show "owned" status or an "Aggiungi" quick action.
 */

'use client';

import { useState, useMemo, useCallback, useEffect } from 'react';

import { ChevronDown, Filter, Loader2, Plus, Search, X } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { EmptyState } from '@/components/empty-state/EmptyState';
import { MechanicIcon } from '@/components/icons/mechanics';
import { toast } from '@/components/layout';
import { ShelfRow } from '@/components/library/ShelfRow';
import { mapSharedGameToLinkedEntities } from '@/components/library/mapSharedGameLinkedEntities';
import { MeepleCard } from '@/components/ui/data-display/meeple-card/MeepleCard';
import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/overlays/popover';
import { SectionBlock } from '@/components/ui/SectionBlock';
import { useCatalogTrending } from '@/hooks/queries/useCatalogTrending';
import { useAddGameToLibrary, useLibrary } from '@/hooks/queries/useLibrary';
import { useSharedGames, useGameMechanics } from '@/hooks/queries/useSharedGames';
import { useDebounce } from '@/hooks/useDebounce';
import type { SharedGame } from '@/lib/api/schemas/shared-games.schemas';
import { cn } from '@/lib/utils';

// ============================================================================
// Constants
// ============================================================================

const PAGE_SIZE = 18;

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
 * PublicLibraryPage — catalog browse page with trending, mechanic filter popover,
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

  const debouncedSearch = useDebounce(search, 300);

  // ------------------------------------------------------------------
  // Data fetching
  // ------------------------------------------------------------------

  const {
    data: trendingGames,
    isLoading: isTrendingLoading,
    isError: isTrendingError,
  } = useCatalogTrending(10);

  const { data: mechanicsData } = useGameMechanics();

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

  const availableMechanicSlugs = useMemo(() => {
    return mechanicsData?.map(m => m.slug) ?? [];
  }, [mechanicsData]);

  const selectedMechanicsSet = useMemo(() => new Set(selectedMechanics), [selectedMechanics]);

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

  const handleClearMechanics = useCallback(() => {
    setSelectedMechanics([]);
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

  /** KB pip on shared games shows a toast instead of opening KbBottomSheet */
  const handleSharedGamePipClick = useCallback((entityType: MeepleEntityType) => {
    if (entityType === 'kb') {
      toast.info("Aggiungi alla tua libreria per chattare con l'agente");
    }
  }, []);

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
      {/* Trending section — MeepleCard in horizontal ShelfRow              */}
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
            {trendingGames.map(game => {
              const inLibrary = userGameIds.has(game.gameId);
              return (
                <div key={game.gameId} className="w-[160px] flex-shrink-0">
                  <MeepleCard
                    entity="game"
                    variant="grid"
                    title={game.title}
                    subtitle={`#${game.rank} trending`}
                    imageUrl={game.thumbnailUrl ?? undefined}
                    status={inLibrary ? 'owned' : undefined}
                    showStatusIcon={inLibrary}
                    onClick={() => router.push(`/library/games/${game.gameId}`)}
                    entityQuickActions={
                      !inLibrary
                        ? [
                            {
                              icon: Plus,
                              label: 'Aggiungi',
                              onClick: () => handleAddToLibrary(game.gameId),
                            },
                          ]
                        : undefined
                    }
                  />
                </div>
              );
            })}
          </ShelfRow>
        ) : (
          <p className="text-xs text-muted-foreground py-2" data-testid="trending-empty">
            Nessun gioco in tendenza al momento.
          </p>
        )}
      </SectionBlock>

      {/* ---------------------------------------------------------------- */}
      {/* Mechanic filter — Popover button + active chips                   */}
      {/* ---------------------------------------------------------------- */}
      {availableMechanicSlugs.length > 0 && (
        <div className="flex flex-wrap items-center gap-2" data-testid="mechanic-filter-row">
          <Popover>
            <PopoverTrigger asChild>
              <button
                type="button"
                className={cn(
                  'flex items-center gap-2 rounded-lg px-3 py-2 text-sm font-medium transition-colors',
                  'bg-[#21262d] border border-[#30363d] text-[#e6edf3]',
                  'hover:bg-[#30363d] hover:border-[#58a6ff]',
                  selectedMechanics.length > 0 && 'border-[#f0a030] text-[#f0a030]'
                )}
                aria-label="Filtra per meccanica"
              >
                <Filter className="h-4 w-4" aria-hidden="true" />
                Meccaniche
                {selectedMechanics.length > 0 && (
                  <span className="flex h-5 w-5 items-center justify-center rounded-full bg-[#f0a030] text-[10px] font-bold text-[#0d1117]">
                    {selectedMechanics.length}
                  </span>
                )}
                <ChevronDown className="h-3 w-3 opacity-60" aria-hidden="true" />
              </button>
            </PopoverTrigger>

            <PopoverContent align="start" className="w-64 p-0">
              {/* Header */}
              <div className="flex items-center justify-between border-b border-border/50 px-3 py-2">
                <span className="text-sm font-medium">Filtra per meccanica</span>
                {selectedMechanics.length > 0 && (
                  <button
                    type="button"
                    onClick={handleClearMechanics}
                    className="text-xs text-muted-foreground hover:text-foreground transition-colors"
                  >
                    Pulisci tutto
                  </button>
                )}
              </div>

              {/* Mechanic list */}
              <div className="max-h-64 overflow-y-auto p-2">
                {availableMechanicSlugs.map(mechanic => {
                  const isChecked = selectedMechanicsSet.has(mechanic);
                  return (
                    <label
                      key={mechanic}
                      className={cn(
                        'flex cursor-pointer items-center gap-2.5 rounded-md px-2 py-1.5 transition-colors',
                        'hover:bg-muted/50',
                        isChecked && 'bg-[#f0a030]/10'
                      )}
                    >
                      <input
                        type="checkbox"
                        checked={isChecked}
                        onChange={() => handleMechanicToggle(mechanic)}
                        className="h-3.5 w-3.5 rounded accent-[#f0a030]"
                      />
                      <MechanicIcon mechanic={mechanic} size={14} />
                      <span className="text-sm">{MECHANIC_LABELS[mechanic] ?? mechanic}</span>
                    </label>
                  );
                })}
              </div>
            </PopoverContent>
          </Popover>

          {/* Active filter chips — dismissible */}
          {selectedMechanics.map(m => (
            <button
              key={m}
              type="button"
              onClick={() => handleMechanicToggle(m)}
              className={cn(
                'flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-medium',
                'bg-[#f0a030]/20 text-[#f0a030] border border-[#f0a030]/40',
                'hover:bg-[#f0a030]/30 transition-colors'
              )}
              aria-label={`Rimuovi filtro ${MECHANIC_LABELS[m] ?? m}`}
            >
              {MECHANIC_LABELS[m] ?? m}
              <X className="h-3 w-3" aria-hidden="true" />
            </button>
          ))}
        </div>
      )}

      {/* ---------------------------------------------------------------- */}
      {/* All games — responsive MeepleCard grid                            */}
      {/* ---------------------------------------------------------------- */}
      <SectionBlock
        icon="📋"
        title="Tutti i giochi"
        count={totalCount}
        countTestId="catalog-total-count"
      >
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
            className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-3"
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
                  linkedEntities={mapSharedGameToLinkedEntities(game)}
                  onManaPipClick={handleSharedGamePipClick}
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
