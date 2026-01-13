/**
 * SharedGameSearch Component (Issue #2373: Phase 4)
 *
 * Search-first game search with SharedGameCatalog and BGG fallback.
 * Implements the user-facing search flow from the SharedGameCatalog spec.
 *
 * Flow:
 * 1. User types search term (debounced 300ms)
 * 2. Search SharedGameCatalog first (instant, local DB)
 * 3. If no results, show "Search on BGG" button
 * 4. User clicks BGG button -> BGG search results shown
 * 5. Results show source badge (Catalog vs BGG)
 *
 * @see claudedocs/shared-game-catalog-spec.md (Section: User-Facing, Flusso Utente)
 */

'use client';

import { useCallback, useEffect, useRef, useState } from 'react';

import { ExternalLink, Loader2, Search, X } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { api } from '@/lib/api';
import { cn } from '@/lib/utils';

import {
  SharedGameSearchFilters,
  DEFAULT_FILTERS,
  type SearchFilters,
} from './SharedGameSearchFilters';

// ============================================================================
// Types
// ============================================================================

export interface SharedGameSearchResult {
  id: string;
  title: string;
  yearPublished: number;
  thumbnailUrl: string;
  minPlayers: number;
  maxPlayers: number;
  playingTimeMinutes: number;
  averageRating: number | null;
  source: 'catalog' | 'bgg';
  bggId?: number | null;
}

export interface SharedGameSearchProps {
  /** Called when user selects a game */
  onSelect?: (game: SharedGameSearchResult) => void;
  /** Placeholder text */
  placeholder?: string;
  /** Auto-focus the search input */
  autoFocus?: boolean;
  /** Show filters panel */
  showFilters?: boolean;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Constants
// ============================================================================

const DEBOUNCE_MS = 300;
const PAGE_SIZE = 10;

// ============================================================================
// Empty State Components
// ============================================================================

function NoResultsState({ searchTerm, catalogOnly }: { searchTerm: string; catalogOnly: boolean }) {
  return (
    <div className="p-6 text-center">
      <div className="text-4xl mb-3">🎲</div>
      <p className="text-sm font-medium mb-1">Nessun risultato per &quot;{searchTerm}&quot;</p>
      <p className="text-xs text-muted-foreground">
        {catalogOnly
          ? 'Prova a disattivare il filtro "Solo dal catalogo" per cercare su BGG'
          : 'Prova con un termine di ricerca diverso'}
      </p>
    </div>
  );
}

// ============================================================================
// Component
// ============================================================================

export function SharedGameSearch({
  onSelect,
  placeholder = 'Cerca un gioco...',
  autoFocus = false,
  showFilters = true,
  className,
}: SharedGameSearchProps) {
  const inputRef = useRef<HTMLInputElement>(null);

  // State
  const [searchTerm, setSearchTerm] = useState('');
  const [debouncedTerm, setDebouncedTerm] = useState('');
  const [results, setResults] = useState<SharedGameSearchResult[]>([]);
  const [loading, setLoading] = useState(false);
  const [showBggButton, setShowBggButton] = useState(false);
  const [bggLoading, setBggLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [filters, setFilters] = useState<SearchFilters>(DEFAULT_FILTERS);
  const abortControllerRef = useRef<AbortController | null>(null);

  // ============================================================================
  // Debounced Search
  // ============================================================================

  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedTerm(searchTerm);
    }, DEBOUNCE_MS);

