'use client';

/**
 * CatalogSearchStep — Step 1b of AddGameDrawer
 * Issue #5169 — Search shared catalog and add game to personal library
 *
 * Provides:
 * - Debounced (300ms) search input
 * - Grid of selectable game cards
 * - Loading skeletons
 * - Pagination (CatalogPagination)
 * - "Already in library" disabled state via useGameInLibraryStatus
 * - Calls POST /api/v1/library/games/{gameId} on select via useAddGameToLibrary
 * - On success → calls onSelect(gameId, gameName) to advance wizard to PDF step
 */

import { useState, useEffect, useRef, useCallback } from 'react';

import { ArrowLeft, Search } from 'lucide-react';
import Image from 'next/image';

import { CatalogPagination } from '@/components/catalog/CatalogPagination';
import { toast } from '@/components/layout';
import { Button } from '@/components/ui/primitives/button';
import { useGameInLibraryStatus, useSharedGames } from '@/hooks/queries';
import { useAddGameToLibrary } from '@/hooks/queries/useLibrary';
import type { SharedGame } from '@/lib/api';

// ─── Constants ────────────────────────────────────────────────────────────────

const PAGE_SIZE = 9;
const DEBOUNCE_MS = 300;

// ─── Single selectable card ───────────────────────────────────────────────────

interface SelectableGameCardProps {
  game: SharedGame;
  onSelect: (gameId: string, gameName: string) => void;
  isSelecting: boolean;
}

function SelectableGameCard({ game, onSelect, isSelecting }: SelectableGameCardProps) {
  const { data: status } = useGameInLibraryStatus(game.id);
  const inLibrary = status?.inLibrary ?? false;

  return (
    <div
      data-testid={`catalog-card-${game.id}`}
      className={[
        'flex flex-col rounded-lg border border-border/50 overflow-hidden',
        'transition-shadow',
        inLibrary ? 'opacity-60' : 'hover:shadow-md',
      ].join(' ')}
    >
      {/* Thumbnail */}
      <div className="relative aspect-video bg-muted overflow-hidden">
        {game.thumbnailUrl ? (
          <Image
            src={game.thumbnailUrl}
            alt={game.title}
            fill
            className="object-cover"
            sizes="(max-width: 640px) 100vw, 33vw"
          />
        ) : (
          <div className="w-full h-full flex items-center justify-center text-muted-foreground text-xs">
            No image
          </div>
        )}
        {inLibrary && (
          <div
            data-testid={`catalog-card-${game.id}-in-library-badge`}
            className="absolute top-1.5 right-1.5 bg-green-600 text-white text-xs font-medium px-1.5 py-0.5 rounded"
          >
            In library
          </div>
        )}
      </div>

      {/* Info */}
      <div className="p-3 flex flex-col gap-2 flex-1">
        <p
          className="font-medium text-sm leading-tight line-clamp-2"
          title={game.title}
        >
          {game.title}
        </p>
        {game.yearPublished > 0 && (
          <p className="text-xs text-muted-foreground">{game.yearPublished}</p>
        )}
        <Button
          size="sm"
          variant={inLibrary ? 'outline' : 'default'}
          disabled={inLibrary || isSelecting}
          onClick={() => onSelect(game.id, game.title)}
          data-testid={`catalog-card-${game.id}-select-btn`}
          className="mt-auto w-full"
        >
          {inLibrary ? 'Already in library' : 'Select'}
        </Button>
      </div>
    </div>
  );
}

// ─── Skeleton ─────────────────────────────────────────────────────────────────

function CardSkeleton() {
  return (
    <div className="flex flex-col rounded-lg border border-border/50 overflow-hidden animate-pulse">
      <div className="aspect-video bg-muted" />
      <div className="p-3 space-y-2">
        <div className="h-4 bg-muted rounded w-3/4" />
        <div className="h-3 bg-muted rounded w-1/4" />
        <div className="h-8 bg-muted rounded mt-2" />
      </div>
    </div>
  );
}

