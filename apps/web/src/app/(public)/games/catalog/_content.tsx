'use client';

/**
 * Shared Games Catalog Content (Issue #2518, #2876)
 *
 * Client component for browsing shared games available to add to library.
 * Displays community-curated game catalog with advanced filtering.
 */

import { useMemo, useCallback } from 'react';

import { AlertCircle, Library, Star, Clock } from 'lucide-react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';

import { CatalogFilters } from '@/components/catalog/CatalogFilters';
import { CatalogPagination } from '@/components/catalog/CatalogPagination';
import {
  MeepleGameCatalogCard,
  MeepleGameCatalogCardSkeleton,
} from '@/components/catalog/MeepleGameCatalogCard';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';
import { Button } from '@/components/ui/primitives/button';
import { useSharedGames, useGameCategories, useGameMechanics } from '@/hooks/queries';
import { useCatalogSearchParams, type SortByField } from '@/hooks/useCatalogSearchParams';
import type { SearchSharedGamesParams } from '@/lib/api';

/**
 * Inner component that uses useSearchParams (requires Suspense boundary)
 */
interface CatalogContentProps {
  gameDetailBasePath?: string;
}

export function CatalogContent({ gameDetailBasePath = '/games' }: CatalogContentProps) {
  const router = useRouter();

  // URL-synced filter and pagination state (#2876)
  const { params, setParams, resetParams, setPage } = useCatalogSearchParams();

  // Build query params for API from URL params
  const queryParams = useMemo<SearchSharedGamesParams>(() => {
    const apiParams: SearchSharedGamesParams = {
      page: params.page,
      pageSize: params.pageSize,
      sortBy: params.sortBy,
      sortDescending: params.sortDescending,
    };

    if (params.searchTerm) apiParams.searchTerm = params.searchTerm;
    if (params.categoryIds.length > 0) apiParams.categoryIds = params.categoryIds.join(',');
    if (params.mechanicIds.length > 0) apiParams.mechanicIds = params.mechanicIds.join(',');
    if (params.minPlayers !== undefined) apiParams.minPlayers = params.minPlayers;
    if (params.maxPlayers !== undefined) apiParams.maxPlayers = params.maxPlayers;
    if (params.maxPlayingTime !== undefined) apiParams.maxPlayingTime = params.maxPlayingTime;

    return apiParams;
  }, [params]);

  // Fetch data
  const { data: gamesData, isLoading, error } = useSharedGames(queryParams);
  const { data: categories = [] } = useGameCategories();
  const { data: mechanics = [] } = useGameMechanics();

  // Hero section: Top 5 by BGG Rating
  const { data: topRatedData, isLoading: topRatedLoading } = useSharedGames({
    sortBy: 'AverageRating',
    sortDescending: true,
    pageSize: 5,
    page: 1,
  });

  // Hero section: Latest 5 Added
  const { data: latestAddedData, isLoading: latestAddedLoading } = useSharedGames({
    sortBy: 'CreatedAt',
    sortDescending: true,
    pageSize: 5,
    page: 1,
  });

  const topRatedGames = topRatedData?.items || [];
  const latestAddedGames = latestAddedData?.items || [];

  const games = gamesData?.items || [];
  const total = gamesData?.total || 0;
  const totalPages = gamesData ? Math.ceil(total / params.pageSize) : 0;

  // Navigate to game detail page
  const handleGameClick = useCallback(
    (gameId: string) => {
      router.push(`${gameDetailBasePath}/${gameId}`);
    },
    [router, gameDetailBasePath]
  );

  // Handlers - update URL params (resets page to 1 for filter changes)
  const handleSearch = (term: string) => {
    setParams({ searchTerm: term, page: 1 });
  };

  const handleCategoryChange = (categoryIds: string[]) => {
    setParams({ categoryIds, page: 1 });
  };

  const handleMechanicChange = (mechanicIds: string[]) => {
    setParams({ mechanicIds, page: 1 });
  };

  const handlePlayersChange = (min?: number, max?: number) => {
    setParams({ minPlayers: min, maxPlayers: max, page: 1 });
  };

  const handlePlaytimeChange = (max?: number) => {
    setParams({ maxPlayingTime: max, page: 1 });
  };

  const handleSortChange = (
    sortBy: 'title' | 'complexity' | 'playingTime',
    descending: boolean
  ) => {
    setParams({ sortBy: sortBy as SortByField, sortDescending: descending });
  };

  // Map URL sortBy to CatalogFilters expected type
  const getFilterSortBy = (): 'title' | 'complexity' | 'playingTime' => {
    if (
      params.sortBy === 'title' ||
      params.sortBy === 'complexity' ||
      params.sortBy === 'playingTime'
    ) {
      return params.sortBy;
    }
    return 'title';
  };

  return (
    <div className="min-h-screen bg-background pb-24 md:pb-0 md:pt-16">
      <div className="container mx-auto px-4 py-8 max-w-7xl">
        {/* Header */}
        <div className="flex flex-col md:flex-row justify-between items-start md:items-center mb-8 gap-4">
          <div>
            <h1 className="font-heading text-3xl md:text-4xl font-bold mb-2">
              Catalogo Giochi Condivisi
            </h1>
            <p className="text-muted-foreground">
              Esplora {total} giochi disponibili. Aggiungi alla tua libreria per iniziare a
              chattare.
            </p>
          </div>
          <Link href="/library">
            <Button variant="outline">
              <Library className="mr-2 h-4 w-4" />
              La Mia Libreria
            </Button>
          </Link>
        </div>

        {/* Hero Section: Top Rated + Latest Added */}
        <div className="mb-8 space-y-6">
          {/* Top 5 by BGG Rating */}
          <section>
            <div className="flex items-center gap-2 mb-4">
              <Star className="h-5 w-5 text-yellow-500 fill-yellow-500" />
              <h2 className="font-heading text-xl font-semibold">Top 5 per Valutazione BGG</h2>
            </div>
            {topRatedLoading ? (
              <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-4">
                {Array.from({ length: 5 }).map((_, i) => (
                  <MeepleGameCatalogCardSkeleton key={i} />
                ))}
              </div>
            ) : topRatedGames.length > 0 ? (
              <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-4">
                {topRatedGames.map(game => (
                  <MeepleGameCatalogCard key={game.id} game={game} onClick={handleGameClick} />
                ))}
              </div>
            ) : (
              <p className="text-muted-foreground text-sm">
                Nessun gioco con valutazione disponibile.
              </p>
            )}
          </section>

          {/* Latest 5 Added */}
          <section>
            <div className="flex items-center gap-2 mb-4">
              <Clock className="h-5 w-5 text-blue-500" />
              <h2 className="font-heading text-xl font-semibold">Ultimi 5 Aggiunti</h2>
            </div>
            {latestAddedLoading ? (
              <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-4">
                {Array.from({ length: 5 }).map((_, i) => (
                  <MeepleGameCatalogCardSkeleton key={i} />
                ))}
              </div>
            ) : latestAddedGames.length > 0 ? (
              <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-4">
                {latestAddedGames.map(game => (
                  <MeepleGameCatalogCard key={game.id} game={game} onClick={handleGameClick} />
                ))}
              </div>
            ) : (
              <p className="text-muted-foreground text-sm">Nessun gioco aggiunto di recente.</p>
            )}
          </section>
        </div>

        {/* Filters and Content */}
        <div className="flex flex-col lg:flex-row gap-6">
          {/* Sidebar Filters */}
          <aside className="w-full lg:w-64 flex-shrink-0">
            <CatalogFilters
              searchTerm={params.searchTerm}
              onSearchChange={handleSearch}
              categories={categories}
              selectedCategories={params.categoryIds}
              onCategoryChange={handleCategoryChange}
              mechanics={mechanics}
              selectedMechanics={params.mechanicIds}
              onMechanicChange={handleMechanicChange}
              minPlayers={params.minPlayers}
              maxPlayers={params.maxPlayers}
              onPlayersChange={handlePlayersChange}
              maxPlayingTime={params.maxPlayingTime}
              onPlaytimeChange={handlePlaytimeChange}
              sortBy={getFilterSortBy()}
              sortDescending={params.sortDescending}
              onSortChange={handleSortChange}
              onClearFilters={resetParams}
            />
          </aside>

          {/* Main Content */}
          <main className="flex-1">
            {/* Error State */}
            {error && (
              <Alert variant="destructive" className="mb-6">
                <AlertCircle className="h-4 w-4" />
                <AlertTitle>Errore</AlertTitle>
                <AlertDescription>
                  {error instanceof Error ? error.message : 'Errore nel caricamento dei giochi'}
                </AlertDescription>
              </Alert>
            )}

            {/* Loading State */}
            {isLoading && (
              <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                {Array.from({ length: 6 }).map((_, i) => (
                  <MeepleGameCatalogCardSkeleton key={i} />
                ))}
              </div>
            )}

            {/* Empty State */}
            {!isLoading && !error && games.length === 0 && (
              <div className="text-center py-12">
                <Library className="mx-auto h-16 w-16 text-muted-foreground mb-4" />
                <h3 className="text-xl font-semibold mb-2">Nessun gioco trovato</h3>
                <p className="text-muted-foreground mb-6">
                  Prova a modificare i filtri per trovare altri giochi.
                </p>
                <Button variant="outline" onClick={resetParams}>
                  Rimuovi Filtri
                </Button>
              </div>
            )}

            {/* Games Grid */}
            {!isLoading && !error && games.length > 0 && (
              <>
                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4 mb-6">
                  {games.map(game => (
                    <MeepleGameCatalogCard key={game.id} game={game} onClick={handleGameClick} />
                  ))}
                </div>

                {/* Pagination with results display (#2876) */}
                {totalPages > 1 && (
                  <CatalogPagination
                    currentPage={params.page}
                    totalPages={totalPages}
                    totalResults={total}
                    onPageChange={setPage}
                  />
                )}
              </>
            )}
          </main>
        </div>
      </div>
    </div>
  );
}
