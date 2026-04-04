/**
 * CollectionDashboard - User Game Collection Management
 * Issue #3632 - EPIC #3475: User Private Library & Collections Management
 *
 * A comprehensive dashboard for managing user's game collection with:
 * - Hero stats section with key metrics
 * - Advanced filtering and search
 * - Grid/List view toggle
 * - Quick actions (add game, create collection)
 * - Responsive design with "Warm Tabletop" aesthetic
 *
 * @example
 * ```tsx
 * <CollectionDashboard />
 * ```
 */

'use client';

import { useState, useMemo, useCallback } from 'react';

import { Dices, Heart, Library, TrendingUp } from 'lucide-react';

import { type CarouselGame } from '@/components/ui/data-display/game-carousel';
import { useLibrary, useLibraryStats, useLibraryQuota } from '@/hooks/queries/useLibrary';
import type { GameStateType, GetUserLibraryParams } from '@/lib/api/schemas/library.schemas';
import { COLLECTION_TEST_IDS } from '@/lib/test-ids';
import { cn } from '@/lib/utils';

import { CollectionGameGrid } from './CollectionGameGrid';
import { CollectionHeroStats, type HeroStat } from './CollectionHeroStats';
import { CollectionToolbar, type ViewMode, type SortOption } from './CollectionToolbar';

// ============================================================================
// Types
// ============================================================================

export interface CollectionDashboardProps {
  /** Additional className */
  className?: string;
}

// ============================================================================
// Main Component
// ============================================================================

export function CollectionDashboard({ className }: CollectionDashboardProps) {
  // Local state
  const [viewMode, setViewMode] = useState<ViewMode>('grid');
  const [searchQuery, setSearchQuery] = useState('');
  const [favoritesOnly, setFavoritesOnly] = useState(false);
  const [stateFilter, setStateFilter] = useState<GameStateType[]>([]);
  const [sortBy, setSortBy] = useState<SortOption>('addedAt');
  const [sortDescending, setSortDescending] = useState(true);
  const [page, setPage] = useState(1);
  const pageSize = 20;

  // Query params
  const queryParams: GetUserLibraryParams = useMemo(
    () => ({
      page,
      pageSize,
      search: searchQuery || undefined,
      favoritesOnly: favoritesOnly || undefined,
      stateFilter: stateFilter.length > 0 ? stateFilter : undefined,
      sortBy: sortBy === 'playCount' ? 'addedAt' : sortBy, // API doesn't support playCount yet
      sortDescending,
    }),
    [page, pageSize, searchQuery, favoritesOnly, stateFilter, sortBy, sortDescending]
  );

  // Data fetching
  const {
    data: libraryData,
    isLoading: isLoadingLibrary,
    error: libraryError,
  } = useLibrary(queryParams);
  const { data: statsData, isLoading: isLoadingStats } = useLibraryStats();
  const { data: quotaData } = useLibraryQuota();

  // Derived state
  const heroStats: HeroStat[] = useMemo(() => {
    if (!statsData) return [];
    return [
      { id: 'total', icon: Library, value: statsData.totalGames, label: 'Giochi', color: 'amber' },
      {
        id: 'favorites',
        icon: Heart,
        value: statsData.favoriteGames,
        label: 'Preferiti',
        color: 'purple',
      },
      {
        id: 'quota',
        icon: TrendingUp,
        value: quotaData?.remainingSlots ?? 0,
        label: 'Slot liberi',
        color: 'teal',
      },
      {
        id: 'usage',
        icon: Dices,
        value: quotaData?.currentCount ?? statsData.totalGames,
        label: 'In uso',
        color: 'emerald',
      },
    ];
  }, [statsData, quotaData]);

  const stateCounts = useMemo(() => {
    if (!statsData) return undefined;
    return {
      total: statsData.totalGames,
      favorites: statsData.favoriteGames,
      nuovo: statsData.nuovoCount ?? 0,
      inPrestito: statsData.inPrestitoCount ?? 0,
      wishlist: statsData.wishlistCount ?? 0,
      owned: statsData.ownedCount ?? 0,
    };
  }, [statsData]);

  const hasActiveFilters = Boolean(searchQuery) || favoritesOnly || stateFilter.length > 0;

  // Handlers
  const handleSortChange = useCallback((newSortBy: SortOption, descending: boolean) => {
    setSortBy(newSortBy);
    setSortDescending(descending);
    setPage(1);
  }, []);

  const handleClearFilters = useCallback(() => {
    setSearchQuery('');
    setFavoritesOnly(false);
    setStateFilter([]);
    setSortBy('addedAt');
    setSortDescending(true);
    setPage(1);
  }, []);

  const handleSearchChange = useCallback((query: string) => {
    setSearchQuery(query);
    setPage(1);
  }, []);

  const handleFavoritesChange = useCallback((enabled: boolean) => {
    setFavoritesOnly(enabled);
    setPage(1);
  }, []);

  const handleStateFilterChange = useCallback((states: GameStateType[]) => {
    setStateFilter(states);
    setPage(1);
  }, []);

  // Map library entries to CollectionGame format
  const games = useMemo(() => {
    if (!libraryData?.items) return [];
    return libraryData.items.map(entry => {
      const stateToStatus: Record<string, 'owned' | 'wishlisted' | 'borrowed'> = {
        Owned: 'owned',
        Nuovo: 'owned',
        Wishlist: 'wishlisted',
        InPrestito: 'borrowed',
      };
      return {
        id: entry.gameId,
        title: entry.gameTitle,
        thumbnailUrl: entry.gameIconUrl ?? undefined,
        imageUrl: entry.gameImageUrl ?? undefined,
        yearPublished: entry.gameYearPublished ?? undefined,
        addedAt: entry.addedAt,
        playCount: 0,
        hasPdf: entry.hasKb,
        hasActiveChat: false,
        chatCount: 0,
        status: stateToStatus[entry.currentState] ?? 'owned',
      };
    });
  }, [libraryData]);

  const carouselGames: CarouselGame[] = useMemo(() => {
    if (!libraryData?.items) return [];
    return libraryData.items.map(entry => ({
      id: entry.gameId,
      title: entry.gameTitle,
      subtitle: entry.gamePublisher ?? undefined,
      imageUrl: entry.gameImageUrl ?? entry.gameIconUrl ?? undefined,
      hasKb: entry.hasKb,
    }));
  }, [libraryData]);

  return (
    <div className={cn('space-y-6', className)} data-testid={COLLECTION_TEST_IDS.dashboard}>
      <CollectionHeroStats heroStats={heroStats} isLoading={isLoadingStats} />

      <CollectionToolbar
        searchQuery={searchQuery}
        onSearchChange={handleSearchChange}
        viewMode={viewMode}
        onViewModeChange={setViewMode}
        sortBy={sortBy}
        sortDescending={sortDescending}
        onSortChange={handleSortChange}
        favoritesOnly={favoritesOnly}
        onFavoritesChange={handleFavoritesChange}
        stateFilter={stateFilter}
        onStateFilterChange={handleStateFilterChange}
        onClearFilters={handleClearFilters}
        stateCounts={stateCounts}
      />

      <CollectionGameGrid
        games={games}
        carouselGames={carouselGames}
        viewMode={viewMode}
        isLoading={isLoadingLibrary}
        hasError={!!libraryError}
        hasActiveFilters={hasActiveFilters}
        totalCount={libraryData?.totalCount}
        totalPages={libraryData?.totalPages}
        page={page}
        onPageChange={setPage}
      />
    </div>
  );
}

export default CollectionDashboard;
