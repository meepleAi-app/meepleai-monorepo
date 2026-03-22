'use client';

/**
 * SearchExpander - Glassmorphism search bar with dropdown results
 *
 * Searches the shared game catalog with debounce (300ms).
 * Shows results with dual actions: "Vedi" (view) and "Chiedi" (ask AI).
 * Indicates if a game is already in the user's library.
 */

import { useState, useEffect, useRef, useCallback, useMemo } from 'react';

import { Search, Loader2 } from 'lucide-react';

import { useLibrary } from '@/hooks/queries/useLibrary';
import { api } from '@/lib/api';
import type { SharedGame } from '@/lib/api/schemas/shared-games.schemas';

// ========== Types ==========

export interface SharedGameSearchResult extends SharedGame {
  isInLibrary?: boolean;
}

export interface SearchExpanderProps {
  isOpen: boolean;
  onClose: () => void;
  onViewGame: (gameId: string) => void;
  onAskAboutGame: (game: SharedGameSearchResult) => void;
}

// ========== Constants ==========

const DEBOUNCE_MS = 300;
const PAGE_SIZE = 5;

// ========== Component ==========

export function SearchExpander({
  isOpen,
  onClose,
  onViewGame,
  onAskAboutGame,
}: SearchExpanderProps) {
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<SharedGame[]>([]);
  const [isSearching, setIsSearching] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Fetch user library to check which games are already owned
  const { data: libraryData } = useLibrary();

  // Build a Set of library game IDs for O(1) lookup
  const libraryGameIds = useMemo(() => {
    if (!libraryData?.items) return new Set<string>();
    return new Set(libraryData.items.map(entry => entry.gameId));
  }, [libraryData?.items]);

  // Debounced search
  const performSearch = useCallback(async (searchTerm: string) => {
    if (!searchTerm.trim()) {
      setResults([]);
      setIsSearching(false);
      return;
    }

    setIsSearching(true);
    try {
      const response = await api.sharedGames.search({
        searchTerm,
        pageSize: PAGE_SIZE,
      });
      setResults(response.items);
    } catch {
      setResults([]);
    } finally {
      setIsSearching(false);
    }
  }, []);

  // Handle input change with debounce
  useEffect(() => {
    if (debounceRef.current) {
      clearTimeout(debounceRef.current);
    }

    if (!query.trim()) {
      setResults([]);
      return;
    }

    debounceRef.current = setTimeout(() => {
      performSearch(query);
    }, DEBOUNCE_MS);

    return () => {
      if (debounceRef.current) {
        clearTimeout(debounceRef.current);
      }
    };
  }, [query, performSearch]);

  // Auto-focus input when opened
  useEffect(() => {
    if (isOpen) {
      // Small delay to ensure the DOM is ready
      const timer = setTimeout(() => {
        inputRef.current?.focus();
      }, 50);
      return () => clearTimeout(timer);
    } else {
      // Reset state when closed
      setQuery('');
      setResults([]);
    }
  }, [isOpen]);

  // ESC key handler
  useEffect(() => {
    if (!isOpen) return;

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        onClose();
      }
    };

    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, onClose]);

  // Don't render when closed
  if (!isOpen) return null;

  // Enrich results with library status
  const enrichedResults: SharedGameSearchResult[] = results.map(game => ({
    ...game,
    isInLibrary: libraryGameIds.has(game.id),
  }));

  const formatPlayers = (min: number, max: number) => {
    if (min === max) return `${min} giocatori`;
    return `${min}-${max} giocatori`;
  };

  return (
    <div data-testid="search-expander" className="absolute inset-x-4 bottom-20 z-50">
      {/* Search Input */}
      <div className="bg-white/5 backdrop-blur-xl border border-white/10 rounded-2xl p-3 shadow-2xl">
        <div className="relative">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-white/40" />
          <input
            ref={inputRef}
            type="text"
            value={query}
            onChange={e => setQuery(e.target.value)}
            placeholder="Cerca un gioco..."
            className="w-full bg-white/5 border border-white/10 rounded-xl py-2.5 pl-10 pr-4 text-sm text-white placeholder:text-white/40 focus:outline-none focus:ring-1 focus:ring-white/20"
          />
          {isSearching && (
            <Loader2 className="absolute right-3 top-1/2 -translate-y-1/2 h-4 w-4 text-white/40 animate-spin" />
          )}
        </div>

        {/* Results Dropdown */}
        {enrichedResults.length > 0 && (
          <div className="mt-2 space-y-1 max-h-[280px] overflow-y-auto">
            {enrichedResults.map(game => (
              <div
                key={game.id}
                className="flex items-center gap-3 p-2 rounded-xl hover:bg-white/5 transition-colors"
              >
                {/* Thumbnail */}
                {game.thumbnailUrl && (
                  <img
                    src={game.thumbnailUrl}
                    alt={game.title}
                    className="h-10 w-10 rounded-lg object-cover flex-shrink-0"
                  />
                )}

                {/* Game Info */}
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2">
                    <span className="text-sm font-medium text-white truncate">{game.title}</span>
                    {game.isInLibrary && (
                      <span className="text-[10px] px-1.5 py-0.5 rounded-full bg-emerald-500/20 text-emerald-400 whitespace-nowrap">
                        ✓ In libreria
                      </span>
                    )}
                  </div>
                  <span className="text-xs text-white/40">
                    {formatPlayers(game.minPlayers, game.maxPlayers)}
                  </span>
                </div>

                {/* Action Buttons */}
                <div className="flex items-center gap-1.5 flex-shrink-0">
                  <button
                    onClick={() => onViewGame(game.id)}
                    className="px-2.5 py-1 text-xs font-medium rounded-lg bg-blue-500/20 text-blue-400 hover:bg-blue-500/30 transition-colors"
                  >
                    Vedi
                  </button>
                  <button
                    onClick={() => onAskAboutGame(game)}
                    className={`px-2.5 py-1 text-xs font-medium rounded-lg transition-colors ${
                      game.isInLibrary
                        ? 'bg-emerald-500/20 text-emerald-400 hover:bg-emerald-500/30'
                        : 'bg-purple-500/20 text-purple-400 hover:bg-purple-500/30'
                    }`}
                  >
                    Chiedi
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}

        {/* Empty state when searching with no results */}
        {query.trim() && !isSearching && results.length === 0 && (
          <div className="mt-2 py-4 text-center text-xs text-white/30">Nessun gioco trovato</div>
        )}
      </div>
    </div>
  );
}