    return () => clearTimeout(timer);
  }, [searchTerm]);

  // ============================================================================
  // Search SharedGameCatalog
  // ============================================================================

  useEffect(() => {
    if (!debouncedTerm.trim()) {
      setResults([]);
      setShowBggButton(false);
      setError(null);
      return;
    }

    // Cancel previous request
    if (abortControllerRef.current) {
      abortControllerRef.current.abort();
    }
    abortControllerRef.current = new AbortController();

    const searchCatalog = async () => {
      setLoading(true);
      setError(null);
      setShowBggButton(false);

      try {
        const response = await api.sharedGames.search({
          searchTerm: debouncedTerm,
          page: 1,
          pageSize: PAGE_SIZE,
          status: 1, // Published only
          // Apply filters
          categoryIds: filters.categoryIds.length > 0 ? filters.categoryIds.join(',') : undefined,
          mechanicIds: filters.mechanicIds.length > 0 ? filters.mechanicIds.join(',') : undefined,
          minPlayers: filters.minPlayers ?? undefined,
          maxPlayers: filters.maxPlayers ?? undefined,
          maxPlayingTime: filters.maxPlayingTime ?? undefined,
        });

        if (response.items.length > 0) {
          // Map catalog results
          const catalogResults: SharedGameSearchResult[] = response.items.map(game => ({
            id: game.id,
            title: game.title,
            yearPublished: game.yearPublished,
            thumbnailUrl: game.thumbnailUrl,
            minPlayers: game.minPlayers,
            maxPlayers: game.maxPlayers,
            playingTimeMinutes: game.playingTimeMinutes,
            averageRating: game.averageRating,
            source: 'catalog' as const,
            bggId: game.bggId,
          }));
          setResults(catalogResults);
          setShowBggButton(false);
        } else {
          // No local results - show BGG button (only if not catalog-only)
          setResults([]);
          setShowBggButton(!filters.catalogOnly);
        }
      } catch (_err) {
        if ((_err as Error).name !== 'AbortError') {
          setError('Errore nella ricerca. Riprova.');
          setShowBggButton(!filters.catalogOnly);
        }
      } finally {
        setLoading(false);
      }
    };

    void searchCatalog();
  }, [debouncedTerm, filters]);

  // ============================================================================
  // Search BGG (Fallback)
  // ============================================================================

  const handleBggSearch = useCallback(async () => {
    if (!debouncedTerm.trim()) return;

    setBggLoading(true);
    setError(null);

    try {
      const response = await api.bgg.search(debouncedTerm, false, 1, PAGE_SIZE);

      if (response.results.length > 0) {
        // Map BGG results - limited info from search endpoint
        const bggResults: SharedGameSearchResult[] = response.results.map(item => ({
          id: `bgg-${item.bggId}`,
          title: item.name,
          yearPublished: item.yearPublished ?? 0,
          thumbnailUrl: item.thumbnailUrl ?? '',
          minPlayers: 0, // Not available in search results
          maxPlayers: 0, // Not available in search results
          playingTimeMinutes: 0, // Not available in search results
          averageRating: null, // Not available in search results
          source: 'bgg' as const,
          bggId: item.bggId,
        }));
        setResults(bggResults);
        setShowBggButton(false);
      } else {
        setResults([]);
        setError('Nessun gioco trovato su BoardGameGeek.');
      }
    } catch (_err) {
      setError('BoardGameGeek non disponibile. Riprova più tardi.');
    } finally {
      setBggLoading(false);
    }
  }, [debouncedTerm]);

  // ============================================================================
  // Handlers
  // ============================================================================

  const handleClear = useCallback(() => {
    setSearchTerm('');
    setResults([]);
    setShowBggButton(false);
    setError(null);
    inputRef.current?.focus();
  }, []);

  const handleSelect = useCallback(
    (game: SharedGameSearchResult) => {
      onSelect?.(game);
    },
    [onSelect]
  );

  const handleFiltersChange = useCallback((newFilters: SearchFilters) => {
    setFilters(newFilters);
  }, []);

  // Auto-focus
  useEffect(() => {
    if (autoFocus) {
      inputRef.current?.focus();
    }
  }, [autoFocus]);

  // ============================================================================
  // Render
  // ============================================================================

  return (
    <div className={cn('w-full space-y-3', className)}>
      {/* Search Input */}
      <div className="relative">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
        <Input
          ref={inputRef}
          type="search"
          placeholder={placeholder}
          value={searchTerm}
          onChange={e => setSearchTerm(e.target.value)}
          className="pl-10 pr-10"
          aria-label="Cerca giochi"
        />
        {searchTerm && (
          <Button
            variant="ghost"
            size="sm"
            onClick={handleClear}
            className="absolute right-1 top-1/2 -translate-y-1/2 h-7 w-7 p-0"
            aria-label="Cancella ricerca"
          >
            <X className="h-4 w-4" />
          </Button>
        )}
      </div>

      {/* Filters */}
      {showFilters && (
        <SharedGameSearchFilters filters={filters} onFiltersChange={handleFiltersChange} />
      )}

      {/* Results Dropdown */}
      {(results.length > 0 || loading || showBggButton || error) && (
        <Card className="shadow-lg max-h-[400px] overflow-auto">
          <CardContent className="p-2">
            {/* Loading State */}
            {loading && (
              <div className="space-y-2">
                {Array.from({ length: 3 }).map((_, i) => (
                  <Skeleton key={i} className="h-16 w-full" />
                ))}
              </div>
            )}

            {/* Error State */}
            {error && !loading && (
              <div className="p-4 text-center text-sm text-muted-foreground">{error}</div>
            )}

            {/* No Results State (after BGG search failed) */}
            {!loading &&
              !error &&
              results.length === 0 &&
              !showBggButton &&
              debouncedTerm.trim() && (
                <NoResultsState searchTerm={debouncedTerm} catalogOnly={filters.catalogOnly} />
              )}

            {/* Results List */}
            {!loading && results.length > 0 && (
              <div className="space-y-1">
                {results.map(game => (
                  <SharedGameSearchResultItem
                    key={game.id}
                    game={game}
                    onClick={() => handleSelect(game)}
                  />
                ))}
              </div>
            )}

            {/* BGG Fallback Button */}
            {showBggButton && !loading && (
              <div className="p-4 text-center">
                <p className="text-sm text-muted-foreground mb-3">
                  Nessun gioco trovato nel catalogo.
                </p>
                <Button onClick={handleBggSearch} disabled={bggLoading} variant="outline" size="sm">
                  {bggLoading ? (
                    <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                  ) : (
                    <ExternalLink className="h-4 w-4 mr-2" />
                  )}
                  Cerca su BoardGameGeek
                </Button>
              </div>
            )}
          </CardContent>
        </Card>
      )}
    </div>
  );
}

