'use client';

/**
 * GameCatalogClient Component (Issue #1017: BGAI-078)
 *
 * Client component for interactive game catalog functionality.
 * Handles search, view toggle, pagination, and game navigation.
 *
 * Features:
 * - Debounced search (300ms)
 * - Grid/List view toggle
 * - Pagination with URL state
 * - Click to navigate to game detail or ask page
 * - Loading and error states
 *
 * @see docs/04-frontend/wireframes-playful-boardroom.md
 */

import { useCallback, useEffect, useState, useTransition, useRef } from 'react';
import { useRouter, usePathname, useSearchParams } from 'next/navigation';
import Link from 'next/link';
import { Search, X, LayoutGrid, List, MessageSquare } from 'lucide-react';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { ToggleGroup, ToggleGroupItem } from '@/components/ui/toggle-group';
import { Skeleton } from '@/components/ui/skeleton';
import { GameCard } from '@/components/games/GameCard';
import { api, type Game } from '@/lib/api';
import { logger } from '@/lib/logger';
import { createErrorContext } from '@/lib/errors';

// ============================================================================
// Types
// ============================================================================

interface GameCatalogClientProps {
  initialView: 'grid' | 'list';
  initialSearch: string;
  initialPage: number;
}

interface PaginatedResponse {
  games: Game[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

// ============================================================================
// Constants
// ============================================================================

const PAGE_SIZE = 20;
const DEBOUNCE_MS = 300;

// ============================================================================
// Component
// ============================================================================

export function GameCatalogClient({
  initialView,
  initialSearch,
  initialPage,
}: GameCatalogClientProps) {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const [isPending, startTransition] = useTransition();

  // State
  const [view, setView] = useState<'grid' | 'list'>(initialView);
  const [searchInput, setSearchInput] = useState(initialSearch);
  const [currentPage, setCurrentPage] = useState(initialPage);
  const [games, setGames] = useState<Game[]>([]);
  const [total, setTotal] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const previousSearchRef = useRef(initialSearch);

  // ============================================================================
  // Data Fetching
  // ============================================================================

  const fetchGames = useCallback(async (search: string, page: number) => {
    setLoading(true);
    setError(null);

    try {
      const result = await api.games.getAll(
        search ? { search } : undefined,
        { field: 'title', direction: 'asc' },
        page,
        PAGE_SIZE
      );

      setGames(result.games);
      setTotal(result.total);
      setTotalPages(result.totalPages);
    } catch (err) {
      logger.error(
        'Failed to fetch games',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('GameCatalogClient', 'fetchGames', { search, page })
      );
      setError('Impossibile caricare i giochi. Riprova più tardi.');
      setGames([]);
      setTotal(0);
      setTotalPages(0);
    } finally {
      setLoading(false);
    }
  }, []);

  // Initial fetch
  useEffect(() => {
    void fetchGames(initialSearch, initialPage);
  }, [fetchGames, initialSearch, initialPage]);

  // ============================================================================
  // URL State Management
  // ============================================================================

  const updateUrl = useCallback(
    (params: { view?: string; search?: string; page?: number }) => {
      const newParams = new URLSearchParams(searchParams?.toString() || '');

      if (params.view) {
        newParams.set('view', params.view);
      }

      if (params.search !== undefined) {
        if (params.search) {
          newParams.set('search', params.search);
        } else {
          newParams.delete('search');
        }
        // Reset page when search changes
        newParams.delete('page');
      }

      if (params.page !== undefined && params.page > 1) {
        newParams.set('page', params.page.toString());
      } else if (params.page === 1) {
        newParams.delete('page');
      }

      startTransition(() => {
        router.push(`${pathname}?${newParams.toString()}`, { scroll: false });
      });
    },
    [router, pathname, searchParams]
  );

  // Debounced search
  useEffect(() => {
    const timer = setTimeout(() => {
      if (searchInput !== previousSearchRef.current) {
        previousSearchRef.current = searchInput;
        updateUrl({ search: searchInput });
        setCurrentPage(1);
        void fetchGames(searchInput, 1);
      }
    }, DEBOUNCE_MS);

    return () => clearTimeout(timer);
  }, [searchInput, updateUrl, fetchGames]);

  // ============================================================================
  // Event Handlers
  // ============================================================================

  const handleViewChange = useCallback(
    (value: string) => {
      if (value === 'grid' || value === 'list') {
        setView(value);
        updateUrl({ view: value });
      }
    },
    [updateUrl]
  );

  const handleClearSearch = useCallback(() => {
    setSearchInput('');
  }, []);

  const handlePageChange = useCallback(
    (page: number) => {
      setCurrentPage(page);
      updateUrl({ page });
      void fetchGames(searchInput, page);
      window.scrollTo({ top: 0, behavior: 'smooth' });
    },
    [updateUrl, fetchGames, searchInput]
  );

  const handleGameClick = useCallback(
    (gameId: string) => {
      router.push(`/board-game-ai/ask?gameId=${gameId}`);
    },
    [router]
  );

  // ============================================================================
  // Render
  // ============================================================================

  return (
    <div className="space-y-6">
      {/* Toolbar: Search + View Toggle */}
      <div className="flex flex-col sm:flex-row gap-4 items-start sm:items-center justify-between">
        {/* Search Bar */}
        <div className="relative w-full sm:w-96">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input
            type="search"
            placeholder="Cerca giochi per nome..."
            value={searchInput}
            onChange={e => setSearchInput(e.target.value)}
            className="pl-10 pr-10"
            aria-label="Cerca giochi"
          />
          {searchInput && (
            <Button
              variant="ghost"
              size="sm"
              onClick={handleClearSearch}
              className="absolute right-1 top-1/2 -translate-y-1/2 h-7 w-7 p-0"
              aria-label="Cancella ricerca"
            >
              <X className="h-4 w-4" />
            </Button>
          )}
        </div>

        {/* View Toggle */}
        <ToggleGroup
          type="single"
          value={view}
          onValueChange={handleViewChange}
          aria-label="Modalità visualizzazione"
          className="border rounded-md"
        >
          <ToggleGroupItem value="grid" aria-label="Vista griglia" className="gap-2">
            <LayoutGrid className="h-4 w-4" />
            <span className="hidden sm:inline">Griglia</span>
          </ToggleGroupItem>
          <ToggleGroupItem value="list" aria-label="Vista lista" className="gap-2">
            <List className="h-4 w-4" />
            <span className="hidden sm:inline">Lista</span>
          </ToggleGroupItem>
        </ToggleGroup>
      </div>

      {/* Results Count */}
      {!loading && !error && (
        <p className="text-sm text-muted-foreground">
          {total === 0
            ? 'Nessun gioco trovato'
            : total === 1
              ? '1 gioco trovato'
              : `${total} giochi trovati`}
          {searchInput && ` per "${searchInput}"`}
        </p>
      )}

      {/* Error State */}
      {error && (
        <div className="flex flex-col items-center justify-center py-16 text-center">
          <div className="text-6xl mb-4">⚠️</div>
          <h3 className="text-lg font-semibold mb-2 text-destructive">{error}</h3>
          <Button onClick={() => fetchGames(searchInput, currentPage)} variant="outline">
            Riprova
          </Button>
        </div>
      )}

      {/* Loading State */}
      {loading && (
        <div
          className={
            view === 'grid'
              ? 'grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-6'
              : 'flex flex-col gap-4'
          }
        >
          {Array.from({ length: PAGE_SIZE }).map((_, i) => (
            <Skeleton key={i} className={view === 'grid' ? 'h-[300px]' : 'h-[120px]'} />
          ))}
        </div>
      )}

      {/* Empty State */}
      {!loading && !error && games.length === 0 && (
        <div className="flex flex-col items-center justify-center py-16 text-center">
          <div className="text-6xl mb-4">🎲</div>
          <h3 className="text-lg font-semibold mb-2">Nessun gioco trovato</h3>
          <p className="text-muted-foreground mb-4">Prova a modificare la ricerca</p>
          {searchInput && (
            <Button onClick={handleClearSearch} variant="outline">
              Cancella ricerca
            </Button>
          )}
        </div>
      )}

      {/* Games Grid/List */}
      {!loading && !error && games.length > 0 && (
        <div
          className={
            view === 'grid'
              ? 'grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-6'
              : 'flex flex-col gap-4'
          }
        >
          {games.map(game => (
            <div key={game.id} className="relative group">
              <GameCard game={game} variant={view} onClick={() => handleGameClick(game.id)} />
              {/* Quick action overlay on hover */}
              <div className="absolute inset-0 bg-background/80 opacity-0 group-hover:opacity-100 transition-opacity flex items-center justify-center gap-2 rounded-lg pointer-events-none group-hover:pointer-events-auto">
                <Button
                  size="sm"
                  onClick={e => {
                    e.stopPropagation();
                    handleGameClick(game.id);
                  }}
                  className="gap-2"
                >
                  <MessageSquare className="h-4 w-4" />
                  Chiedi all&apos;AI
                </Button>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Pagination */}
      {!loading && !error && totalPages > 1 && (
        <GameCatalogPagination
          currentPage={currentPage}
          totalPages={totalPages}
          totalItems={total}
          onPageChange={handlePageChange}
        />
      )}
    </div>
  );
}

// ============================================================================
// Pagination Sub-Component
// ============================================================================

interface PaginationProps {
  currentPage: number;
  totalPages: number;
  totalItems: number;
  onPageChange: (page: number) => void;
}

function GameCatalogPagination({
  currentPage,
  totalPages,
  totalItems,
  onPageChange,
}: PaginationProps) {
  const getPageNumbers = (): (number | 'ellipsis')[] => {
    const pages: (number | 'ellipsis')[] = [];
    const showMax = 7;

    if (totalPages <= showMax) {
      for (let i = 1; i <= totalPages; i++) {
        pages.push(i);
      }
    } else {
      pages.push(1);

      if (currentPage > 3) {
        pages.push('ellipsis');
      }

      const start = Math.max(2, currentPage - 1);
      const end = Math.min(totalPages - 1, currentPage + 1);

      for (let i = start; i <= end; i++) {
        pages.push(i);
      }

      if (currentPage < totalPages - 2) {
        pages.push('ellipsis');
      }

      pages.push(totalPages);
    }

    return pages;
  };

  return (
    <div className="flex flex-col items-center gap-4 mt-8">
      <p className="text-sm text-muted-foreground">
        Pagina {currentPage} di {totalPages} ({totalItems} giochi)
      </p>

      <div className="flex items-center gap-2">
        <Button
          variant="outline"
          size="sm"
          onClick={() => onPageChange(currentPage - 1)}
          disabled={currentPage === 1}
          aria-label="Pagina precedente"
        >
          <span className="hidden sm:inline">Precedente</span>
          <span className="sm:hidden">←</span>
        </Button>

        <div className="hidden sm:flex items-center gap-1">
          {getPageNumbers().map((page, idx) =>
            page === 'ellipsis' ? (
              <span key={`ellipsis-${idx}`} className="px-2 text-muted-foreground">
                ...
              </span>
            ) : (
              <Button
                key={page}
                variant={page === currentPage ? 'default' : 'outline'}
                size="sm"
                onClick={() => onPageChange(page)}
                aria-label={`Vai a pagina ${page}`}
                aria-current={page === currentPage ? 'page' : undefined}
              >
                {page}
              </Button>
            )
          )}
        </div>

        <Button
          variant="outline"
          size="sm"
          onClick={() => onPageChange(currentPage + 1)}
          disabled={currentPage === totalPages}
          aria-label="Pagina successiva"
        >
          <span className="hidden sm:inline">Successiva</span>
          <span className="sm:hidden">→</span>
        </Button>
      </div>
    </div>
  );
}
