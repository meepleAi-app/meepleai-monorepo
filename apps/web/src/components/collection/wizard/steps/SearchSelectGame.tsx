'use client';

/**
 * Step 1: Search/Select Game
 * Issue #3477, #3650: Search SharedGameCatalog with debounce + pagination
 */

import { useState, useEffect, useCallback, useRef } from 'react';

import { Search, Loader2 } from 'lucide-react';

import { toast } from '@/components/layout';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { useAddGameWizard } from '@/hooks/useAddGameWizard';
import { api } from '@/lib/api';
import type { Game } from '@/types/domain';

// Constants
const DEBOUNCE_MS = 300;
const PAGE_SIZE = 10;

// Search result type matching SharedGame catalog
interface SearchResult {
  id: string;
  title: string;
  yearPublished: number;
  thumbnailUrl: string;
  minPlayers: number;
  maxPlayers: number;
  playingTimeMinutes: number;
  averageRating: number | null;
}

export function SearchSelectGame() {
  const { selectSharedGame, selectCustomGame, goNext } = useAddGameWizard();

  // Search state
  const [searchQuery, setSearchQuery] = useState('');
  const [debouncedQuery, setDebouncedQuery] = useState('');
  const [results, setResults] = useState<SearchResult[]>([]);
  const [loading, setLoading] = useState(false);
  const [loadingMore, setLoadingMore] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [hasMore, setHasMore] = useState(false);
  const [totalCount, setTotalCount] = useState(0);

  // Selection state
  const [selectedGameId, setSelectedGameId] = useState<string | null>(null);

  // Abort controller for cancelling requests
  const abortControllerRef = useRef<AbortController | null>(null);

  // Debounce search query
  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedQuery(searchQuery);
      setPage(1); // Reset pagination on new search
    }, DEBOUNCE_MS);

    return () => clearTimeout(timer);
  }, [searchQuery]);

  // Search API call
  const searchGames = useCallback(async (query: string, pageNum: number, append: boolean = false) => {
    // Cancel previous request
    if (abortControllerRef.current) {
      abortControllerRef.current.abort();
    }
    abortControllerRef.current = new AbortController();

    if (!query.trim()) {
      setResults([]);
      setTotalCount(0);
      setHasMore(false);
      setError(null);
      return;
    }

    if (append) {
      setLoadingMore(true);
    } else {
      setLoading(true);
    }
    setError(null);

    try {
      const response = await api.sharedGames.search({
        searchTerm: query,
        page: pageNum,
        pageSize: PAGE_SIZE,
        status: 2, // Published only
      });

      const mappedResults: SearchResult[] = response.items.map(game => ({
        id: game.id,
        title: game.title,
        yearPublished: game.yearPublished,
        thumbnailUrl: game.thumbnailUrl,
        minPlayers: game.minPlayers,
        maxPlayers: game.maxPlayers,
        playingTimeMinutes: game.playingTimeMinutes,
        averageRating: game.averageRating,
      }));

      if (append) {
        setResults(prev => [...prev, ...mappedResults]);
      } else {
        setResults(mappedResults);
      }

      setTotalCount(response.total);
      setHasMore(pageNum * PAGE_SIZE < response.total);
    } catch (err) {
      if ((err as Error).name !== 'AbortError') {
        const message = err instanceof Error ? err.message : 'Search failed';
        setError(message);
        toast.error(`Search error: ${message}`);
      }
    } finally {
      setLoading(false);
      setLoadingMore(false);
    }
  }, []);

  // Trigger search on debounced query change
  useEffect(() => {
    void searchGames(debouncedQuery, 1, false);
  }, [debouncedQuery, searchGames]);

  // Load more handler
  const handleLoadMore = useCallback(() => {
    const nextPage = page + 1;
    setPage(nextPage);
    void searchGames(debouncedQuery, nextPage, true);
  }, [page, debouncedQuery, searchGames]);

  // Select a game from catalog
  const handleSelectGame = (game: SearchResult) => {
    setSelectedGameId(game.id);
    // Map to Game type expected by store
    const gameForStore: Game = {
      id: game.id,
      title: game.title,
      createdAt: new Date().toISOString(),
    };
    selectSharedGame(gameForStore);
  };

  // Create custom game
  const handleCreateCustom = () => {
    selectCustomGame();
    goNext(); // Immediately go to Step 2 (Game Details)
  };

  // Proceed to next step
  const handleNext = () => {
    goNext(); // Skip to Step 3 (Upload PDF)
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-semibold text-slate-900 dark:text-white mb-2">
          Search or Create Game
        </h2>
        <p className="text-slate-600 dark:text-slate-400">
          Search the shared game catalog or create a custom game entry
        </p>
      </div>

      {/* Search Input */}
      <div className="space-y-2">
        <div className="relative">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
          <Input
            type="text"
            placeholder="Search by game title..."
            value={searchQuery}
            onChange={e => setSearchQuery(e.target.value)}
            className="w-full pl-10"
            aria-label="Search games"
          />
        </div>
        {debouncedQuery && !loading && (
          <p className="text-sm text-slate-500">
            {totalCount} game{totalCount !== 1 ? 's' : ''} found
          </p>
        )}
      </div>

      {/* Loading Skeleton */}
      {loading && (
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          {Array.from({ length: 4 }).map((_, i) => (
            <div key={i} className="p-4 border-2 border-slate-200 dark:border-slate-700 rounded-lg">
              <Skeleton className="h-5 w-3/4 mb-2" />
              <Skeleton className="h-4 w-1/2 mb-2" />
              <Skeleton className="h-3 w-2/3" />
            </div>
          ))}
        </div>
      )}

      {/* Error State */}
      {error && !loading && (
        <div className="p-4 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg">
          <p className="text-sm text-red-700 dark:text-red-300">{error}</p>
          <Button
            variant="outline"
            size="sm"
            onClick={() => void searchGames(debouncedQuery, 1, false)}
            className="mt-2"
          >
            Retry
          </Button>
        </div>
      )}

      {/* Game Results Grid */}
      {!loading && results.length > 0 && (
        <>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 max-h-96 overflow-y-auto p-1">
            {results.map(game => (
              <button
                key={game.id}
                onClick={() => handleSelectGame(game)}
                className={`p-4 border-2 rounded-lg text-left transition-colors hover:border-blue-400 ${
                  selectedGameId === game.id
                    ? 'border-blue-600 bg-blue-50 dark:bg-blue-900/20'
                    : 'border-slate-200 dark:border-slate-700 hover:bg-slate-50 dark:hover:bg-slate-800'
                }`}
              >
                <div className="flex gap-3">
                  {/* Thumbnail */}
                  {game.thumbnailUrl ? (
                    // eslint-disable-next-line @next/next/no-img-element
                    <img
                      src={game.thumbnailUrl}
                      alt=""
                      className="w-12 h-12 rounded object-cover flex-shrink-0"
                      loading="lazy"
                    />
                  ) : (
                    <div className="w-12 h-12 rounded bg-slate-100 dark:bg-slate-800 flex items-center justify-center flex-shrink-0">
                      <span className="text-xl">🎲</span>
                    </div>
                  )}
                  <div className="flex-1 min-w-0">
                    <h3 className="font-semibold text-slate-900 dark:text-white truncate">
                      {game.title}
                    </h3>
                    <p className="text-sm text-slate-500 mt-0.5">
                      {game.yearPublished > 0 && `${game.yearPublished} • `}
                      {game.minPlayers > 0 && game.maxPlayers > 0 && (
                        <span>
                          {game.minPlayers === game.maxPlayers
                            ? `${game.minPlayers}p`
                            : `${game.minPlayers}-${game.maxPlayers}p`}
                        </span>
                      )}
                      {game.playingTimeMinutes > 0 && ` • ${game.playingTimeMinutes}min`}
                    </p>
                    {game.averageRating && game.averageRating > 0 && (
                      <p className="text-xs text-amber-600 dark:text-amber-400 mt-0.5">
                        ★ {game.averageRating.toFixed(1)}
                      </p>
                    )}
                  </div>
                </div>
                {selectedGameId === game.id && (
                  <span className="inline-block mt-2 text-xs bg-blue-600 text-white px-2 py-1 rounded">
                    Selected ✓
                  </span>
                )}
              </button>
            ))}
          </div>

          {/* Load More Button */}
          {hasMore && (
            <div className="flex justify-center">
              <Button variant="outline" onClick={handleLoadMore} disabled={loadingMore}>
                {loadingMore ? (
                  <>
                    <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                    Loading...
                  </>
                ) : (
                  `Load More (${results.length} of ${totalCount})`
                )}
              </Button>
            </div>
          )}
        </>
      )}

      {/* No Results State */}
      {!loading && debouncedQuery && results.length === 0 && !error && (
        <div className="p-6 text-center border-2 border-dashed border-slate-200 dark:border-slate-700 rounded-lg">
          <p className="text-slate-600 dark:text-slate-400 mb-2">
            No games found for &quot;{debouncedQuery}&quot;
          </p>
          <p className="text-sm text-slate-500">
            Try a different search term or create a custom game
          </p>
        </div>
      )}

      {/* Divider */}
      <div className="flex items-center gap-4">
        <div className="flex-1 h-px bg-slate-200 dark:bg-slate-700" />
        <span className="text-sm text-slate-500">OR</span>
        <div className="flex-1 h-px bg-slate-200 dark:bg-slate-700" />
      </div>

      {/* Create Custom Game */}
      <div className="p-4 border-2 border-dashed border-slate-300 dark:border-slate-600 rounded-lg text-center">
        <p className="text-slate-600 dark:text-slate-400 mb-3">
          Can&apos;t find your game? Create a custom entry.
        </p>
        <Button variant="outline" onClick={handleCreateCustom}>
          + Create Custom Game
        </Button>
      </div>

      {/* Next Button (only if shared game selected) */}
      {selectedGameId && (
        <div className="flex justify-end">
          <Button onClick={handleNext}>Next →</Button>
        </div>
      )}
    </div>
  );
}