// ============================================================================
// Result Item Sub-Component
// ============================================================================

interface SharedGameSearchResultItemProps {
  game: SharedGameSearchResult;
  onClick: () => void;
}

function SharedGameSearchResultItem({ game, onClick }: SharedGameSearchResultItemProps) {
  return (
    <button
      type="button"
      onClick={onClick}
      className="w-full flex items-center gap-3 p-2 rounded-md hover:bg-muted transition-colors text-left"
    >
      {/* Thumbnail */}
      <div className="w-12 h-12 rounded-md bg-muted overflow-hidden flex-shrink-0">
        {game.thumbnailUrl ? (
          <img
            src={game.thumbnailUrl}
            alt=""
            className="w-full h-full object-cover"
            loading="lazy"
          />
        ) : (
          <div className="w-full h-full flex items-center justify-center text-2xl">🎲</div>
        )}
      </div>

      {/* Info */}
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2">
          <span className="font-medium text-sm truncate">{game.title}</span>
          <Badge
            variant={game.source === 'catalog' ? 'default' : 'secondary'}
            className="text-xs flex-shrink-0"
          >
            {game.source === 'catalog' ? 'Catalogo' : 'BGG'}
          </Badge>
        </div>
        <div className="text-xs text-muted-foreground flex items-center gap-2 mt-0.5">
          {game.yearPublished > 0 && <span>{game.yearPublished}</span>}
          {game.minPlayers > 0 && game.maxPlayers > 0 && (
            <>
              {game.yearPublished > 0 && <span>•</span>}
              <span>
                {game.minPlayers === game.maxPlayers
                  ? `${game.minPlayers} giocatori`
                  : `${game.minPlayers}-${game.maxPlayers} giocatori`}
              </span>
            </>
          )}
          {game.averageRating && game.averageRating > 0 && (
            <>
              <span>•</span>
              <span>★ {game.averageRating.toFixed(1)}</span>
            </>
          )}
        </div>
      </div>
    </button>
  );
}
