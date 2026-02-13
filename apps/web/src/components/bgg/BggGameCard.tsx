/**
 * BggGameCard - Issue #4141
 *
 * Card component for BGG search results in wizard.
 */

import { Check } from 'lucide-react';

import { cn } from '@/lib/utils';
import type { BggSearchResult } from '@/types/bgg';

interface BggGameCardProps {
  game: BggSearchResult;
  selected?: boolean;
  onSelect: (gameId: number) => void;
}

/**
 * BggGameCard component
 *
 * Displays a BGG game search result with thumbnail, name, and year.
 * Supports selection state with visual feedback.
 */
export function BggGameCard({ game, selected = false, onSelect }: BggGameCardProps) {
  return (
    <button
      type="button"
      onClick={() => onSelect(game.id)}
      className={cn(
        'relative w-full p-4 rounded-lg border-2 transition-all',
        'bg-white/70 backdrop-blur-md hover:bg-white/90',
        'focus:outline-none focus:ring-2 focus:ring-amber-500 focus:ring-offset-2',
        selected
          ? 'border-amber-500 bg-amber-50/70'
          : 'border-gray-200 hover:border-amber-300'
      )}
      aria-pressed={selected}
      aria-label={`Select ${game.name} (${game.yearPublished})`}
    >
      {/* Selection indicator */}
      {selected && (
        <div className="absolute top-2 right-2 w-6 h-6 rounded-full bg-amber-500 text-white flex items-center justify-center">
          <Check className="w-4 h-4" aria-hidden="true" />
        </div>
      )}

      <div className="flex items-start gap-3">
        {/* Thumbnail */}
        {game.thumbnail ? (
          <img
            src={game.thumbnail}
            alt={game.name}
            className="w-16 h-16 rounded object-cover flex-shrink-0"
            loading="lazy"
          />
        ) : (
          <div className="w-16 h-16 rounded bg-gray-200 flex items-center justify-center flex-shrink-0">
            <span className="text-xs text-gray-500">No image</span>
          </div>
        )}

        {/* Game info */}
        <div className="flex-1 text-left min-w-0">
          <h3 className="font-quicksand font-bold text-base text-gray-900 truncate">
            {game.name}
          </h3>
          <p className="font-nunito text-sm text-gray-600 mt-1">
            Published: {game.yearPublished}
          </p>
        </div>
      </div>
    </button>
  );
}
