/**
 * GameSearchResults - Search results list for game source step.
 * Issue #4819: AddGameSheet Step 1 - Game Source
 * Epic #4817: User Collection Wizard
 */

'use client';

import { Badge } from '@/components/ui/data-display/badge';
import { Skeleton } from '@/components/ui/feedback/skeleton';

export interface GameSearchResultItem {
  id: string;
  title: string;
  yearPublished?: number;
  thumbnailUrl?: string;
  minPlayers?: number;
  maxPlayers?: number;
  playingTimeMinutes?: number;
  averageRating?: number | null;
  source: 'catalog' | 'bgg';
  bggId?: number | null;
}

interface GameSearchResultsProps {
  results: GameSearchResultItem[];
  loading: boolean;
  onSelect: (game: GameSearchResultItem) => void;
}

export function GameSearchResults({ results, loading, onSelect }: GameSearchResultsProps) {
  if (loading) {
    return (
      <div className="space-y-2">
        {Array.from({ length: 3 }).map((_, i) => (
          <Skeleton key={i} className="h-16 w-full rounded-lg" />
        ))}
      </div>
    );
  }

  if (results.length === 0) return null;

  return (
    <div className="space-y-1">
      {results.map(game => (
        <button
          key={game.id}
          type="button"
          onClick={() => onSelect(game)}
          className="w-full flex items-center gap-3 p-3 rounded-lg hover:bg-slate-800/60 transition-colors text-left"
        >
          {/* Thumbnail */}
          <div className="w-12 h-12 rounded-lg bg-slate-800 overflow-hidden flex-shrink-0">
            {game.thumbnailUrl ? (
              // eslint-disable-next-line @next/next/no-img-element
              <img
                src={game.thumbnailUrl}
                alt=""
                className="w-full h-full object-cover"
                loading="lazy"
              />
            ) : (
              <div className="w-full h-full flex items-center justify-center text-xl">🎲</div>
            )}
          </div>

          {/* Info */}
          <div className="flex-1 min-w-0">
            <div className="flex items-center gap-2">
              <span className="font-medium text-sm text-slate-200 truncate">{game.title}</span>
              <Badge
                variant={game.source === 'catalog' ? 'default' : 'secondary'}
                className="text-[10px] flex-shrink-0 px-1.5 py-0"
              >
                {game.source === 'catalog' ? 'Catalogo' : 'Importato'}
              </Badge>
            </div>
            <div className="text-xs text-slate-500 flex items-center gap-2 mt-0.5">
              {game.yearPublished && game.yearPublished > 0 && <span>{game.yearPublished}</span>}
              {game.minPlayers && game.maxPlayers && game.minPlayers > 0 && game.maxPlayers > 0 && (
                <>
                  {game.yearPublished && game.yearPublished > 0 && <span>·</span>}
                  <span>
                    {game.minPlayers === game.maxPlayers
                      ? `${game.minPlayers} giocatori`
                      : `${game.minPlayers}-${game.maxPlayers} giocatori`}
                  </span>
                </>
              )}
              {game.averageRating && game.averageRating > 0 && (
                <>
                  <span>·</span>
                  <span>★ {game.averageRating.toFixed(1)}</span>
                </>
              )}
            </div>
          </div>
        </button>
      ))}
    </div>
  );
}