// ─── Main step ────────────────────────────────────────────────────────────────

interface CatalogSearchStepProps {
  /**
   * Called when user selects a game and it is successfully added to library.
   * Advance the wizard to the PDF step with gameId + gameName.
   */
  onSelect: (gameId: string, gameName: string) => void;
  /** Go back to Step 0 (choice) */
  onBack: () => void;
}

export function CatalogSearchStep({ onSelect, onBack }: CatalogSearchStepProps) {
  const [rawSearch, setRawSearch] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [page, setPage] = useState(1);
  const [selectingId, setSelectingId] = useState<string | null>(null);
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Debounce search input
  useEffect(() => {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => {
      setDebouncedSearch(rawSearch.trim());
      setPage(1);
    }, DEBOUNCE_MS);
    return () => {
      if (debounceRef.current) clearTimeout(debounceRef.current);
    };
  }, [rawSearch]);

  const { data, isLoading } = useSharedGames({
    searchTerm: debouncedSearch || undefined,
    page,
    pageSize: PAGE_SIZE,
    status: 2, // Published / Active
  });

  const addMutation = useAddGameToLibrary();

  const handleSelect = useCallback(
    async (gameId: string, gameName: string) => {
      setSelectingId(gameId);
      try {
        await addMutation.mutateAsync({ gameId });
        toast.success(`"${gameName}" added to your library!`);
        onSelect(gameId, gameName);
      } catch {
        toast.error(`Could not add "${gameName}". Please try again.`);
      } finally {
        setSelectingId(null);
      }
    },
    [addMutation, onSelect],
  );

  const totalPages = data ? Math.ceil(data.total / PAGE_SIZE) : 0;

  return (
    <div className="flex flex-col gap-4 px-4 py-4" data-testid="catalog-search-step">
      {/* Back + search row */}
      <div className="flex items-center gap-2">
        <Button
          variant="ghost"
          size="icon"
          onClick={onBack}
          aria-label="Back to choice"
          data-testid="catalog-search-back"
        >
          <ArrowLeft className="h-4 w-4" />
        </Button>

        <div className="relative flex-1">
          <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <input
            type="search"
            placeholder="Search games…"
            value={rawSearch}
            onChange={(e) => setRawSearch(e.target.value)}
            className={[
              'w-full pl-9 pr-3 py-2 text-sm rounded-md',
              'border border-input bg-background',
              'focus:outline-none focus:ring-2 focus:ring-ring',
            ].join(' ')}
            data-testid="catalog-search-input"
            aria-label="Search games"
          />
        </div>
      </div>

      {/* Results count */}
      {data && (
        <p className="text-xs text-muted-foreground" data-testid="catalog-search-count">
          {data.total} game{data.total !== 1 ? 's' : ''} found
        </p>
      )}

      {/* Grid */}
      <div
        className="grid grid-cols-2 sm:grid-cols-3 gap-3"
        data-testid="catalog-search-grid"
      >
        {isLoading
          ? Array.from({ length: PAGE_SIZE }).map((_, i) => <CardSkeleton key={i} />)
          : data?.items.map((game) => (
              <SelectableGameCard
                key={game.id}
                game={game}
                onSelect={handleSelect}
                isSelecting={selectingId === game.id}
              />
            ))}

        {!isLoading && data?.items.length === 0 && (
          <p
            className="col-span-full text-center text-sm text-muted-foreground py-8"
            data-testid="catalog-search-empty"
          >
            No games found for &quot;{debouncedSearch}&quot;.
          </p>
        )}
      </div>

      {/* Pagination */}
      {totalPages > 1 && (
        <CatalogPagination
          currentPage={page}
          totalPages={totalPages}
          onPageChange={setPage}
          totalResults={data?.total}
        />
      )}
    </div>
  );
}
