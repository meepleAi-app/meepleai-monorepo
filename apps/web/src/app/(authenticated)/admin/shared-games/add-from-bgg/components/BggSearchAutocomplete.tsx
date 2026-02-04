'use client';

/**
 * BGG Search Autocomplete Component
 * Issue: Admin Add Shared Game from BGG flow
 *
 * Features:
 * - Live autocomplete while typing (debounced 300ms)
 * - Displays game thumbnail, name, year
 * - Handles loading and error states
 * - Supports keyboard navigation
 */

import { useState, useCallback, useRef, useEffect } from 'react';

import { Search, Loader2, ExternalLink, AlertCircle } from 'lucide-react';
import { toast } from 'sonner';

import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { api } from '@/lib/api';
import type { BggSearchResult } from '@/lib/api/schemas/shared-games.schemas';
import { cn } from '@/lib/utils';

export interface BggSearchAutocompleteProps {
  onSelect: (result: BggSearchResult) => void;
}

export function BggSearchAutocomplete({ onSelect }: BggSearchAutocompleteProps) {
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<BggSearchResult[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [isOpen, setIsOpen] = useState(false);
  const [highlightedIndex, setHighlightedIndex] = useState(-1);
  const [error, setError] = useState<string | null>(null);

  const inputRef = useRef<HTMLInputElement>(null);
  const listRef = useRef<HTMLUListElement>(null);
  const debounceRef = useRef<NodeJS.Timeout | null>(null);

  // Debounced search
  const performSearch = useCallback(async (searchQuery: string) => {
    if (searchQuery.length < 2) {
      setResults([]);
      setIsOpen(false);
      return;
    }

    setIsLoading(true);
    setError(null);

    try {
      const searchResults = await api.sharedGames.searchBgg(searchQuery, false);
      setResults(searchResults);
      setIsOpen(searchResults.length > 0);
      setHighlightedIndex(-1);
    } catch (err) {
      console.error('BGG search error:', err);
      setError('Errore nella ricerca. Riprova.');
      setResults([]);
      setIsOpen(false);
    } finally {
      setIsLoading(false);
    }
  }, []);

  // Handle input change with debounce
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

  // Handle keyboard navigation
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
           
          onSelect(results[highlightedIndex]);
          setIsOpen(false);
        }
        break;
      case 'Escape':
        setIsOpen(false);
        break;
    }
  }, [isOpen, results, highlightedIndex, onSelect]);

  // Handle result click
  const handleResultClick = useCallback((result: BggSearchResult) => {
    onSelect(result);
    setIsOpen(false);
    setQuery(result.name);
  }, [onSelect]);

  // Handle exact search button
  const handleExactSearch = useCallback(async () => {
    if (query.length < 2) {
      toast.error('Inserisci almeno 2 caratteri');
      return;
    }

    setIsLoading(true);
    setError(null);

    try {
      const searchResults = await api.sharedGames.searchBgg(query, true);
      setResults(searchResults);
      setIsOpen(searchResults.length > 0);
      setHighlightedIndex(-1);

      if (searchResults.length === 0) {
        toast.info('Nessun risultato per la ricerca esatta');
      }
    } catch (err) {
      console.error('BGG exact search error:', err);
      setError('Errore nella ricerca. Riprova.');
      toast.error('Errore nella ricerca');
    } finally {
      setIsLoading(false);
    }
  }, [query]);

  // Close dropdown on outside click
  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (
        inputRef.current &&
        !inputRef.current.contains(e.target as Node) &&
        listRef.current &&
        !listRef.current.contains(e.target as Node)
      ) {
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
    <div className="relative">
      {/* Search Input */}
      <div className="flex gap-2">
        <div className="relative flex-1">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input
            ref={inputRef}
            type="text"
            placeholder="Cerca un gioco su BoardGameGeek..."
            value={query}
            onChange={handleInputChange}
            onKeyDown={handleKeyDown}
            onFocus={() => results.length > 0 && setIsOpen(true)}
            className="pl-10"
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
        >
          Ricerca Esatta
        </Button>
      </div>

      {/* Error Message */}
      {error && (
        <div className="flex items-center gap-2 mt-2 text-sm text-destructive">
          <AlertCircle className="h-4 w-4" />
          {error}
        </div>
      )}

      {/* Results Dropdown */}
      {isOpen && results.length > 0 && (
        <ul
          ref={listRef}
          className="absolute z-50 w-full mt-1 bg-background border rounded-md shadow-lg max-h-80 overflow-y-auto"
          role="listbox"
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
        <div className="absolute z-50 w-full mt-1 bg-background border rounded-md shadow-lg p-4 text-center text-muted-foreground">
          Nessun gioco trovato per "{query}"
        </div>
      )}
    </div>
  );
}
