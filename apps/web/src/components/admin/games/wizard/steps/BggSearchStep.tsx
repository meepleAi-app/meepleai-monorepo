'use client';

/**
 * BGG Search Step
 * Step 1 of the Admin Game Wizard.
 * Debounced search with retry backoff, result cards with selection.
 */

import { useState, useDeferredValue } from 'react';

import { SearchIcon, CalendarIcon, LoaderCircleIcon, AlertTriangleIcon } from 'lucide-react';
import Image from 'next/image';

import { Card, CardContent } from '@/components/ui/data-display/card';
import { Input } from '@/components/ui/primitives/input';
import { useSearchBggGames } from '@/hooks/queries/useSearchBggGames';
import type { BggSearchResult } from '@/lib/api/schemas/games.schemas';

// ─── Types ───────────────────────────────────────────────────────────────────

interface BggSearchStepProps {
  onGameSelected: (game: BggSearchResult) => void;
}

// ─── Component ───────────────────────────────────────────────────────────────

export function BggSearchStep({ onGameSelected }: BggSearchStepProps) {
  const [query, setQuery] = useState('');
  const deferredQuery = useDeferredValue(query);

  const { data, isLoading, isFetching, error } = useSearchBggGames({
    query: deferredQuery,
  });

  const results = data?.results ?? [];
  const showResults = deferredQuery.trim().length >= 2;

  return (
    <div className="space-y-4">
      {/* Search Input */}
      <div className="relative">
        <SearchIcon className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
        <Input
          type="text"
          placeholder="Search BoardGameGeek (e.g. Catan, Wingspan, Gloomhaven)..."
          value={query}
          onChange={e => setQuery(e.target.value)}
          className="pl-10 h-11"
          autoFocus
        />
        {isFetching && (
          <LoaderCircleIcon className="absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 animate-spin text-muted-foreground" />
        )}
      </div>

      {/* Hint */}
      {!showResults && !error && (
        <p className="text-sm text-muted-foreground">
          Type at least 2 characters to search BoardGameGeek
        </p>
      )}

      {/* Error */}
      {error && (
        <div className="flex items-start gap-2 rounded-lg border border-red-200 dark:border-red-900/40 bg-red-50 dark:bg-red-950/20 p-3">
          <AlertTriangleIcon className="h-4 w-4 mt-0.5 text-red-500 shrink-0" />
          <div className="text-sm">
            <p className="font-medium text-red-700 dark:text-red-400">Search failed</p>
            <p className="text-red-600 dark:text-red-400/80">
              {error.message}. Retrying automatically...
            </p>
          </div>
        </div>
      )}

      {/* Loading skeleton */}
      {isLoading && showResults && (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
          {Array.from({ length: 4 }).map((_, i) => (
            <div
              key={i}
              className="h-24 bg-white/40 dark:bg-zinc-800/40 backdrop-blur-sm rounded-xl border border-slate-200/60 dark:border-zinc-700/40 animate-pulse"
            />
          ))}
        </div>
      )}

      {/* Results */}
      {showResults && !isLoading && results.length > 0 && (
        <div className="space-y-2">
          <p className="text-sm text-muted-foreground">
            {data?.total ?? results.length} results found
          </p>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
            {results.map(game => (
              <BggGameCard key={game.bggId} game={game} onSelect={onGameSelected} />
            ))}
          </div>
        </div>
      )}

      {/* No results */}
      {showResults && !isLoading && !error && results.length === 0 && (
        <p className="text-sm text-muted-foreground py-8 text-center">
          No games found for &quot;{deferredQuery}&quot;. Try a different search term.
        </p>
      )}
    </div>
  );
}

// ─── Sub-component ───────────────────────────────────────────────────────────

function BggGameCard({
  game,
  onSelect,
}: {
  game: BggSearchResult;
  onSelect: (game: BggSearchResult) => void;
}) {
  return (
    <Card
      className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl border-slate-200/60 dark:border-zinc-700/60 hover:border-amber-300/60 dark:hover:border-amber-600/40 transition-all cursor-pointer group"
      onClick={() => onSelect(game)}
    >
      <CardContent className="flex items-center gap-3 p-3">
        {/* Thumbnail */}
        {game.thumbnailUrl ? (
          <Image
            src={game.thumbnailUrl}
            alt={game.name}
            width={64}
            height={64}
            unoptimized
            className="h-16 w-16 rounded-lg object-cover shrink-0 bg-slate-100 dark:bg-zinc-700"
          />
        ) : (
          <div className="h-16 w-16 rounded-lg shrink-0 bg-slate-100 dark:bg-zinc-700 flex items-center justify-center">
            <span className="text-2xl">🎲</span>
          </div>
        )}

        {/* Info */}
        <div className="min-w-0 flex-1">
          <p className="font-medium text-sm text-foreground truncate group-hover:text-amber-600 dark:group-hover:text-amber-400 transition-colors">
            {game.name}
          </p>
          <div className="flex items-center gap-2 mt-1">
            {game.yearPublished && (
              <span className="inline-flex items-center gap-1 text-xs text-muted-foreground">
                <CalendarIcon className="h-3 w-3" />
                {game.yearPublished}
              </span>
            )}
            <span className="text-xs text-muted-foreground/60">BGG #{game.bggId}</span>
          </div>
          <span className="text-xs text-muted-foreground capitalize">{game.type}</span>
        </div>
      </CardContent>
    </Card>
  );
}
