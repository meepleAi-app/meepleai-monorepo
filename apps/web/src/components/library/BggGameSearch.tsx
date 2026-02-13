/**
 * BGG Game Search Component
 * Issue #4053: User-Facing BGG Search for Private Game Creation
 *
 * Reusable BGG search using the public /api/v1/bgg/search endpoint.
 * Features:
 * - Debounced autocomplete (300ms)
 * - Keyboard navigation (Arrow Up/Down, Enter, Escape)
 * - Game thumbnails and year badges
 * - Exact search mode
 * - Error handling with retry
 */

'use client';

import { useState, useCallback, useRef, useEffect } from 'react';

import { Search, Loader2, ExternalLink, AlertCircle } from 'lucide-react';
import { toast } from 'sonner';

import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { api } from '@/lib/api';
import type { BggSearchResult } from '@/lib/api/schemas/games.schemas';
import { cn } from '@/lib/utils';
import { useBggRateLimit, formatCountdown } from '@/lib/hooks/use-bgg-rate-limit';

export interface BggGameSearchProps {
  onSelect: (result: BggSearchResult) => void;
  placeholder?: string;
}

export function BggGameSearch({
  onSelect,
  placeholder = 'Cerca un gioco su BoardGameGeek...',
}: BggGameSearchProps) {
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<BggSearchResult[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [isOpen, setIsOpen] = useState(false);
  const [highlightedIndex, setHighlightedIndex] = useState(-1);
  const [error, setError] = useState<string | null>(null);

  // Issue #4274: BGG rate limit tracking
  const { quota, updateFromHeaders } = useBggRateLimit();

  const inputRef = useRef<HTMLInputElement>(null);
  const listRef = useRef<HTMLUListElement>(null);
  const debounceRef = useRef<NodeJS.Timeout | null>(null);
  const containerRef = useRef<HTMLDivElement>(null);

  const performSearch = useCallback(async (searchQuery: string, exact: boolean = false) => {
    if (searchQuery.length < 2) {
      setResults([]);
      setIsOpen(false);
      return;
    }

    setIsLoading(true);
    setError(null);

    try {
      const response = await api.bgg.search(searchQuery, exact);
      const searchResults = response.results;
      setResults(searchResults);
      setIsOpen(true);
      setHighlightedIndex(-1);

      if (exact && searchResults.length === 0) {
        toast.info('Nessun risultato per la ricerca esatta');
      }
    } catch (err) {
      console.error('BGG search error:', err);
      setError('Errore nella ricerca BGG. Riprova.');
      setResults([]);
      setIsOpen(false);
    } finally {
      setIsLoading(false);
    }
  }, []);

  const handleInputChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const value = e.target.value;
    setQuery(value);

    if (debounceRef.current) {
      clearTimeout(debounceRef.current);
    }

    debounceRef.current = setTimeout(() => {
      performSearch(value);
    }, 300);
  }, [performSearch]);

  const handleKeyDown = useCallback((e: React.KeyboardEvent<HTMLInputElement>) => {
    if (!isOpen || results.length === 0) return;

    switch (e.key) {
      case 'ArrowDown':
        e.preventDefault();
        setHighlightedIndex(prev => Math.min(prev + 1, results.length - 1));
        break;
      case 'ArrowUp':
        e.preventDefault();
        setHighlightedIndex(prev => Math.max(prev - 1, 0));
        break;
      case 'Enter':
        e.preventDefault();
        if (highlightedIndex >= 0 && highlightedIndex < results.length) {
          handleResultClick(results[highlightedIndex]);
        }
        break;
      case 'Escape':
        setIsOpen(false);
        break;
    }
  }, [isOpen, results, highlightedIndex]); // eslint-disable-line react-hooks/exhaustive-deps

  const handleResultClick = useCallback((result: BggSearchResult) => {
    onSelect(result);
    setIsOpen(false);
    setQuery(result.name);
  }, [onSelect]);

  const handleExactSearch = useCallback(() => {
    if (query.length < 2) {
      toast.error('Inserisci almeno 2 caratteri');
      return;
    }
    performSearch(query, true);
  }, [query, performSearch]);

  // Close dropdown on outside click
  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setIsOpen(false);
      }
    };

    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  // Cleanup debounce on unmount
  useEffect(() => {
    return () => {
      if (debounceRef.current) {
        clearTimeout(debounceRef.current);
      }
    };
  }, []);

  return (
    <div className="relative" ref={containerRef} data-testid="bgg-game-search">
      {/* Search Input */}
      <div className="flex gap-2">
        <div className="relative flex-1">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input
            ref={inputRef}
            type="text"
            placeholder={placeholder}
            value={query}
            onChange={handleInputChange}
            onKeyDown={handleKeyDown}
            onFocus={() => results.length > 0 && setIsOpen(true)}
            className="pl-10"
            aria-label="Search BoardGameGeek"
            aria-expanded={isOpen}
            aria-controls="bgg-search-results"
            role="combobox"
            aria-autocomplete="list"
            data-testid="bgg-search-input"
          />
          {isLoading && (
            <Loader2 className="absolute right-3 top-1/2 -translate-y-1/2 h-4 w-4 animate-spin text-muted-foreground" />
          )}
        </div>
        <Button
          variant="outline"
          onClick={handleExactSearch}
          disabled={isLoading || query.length < 2}
          title="Ricerca esatta"
          data-testid="bgg-exact-search-btn"
        >
          Ricerca Esatta
        </Button>
      </div>

      {/* Error Message */}
      {error && (
        <div className="flex items-center gap-2 mt-2 text-sm text-destructive" data-testid="bgg-search-error">
          <AlertCircle className="h-4 w-4" />
          {error}
        </div>
      )}

      {/* Results Dropdown */}
      {isOpen && results.length > 0 && (
        <ul
          id="bgg-search-results"
          ref={listRef}
          className="absolute z-50 w-full mt-1 bg-background border rounded-md shadow-lg max-h-80 overflow-y-auto"
          role="listbox"
          data-testid="bgg-search-results"
        >
          {results.map((result, index) => (
            <li
              key={result.bggId}
              role="option"
              aria-selected={index === highlightedIndex}
              className={cn(
                'flex items-center gap-3 px-3 py-2 cursor-pointer transition-colors',
                index === highlightedIndex && 'bg-accent',
                'hover:bg-accent'
              )}
              onClick={() => handleResultClick(result)}
              onMouseEnter={() => setHighlightedIndex(index)}
              data-testid={`bgg-search-result-${index}`}
            >
              {/* Thumbnail */}
              {result.thumbnailUrl ? (
                // eslint-disable-next-line @next/next/no-img-element
                <img
                  src={result.thumbnailUrl}
                  alt={result.name}
                  className="h-12 w-12 rounded object-cover flex-shrink-0"
                />
              ) : (
                <div className="h-12 w-12 rounded bg-muted flex items-center justify-center flex-shrink-0">
                  <Search className="h-5 w-5 text-muted-foreground" />
                </div>
              )}

              {/* Info */}
              <div className="flex-1 min-w-0">
                <div className="font-medium truncate">{result.name}</div>
                <div className="text-sm text-muted-foreground flex items-center gap-2">
                  {result.yearPublished && <span>{result.yearPublished}</span>}
                  <span className="text-xs bg-secondary px-1.5 py-0.5 rounded capitalize">
                    {result.type === 'boardgame' ? 'Gioco' : result.type}
                  </span>
                </div>
              </div>

              {/* BGG Link */}
              <a
                href={`https://boardgamegeek.com/boardgame/${result.bggId}`}
                target="_blank"
                rel="noopener noreferrer"
                className="p-1 hover:bg-background rounded"
                onClick={(e) => e.stopPropagation()}
                title="Vedi su BGG"
              >
                <ExternalLink className="h-4 w-4 text-muted-foreground" />
              </a>
            </li>
          ))}
        </ul>
      )}

      {/* Empty State */}
      {isOpen && results.length === 0 && !isLoading && query.length >= 2 && (
        <div
          className="absolute z-50 w-full mt-1 bg-background border rounded-md shadow-lg p-4 text-center text-muted-foreground"
          data-testid="bgg-search-empty"
        >
          Nessun gioco trovato per &quot;{query}&quot;
        </div>
      )}
    </div>
  );
}
