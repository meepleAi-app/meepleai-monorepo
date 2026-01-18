/**
 * Shared Games Catalog Page (Issue #2518)
 *
 * Client Component for browsing shared games available to add to library.
 * Displays community-curated game catalog with advanced filtering.
 *
 * Route: /games/catalog
 * Features:
 * - Responsive grid (1→2→3 columns)
 * - Search with debounce
 * - Advanced filters (complexity, players, playtime, categories, mechanics)
 * - Pagination with infinite scroll option
 * - Add to library functionality
 * - "Already in library" badges
 *
 * @see Issue #2518 User Library - Catalog, Library & Agent Configuration UI
 */

'use client';

import { useState, useMemo } from 'react';
import Link from 'next/link';

import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { AlertCircle, Library } from 'lucide-react';

import { useSharedGames, useGameCategories, useGameMechanics } from '@/hooks/queries';
import type { SearchSharedGamesParams } from '@/lib/api';

import { CatalogFilters } from '@/components/catalog/CatalogFilters';
import { GameCatalogCard } from '@/components/catalog/GameCatalogCard';
import { CatalogPagination } from '@/components/catalog/CatalogPagination';

export default function SharedGamesCatalogPage() {
  // Filter and pagination state
  const [searchTerm, setSearchTerm] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  const [sortBy, setSortBy] = useState<'title' | 'complexity' | 'playingTime'>('title');
  const [sortDescending, setSortDescending] = useState(false);

  // Advanced filters state
  const [selectedCategories, setSelectedCategories] = useState<string[]>([]);
  const [selectedMechanics, setSelectedMechanics] = useState<string[]>([]);
  const [minPlayers, setMinPlayers] = useState<number | undefined>(undefined);
  const [maxPlayers, setMaxPlayers] = useState<number | undefined>(undefined);
  const [maxPlayingTime, setMaxPlayingTime] = useState<number | undefined>(undefined);

  // Build query params
  const queryParams = useMemo<SearchSharedGamesParams>(() => {
    const params: SearchSharedGamesParams = {
      page,
      pageSize,
      sortBy,
      sortDescending,
    };

    if (searchTerm) params.searchTerm = searchTerm;
    if (selectedCategories.length > 0) params.categoryIds = selectedCategories.join(',');
    if (selectedMechanics.length > 0) params.mechanicIds = selectedMechanics.join(',');
    if (minPlayers !== undefined) params.minPlayers = minPlayers;
    if (maxPlayers !== undefined) params.maxPlayers = maxPlayers;
    if (maxPlayingTime !== undefined) params.maxPlayingTime = maxPlayingTime;

    return params;
  }, [
    searchTerm,
    page,
    pageSize,
    sortBy,
    sortDescending,
    selectedCategories,
    selectedMechanics,
    minPlayers,
    maxPlayers,
    maxPlayingTime,
  ]);

  // Fetch data
  const { data: gamesData, isLoading, error } = useSharedGames(queryParams);
  const { data: categories = [] } = useGameCategories();
  const { data: mechanics = [] } = useGameMechanics();

  const games = gamesData?.items || [];
  const total = gamesData?.total || 0;
  const totalPages = gamesData ? Math.ceil(total / pageSize) : 0;

  // Reset to page 1 when filters change
  const handleFilterChange = () => {
    if (page !== 1) setPage(1);
  };

  // Handlers
  const handleSearch = (term: string) => {
    setSearchTerm(term);
    handleFilterChange();
  };

  const handleCategoryChange = (categoryIds: string[]) => {
    setSelectedCategories(categoryIds);
    handleFilterChange();
  };

  const handleMechanicChange = (mechanicIds: string[]) => {
    setSelectedMechanics(mechanicIds);
    handleFilterChange();
  };

  const handlePlayersChange = (min?: number, max?: number) => {
    setMinPlayers(min);
    setMaxPlayers(max);
    handleFilterChange();
  };

  const handlePlaytimeChange = (max?: number) => {
    setMaxPlayingTime(max);
    handleFilterChange();
  };

  const handleSortChange = (newSortBy: typeof sortBy, descending: boolean) => {
    setSortBy(newSortBy);
    setSortDescending(descending);
  };

  // Clear all filters
  const clearFilters = () => {
    setSearchTerm('');
    setSelectedCategories([]);
    setSelectedMechanics([]);
    setMinPlayers(undefined);
    setMaxPlayers(undefined);
    setMaxPlayingTime(undefined);
    setPage(1);
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
              Esplora {total} giochi disponibili. Aggiungi alla tua libreria per iniziare a chattare.
            </p>
          </div>
          <Link href="/library">
            <Button variant="outline">
              <Library className="mr-2 h-4 w-4" />
              La Mia Libreria
            </Button>
          </Link>
        </div>

        {/* Filters and Content */}
        <div className="flex flex-col lg:flex-row gap-6">
          {/* Sidebar Filters */}
          <aside className="w-full lg:w-64 flex-shrink-0">
            <CatalogFilters
              searchTerm={searchTerm}
              onSearchChange={handleSearch}
              categories={categories}
              selectedCategories={selectedCategories}
              onCategoryChange={handleCategoryChange}
              mechanics={mechanics}
              selectedMechanics={selectedMechanics}
              onMechanicChange={handleMechanicChange}
              minPlayers={minPlayers}
              maxPlayers={maxPlayers}
              onPlayersChange={handlePlayersChange}
              maxPlayingTime={maxPlayingTime}
              onPlaytimeChange={handlePlaytimeChange}
              sortBy={sortBy}
              sortDescending={sortDescending}
              onSortChange={handleSortChange}
              onClearFilters={clearFilters}
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
                  <Skeleton key={i} className="h-96 w-full" />
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
                <Button variant="outline" onClick={clearFilters}>
                  Rimuovi Filtri
                </Button>
              </div>
            )}

            {/* Games Grid */}
            {!isLoading && !error && games.length > 0 && (
              <>
                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4 mb-6">
                  {games.map((game) => (
                    <GameCatalogCard key={game.id} game={game} />
                  ))}
                </div>

                {/* Pagination */}
                {totalPages > 1 && (
                  <CatalogPagination
                    currentPage={page}
                    totalPages={totalPages}
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
