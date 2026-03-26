'use client';

/**
 * BggSearchPanel - BGG search with results grid and import actions
 *
 * Provides a debounced search input, paginated results grid using MeepleCard,
 * and "Add to Library" actions for Game Night Improvvisata flow.
 */

import { useState, useCallback, useEffect, useRef } from 'react';

import { Search, ChevronLeft, ChevronRight, Gamepad2 } from 'lucide-react';
import { toast } from 'sonner';

import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { api } from '@/lib/api';
import type {
  BggGameSummary,
  GameNightBggSearchResponse,
  ImportBggGameResponse,
} from '@/lib/api/clients/gameNightBggClient';

import { BggGameCard } from './BggGameCard';

// ============================================================================
// Types
// ============================================================================

export interface BggSearchPanelProps {
  /** Callback fired after a successful import */
  onImportSuccess?: (result: ImportBggGameResponse) => void;
}

// ============================================================================
// Constants
// ============================================================================

const DEBOUNCE_MS = 300;
const PAGE_SIZE = 20;

// ============================================================================
// Component
// ============================================================================

export function BggSearchPanel({ onImportSuccess }: BggSearchPanelProps) {
  const [query, setQuery] = useState('');
  const [debouncedQuery, setDebouncedQuery] = useState('');
  const [page, setPage] = useState(1);
  const [results, setResults] = useState<GameNightBggSearchResponse | null>(null);
  const [isSearching, setIsSearching] = useState(false);
  const [importingIds, setImportingIds] = useState<Set<number>>(new Set());
  const [importedIds, setImportedIds] = useState<Set<number>>(new Set());
  const debounceRef = useRef<ReturnType<typeof setTimeout>>(undefined);

  // Debounce input
  useEffect(() => {
    if (debounceRef.current) {
      clearTimeout(debounceRef.current);
    }

    debounceRef.current = setTimeout(() => {
      setDebouncedQuery(query.trim());
      setPage(1);
    }, DEBOUNCE_MS);

    return () => {
      if (debounceRef.current) {
        clearTimeout(debounceRef.current);
      }
    };
  }, [query]);

  // Perform search when debounced query or page changes
  useEffect(() => {
    if (!debouncedQuery) {
      setResults(null);
      return;
    }

    let cancelled = false;

    async function doSearch() {
      setIsSearching(true);
      try {
        const response = await api.gameNightBgg.searchGames(debouncedQuery, page, PAGE_SIZE);
        if (!cancelled) {
          setResults(response);
        }
      } catch (error) {
        if (!cancelled) {
          const message = error instanceof Error ? error.message : 'Search failed';
          toast.error(message);
          setResults(null);
        }
      } finally {
        if (!cancelled) {
          setIsSearching(false);
        }
      }
    }

    doSearch();

    return () => {
      cancelled = true;
    };
  }, [debouncedQuery, page]);

  // Import handler
  const handleImport = useCallback(
    async (bggId: number) => {
      setImportingIds(prev => new Set(prev).add(bggId));

      try {
        const result = await api.gameNightBgg.importGame(bggId);
        setImportedIds(prev => new Set(prev).add(bggId));
        toast.success(`"${result.title}" added to your library`);
        onImportSuccess?.(result);
      } catch (error) {
        const message = error instanceof Error ? error.message : 'Import failed';
        toast.error(message);
      } finally {
        setImportingIds(prev => {
          const next = new Set(prev);
          next.delete(bggId);
          return next;
        });
      }
    },
    [onImportSuccess]
  );

  const totalPages = results ? Math.ceil(results.totalCount / PAGE_SIZE) : 0;
  const hasResults = results && results.items.length > 0;
  const showEmpty = results && results.items.length === 0 && !isSearching;

  return (
    <div className="space-y-6">
      {/* Search Input */}
      <div className="relative">
        <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
        <Input
          type="text"
          placeholder="Search BoardGameGeek..."
          value={query}
          onChange={e => setQuery(e.target.value)}
          className="pl-10 font-nunito"
          data-testid="bgg-search-input"
        />
      </div>

      {/* Loading Skeleton */}
      {isSearching && (
        <div
          className="grid grid-cols-1 gap-4 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4"
          data-testid="bgg-search-loading"
        >
          {Array.from({ length: 8 }).map((_, i) => (
            <div key={i} className="space-y-3">
              <Skeleton className="h-48 w-full rounded-lg" />
              <Skeleton className="h-4 w-3/4" />
              <Skeleton className="h-4 w-1/2" />
              <Skeleton className="h-9 w-full" />
            </div>
          ))}
        </div>
      )}

      {/* Results Grid */}
      {!isSearching && hasResults && (
        <>
          <p className="text-sm text-muted-foreground font-nunito">
            {results.totalCount} result{results.totalCount !== 1 ? 's' : ''} found
          </p>
          <div
            className="grid grid-cols-1 gap-4 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4"
            data-testid="bgg-search-results"
          >
            {results.items.map((game: BggGameSummary) => (
              <BggGameCard
                key={game.bggId}
                game={game}
                isImporting={importingIds.has(game.bggId)}
                isImported={importedIds.has(game.bggId)}
                onImport={handleImport}
              />
            ))}
          </div>

          {/* Pagination */}
          {totalPages > 1 && (
            <div className="flex items-center justify-center gap-4" data-testid="bgg-pagination">
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage(p => Math.max(1, p - 1))}
                disabled={page <= 1}
              >
                <ChevronLeft className="mr-1 h-4 w-4" />
                Previous
              </Button>
              <span className="text-sm font-nunito text-muted-foreground">
                Page {page} of {totalPages}
              </span>
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage(p => Math.min(totalPages, p + 1))}
                disabled={page >= totalPages}
              >
                Next
                <ChevronRight className="ml-1 h-4 w-4" />
              </Button>
            </div>
          )}
        </>
      )}

      {/* Empty State */}
      {showEmpty && (
        <div
          className="flex flex-col items-center justify-center py-16 text-center"
          data-testid="bgg-search-empty"
        >
          <Gamepad2 className="mb-4 h-12 w-12 text-muted-foreground/50" />
          <h3 className="font-quicksand text-lg font-semibold text-muted-foreground">
            No games found
          </h3>
          <p className="mt-1 text-sm text-muted-foreground/70 font-nunito">
            Try a different search term or check the spelling.
          </p>
        </div>
      )}

      {/* Initial State (no query) */}
      {!debouncedQuery && !isSearching && (
        <div
          className="flex flex-col items-center justify-center py-16 text-center"
          data-testid="bgg-search-initial"
        >
          <Search className="mb-4 h-12 w-12 text-muted-foreground/50" />
          <h3 className="font-quicksand text-lg font-semibold text-muted-foreground">
            Search BoardGameGeek
          </h3>
          <p className="mt-1 text-sm text-muted-foreground/70 font-nunito">
            Type a game name to find and add it to your library.
          </p>
        </div>
      )}
    </div>
  );
}
