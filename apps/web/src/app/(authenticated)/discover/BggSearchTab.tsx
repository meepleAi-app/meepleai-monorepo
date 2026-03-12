'use client';

/**
 * BggSearchTab - BGG direct search tab for the Discover page.
 * Task 1: BGG Search Tab on Discover Page
 *
 * Allows users to search BoardGameGeek directly and open the AddGame wizard
 * for any result, pre-seeded with the BGG ID.
 */

import { useCallback, useEffect, useRef, useState } from 'react';

import { ChevronDown, Loader2, Lock, Search, X } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { useAddGameWizard } from '@/components/library/add-game-sheet/AddGameWizardProvider';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import { Input } from '@/components/ui/primitives/input';
import { useAddPrivateGameFromBgg } from '@/hooks/queries/useLibrary';
import { api } from '@/lib/api';
import type { BggSearchResult } from '@/lib/api/schemas';

const DEBOUNCE_MS = 400;

// ============================================================================
// BggSearchTab
// ============================================================================

export function BggSearchTab() {
  const { openWizard } = useAddGameWizard();
  const router = useRouter();
  const addPrivateGameMutation = useAddPrivateGameFromBgg();

  const inputRef = useRef<HTMLInputElement>(null);

  const [searchTerm, setSearchTerm] = useState('');
  const [debouncedTerm, setDebouncedTerm] = useState('');
  const [results, setResults] = useState<BggSearchResult[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Debounce
  useEffect(() => {
    const timer = setTimeout(() => setDebouncedTerm(searchTerm), DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [searchTerm]);

  // Search BGG when debounced term changes
  useEffect(() => {
    if (!debouncedTerm.trim()) {
      setResults([]);
      setError(null);
      return;
    }

    let stale = false;

    const doSearch = async () => {
      setLoading(true);
      setError(null);
      try {
        const response = await api.bgg.search(debouncedTerm, false, 1, 20);
        if (!stale) {
          setResults(response.results);
        }
      } catch {
        if (!stale) {
          setError('BoardGameGeek non disponibile. Riprova più tardi.');
        }
      } finally {
        if (!stale) setLoading(false);
      }
    };

    void doSearch();

    return () => {
      stale = true;
    };
  }, [debouncedTerm]);

  const handleSelect = useCallback(
    (game: BggSearchResult) => {
      openWizard({ type: 'fromSearch', bggId: game.bggId });
    },
    [openWizard]
  );

  const handleAddAsPrivate = useCallback(
    (game: BggSearchResult) => {
      addPrivateGameMutation.mutate(
        {
          source: 'BoardGameGeek',
          bggId: game.bggId,
          title: game.name,
          minPlayers: 1,
          maxPlayers: 4,
          yearPublished: game.yearPublished ?? undefined,
          thumbnailUrl: game.thumbnailUrl ?? undefined,
        },
        {
          onSuccess: data => {
            router.push(`/library/private/${data.id}`);
          },
        }
      );
    },
    [addPrivateGameMutation, router]
  );

  const handleClear = useCallback(() => {
    setSearchTerm('');
    setResults([]);
    setError(null);
    inputRef.current?.focus();
  }, []);

  return (
    <div className="min-h-screen bg-background">
      <div className="mx-auto max-w-3xl px-4 py-8 sm:px-6 lg:px-8">
        {/* Header */}
        <div className="mb-6">
          <h1 className="text-2xl font-bold tracking-tight text-foreground">
            Cerca su BoardGameGeek
          </h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Trova un gioco su BGG e aggiungilo alla tua collezione.
          </p>
        </div>

        {/* Search Input */}
        <div className="relative mb-6">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input
            ref={inputRef}
            type="search"
            placeholder="Cerca su BoardGameGeek..."
            value={searchTerm}
            onChange={e => setSearchTerm(e.target.value)}
            className="pl-10 pr-10"
            aria-label="Cerca su BoardGameGeek"
          />
          {searchTerm && (
            <Button
              variant="ghost"
              size="sm"
              onClick={handleClear}
              className="absolute right-1 top-1/2 -translate-y-1/2 h-7 w-7 p-0 text-muted-foreground"
              aria-label="Cancella ricerca"
            >
              <X className="h-4 w-4" />
            </Button>
          )}
        </div>

        {/* Loading */}
        {loading && (
          <div className="flex items-center justify-center py-12">
            <Loader2 className="h-6 w-6 animate-spin text-amber-500" />
          </div>
        )}

        {/* API error */}
        {!loading && error && <p className="text-sm text-center text-red-500 py-8">{error}</p>}

        {/* No results (empty success, not an error) */}
        {!loading && results.length === 0 && debouncedTerm.trim() && !error && (
          <p className="text-sm text-center text-muted-foreground py-8">
            Nessun risultato per &quot;{debouncedTerm}&quot;
          </p>
        )}

        {/* Results */}
        {!loading && results.length > 0 && (
          <ul className="space-y-2">
            {results.map(game => (
              <li key={game.bggId}>
                <div className="w-full flex items-center gap-4 rounded-lg border border-border bg-card p-3 text-left hover:border-amber-500/50 hover:bg-amber-500/5 transition-colors">
                  {/* Thumbnail */}
                  <button
                    type="button"
                    onClick={() => handleSelect(game)}
                    className="flex items-center gap-4 flex-1 min-w-0"
                  >
                    <div className="h-12 w-12 flex-shrink-0 rounded bg-muted overflow-hidden">
                      {game.thumbnailUrl ? (
                        // eslint-disable-next-line @next/next/no-img-element
                        <img
                          src={game.thumbnailUrl}
                          alt={game.name}
                          className="h-full w-full object-cover"
                        />
                      ) : (
                        <div className="h-full w-full flex items-center justify-center text-muted-foreground text-xs">
                          BGG
                        </div>
                      )}
                    </div>

                    {/* Info */}
                    <div className="flex-1 min-w-0">
                      <p className="text-sm font-medium text-foreground truncate">{game.name}</p>
                      <p className="text-xs text-muted-foreground">
                        {game.yearPublished ? `${game.yearPublished} · ` : ''}
                        BGG #{game.bggId}
                      </p>
                    </div>
                  </button>

                  {/* CTA with dropdown */}
                  <div className="flex-shrink-0 flex items-center gap-1">
                    <button
                      type="button"
                      onClick={() => handleSelect(game)}
                      className="text-xs text-amber-500 font-medium hover:text-amber-400 transition-colors"
                    >
                      Aggiungi
                    </button>
                    <DropdownMenu>
                      <DropdownMenuTrigger asChild>
                        <Button
                          variant="ghost"
                          size="sm"
                          className="h-6 w-6 p-0 text-muted-foreground hover:text-amber-500"
                          aria-label={`Opzioni per ${game.name}`}
                        >
                          <ChevronDown className="h-3.5 w-3.5" />
                        </Button>
                      </DropdownMenuTrigger>
                      <DropdownMenuContent align="end" className="w-48">
                        <DropdownMenuItem onClick={() => handleSelect(game)}>
                          Aggiungi al catalogo
                        </DropdownMenuItem>
                        <DropdownMenuItem
                          onClick={() => handleAddAsPrivate(game)}
                          disabled={addPrivateGameMutation.isPending}
                        >
                          <Lock className="mr-2 h-3.5 w-3.5" />
                          {addPrivateGameMutation.isPending
                            ? 'Aggiunta in corso...'
                            : 'Aggiungi come privato'}
                        </DropdownMenuItem>
                      </DropdownMenuContent>
                    </DropdownMenu>
                  </div>
                </div>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}
