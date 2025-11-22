'use client';


import React, { useState, useEffect, useCallback } from 'react';
import { Game, GameFilters, GameSortOptions, PaginatedGamesResponse, api } from '@/lib/api';
import { GameSearchBar } from '@/components/games/GameSearchBar';
import { logger } from '@/lib/logger';
import { createErrorContext } from '@/lib/errors';
import { GameFilterPanel } from '@/components/games/GameFilterPanel';
import { GameSortControl } from '@/components/games/GameSortControl';
import { GameList } from '@/components/games/GameList';
import { GameDetailModal } from '@/components/games/GameDetailModal';
import { Button } from '@/components/ui/button';
import { AlertCircle } from 'lucide-react';

// LocalStorage keys
const STORAGE_KEYS = {
  FILTERS: 'meepleai_game_filters',
  SORT: 'meepleai_game_sort',
  VIEW_MODE: 'meepleai_game_view_mode',
};

type ViewMode = 'grid' | 'list';

export default function GamesPage() {
  // State
  const [searchQuery, setSearchQuery] = useState('');
  const [filters, setFilters] = useState<GameFilters>({});
  const [sortOptions, setSortOptions] = useState<GameSortOptions | null>(null);
  const [viewMode, setViewMode] = useState<ViewMode>('grid');
  const [currentPage, setCurrentPage] = useState(1);
  const [paginatedResponse, setPaginatedResponse] = useState<PaginatedGamesResponse>({
    games: [],
    total: 0,
    page: 1,
    pageSize: 20,
    totalPages: 0,
  });
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selectedGame, setSelectedGame] = useState<Game | null>(null);
  const [isDetailModalOpen, setIsDetailModalOpen] = useState(false);

  // Load saved preferences from localStorage on mount
  useEffect(() => {
    try {
      const savedFilters = localStorage.getItem(STORAGE_KEYS.FILTERS);
      const savedSort = localStorage.getItem(STORAGE_KEYS.SORT);
      const savedViewMode = localStorage.getItem(STORAGE_KEYS.VIEW_MODE);

      if (savedFilters) {
        setFilters(JSON.parse(savedFilters));
      }
      if (savedSort) {
        setSortOptions(JSON.parse(savedSort));
      }
      if (savedViewMode) {
        setViewMode(savedViewMode as ViewMode);
      }
    } catch (err) {
      logger.error(
        'Failed to load saved game preferences',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('GamesPage', 'loadPreferences', { operation: 'load_preferences' })
      );
    }
  }, []);

  // Fetch games when filters, sort, or page changes
  useEffect(() => {
    const abortController = new AbortController();
    let isActive = true;

    const fetchGames = async () => {
      setLoading(true);
      setError(null);

      try {
        const combinedFilters: GameFilters = {
          ...filters,
          search: searchQuery || undefined,
        };

        const response = await api.games.getAll(
          combinedFilters,
          sortOptions || undefined,
          currentPage,
          20
        );

        // Only update state if this is still the active request
        if (isActive) {
          setPaginatedResponse(response);
        }
      } catch (err) {
        // Ignore AbortError from cancelled requests
        if (err instanceof Error && err.name === 'AbortError') {
          return;
        }

        if (isActive) {
          logger.error(
            'Failed to fetch games',
            err instanceof Error ? err : new Error(String(err)),
            createErrorContext('GamesPage', 'fetchGames', { filters, sortOptions, page: currentPage, operation: 'fetch_games' })
          );
          setError('Failed to load games. Please try again.');
        }
      } finally {
        if (isActive) {
          setLoading(false);
        }
      }
    };

    fetchGames();

    // Cleanup function to abort request and mark as inactive
    return () => {
      isActive = false;
      abortController.abort();
    };
  }, [filters, sortOptions, currentPage, searchQuery]);

  // Save preferences to localStorage when they change
  useEffect(() => {
    try {
      localStorage.setItem(STORAGE_KEYS.FILTERS, JSON.stringify(filters));
    } catch (err) {
      logger.error(
        'Failed to save game filters',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('GamesPage', 'saveFilters', { operation: 'save_filters' })
      );
    }
  }, [filters]);

  useEffect(() => {
    try {
      if (sortOptions) {
        localStorage.setItem(STORAGE_KEYS.SORT, JSON.stringify(sortOptions));
      } else {
        localStorage.removeItem(STORAGE_KEYS.SORT);
      }
    } catch (err) {
      logger.error(
        'Failed to save sort options',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('GamesPage', 'saveSortOptions', { operation: 'save_sort_options' })
      );
    }
  }, [sortOptions]);

  useEffect(() => {
    try {
      localStorage.setItem(STORAGE_KEYS.VIEW_MODE, viewMode);
    } catch (err) {
      logger.error(
        'Failed to save view mode',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('GamesPage', 'saveViewMode', { operation: 'save_view_mode' })
      );
    }
  }, [viewMode]);

  // Reset page to 1 when filters or sort changes
  useEffect(() => {
    setCurrentPage(1);
  }, [filters, sortOptions, searchQuery]);

  // Handlers
  const handleSearchChange = useCallback((value: string) => {
    setSearchQuery(value);
  }, []);

  const handleFiltersChange = useCallback((newFilters: GameFilters) => {
    setFilters(newFilters);
  }, []);

  const handleSortChange = useCallback((newSort: GameSortOptions | null) => {
    setSortOptions(newSort);
  }, []);

  const handleViewModeChange = useCallback((mode: ViewMode) => {
    setViewMode(mode);
  }, []);

  const handlePageChange = useCallback((page: number) => {
    setCurrentPage(page);
    // Scroll to top on page change
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }, []);

  const handleGameClick = useCallback((game: Game) => {
    setSelectedGame(game);
    setIsDetailModalOpen(true);
  }, []);

  const handleDetailModalClose = useCallback(() => {
    setIsDetailModalOpen(false);
    // Delay clearing selected game to avoid flashing empty content
    setTimeout(() => setSelectedGame(null), 200);
  }, []);

  const handleRetry = useCallback(() => {
    setError(null);
    setCurrentPage(1);
  }, []);

  return (
      <div className="container mx-auto px-4 py-8">
        <div className="mb-8">
          <h1 className="text-3xl font-bold mb-2">Game Library</h1>
          <p className="text-muted-foreground">
            Browse and search our collection of board games
          </p>
        </div>

        {/* Search Bar */}
        <div className="mb-6">
          <GameSearchBar
            value={searchQuery}
            onChange={handleSearchChange}
          />
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-4 gap-6">
          {/* Filters Sidebar */}
          <div className="lg:col-span-1">
            <div className="space-y-4">
              <GameFilterPanel
                filters={filters}
                onChange={handleFiltersChange}
                collapsible={true}
                defaultCollapsed={false}
              />

              <GameSortControl
                sortOptions={sortOptions}
                onChange={handleSortChange}
              />
            </div>
          </div>

          {/* Games List */}
          <div className="lg:col-span-3">
            {error ? (
              <div className="flex flex-col items-center justify-center py-12 px-4 text-center">
                <div className="w-16 h-16 mb-4 rounded-full bg-destructive/10 flex items-center justify-center">
                  <AlertCircle className="h-8 w-8 text-destructive" />
                </div>
                <h3 className="text-lg font-semibold mb-2">Error loading games</h3>
                <p className="text-muted-foreground mb-4">{error}</p>
                <Button onClick={handleRetry}>Try Again</Button>
              </div>
            ) : (
              <GameList
                games={paginatedResponse.games}
                loading={loading}
                viewMode={viewMode}
                onViewModeChange={handleViewModeChange}
                onGameClick={handleGameClick}
                currentPage={currentPage}
                totalPages={paginatedResponse.totalPages}
                totalGames={paginatedResponse.total}
                onPageChange={handlePageChange}
              />
            )}
          </div>
        </div>

        {/* Game Detail Modal */}
        <GameDetailModal
          game={selectedGame}
          open={isDetailModalOpen}
          onOpenChange={handleDetailModalClose}
        />
      </div>
  );
}
