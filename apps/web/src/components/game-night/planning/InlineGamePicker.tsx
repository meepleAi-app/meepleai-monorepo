'use client';

import { useMemo } from 'react';

import { Gamepad2 } from 'lucide-react';

import { cn } from '@/lib/utils';
import type { GameNightGame } from '@/stores/game-night';

interface InlineGamePickerProps {
  games: GameNightGame[];
  onSelect: (game: GameNightGame) => void;
  playerCount?: number;
  excludeIds?: string[];
}

export function InlineGamePicker({
  games,
  onSelect,
  playerCount,
  excludeIds = [],
}: InlineGamePickerProps) {
  const filtered = useMemo(() => {
    let result = games.filter(g => !excludeIds.includes(g.id));
    if (playerCount) {
      result = result.filter(
        g =>
          (!g.minPlayers || g.minPlayers <= playerCount) &&
          (!g.maxPlayers || g.maxPlayers >= playerCount)
      );
    }
    return result;
  }, [games, playerCount, excludeIds]);

  if (filtered.length === 0) {
    return (
      <div className="py-6 text-center text-sm text-muted-foreground">
        Nessun gioco adatto{playerCount ? ` per ${playerCount} giocatori` : ''}
      </div>
    );
  }

  return (
    <div
      className="flex gap-3 overflow-x-auto py-2 px-1 scrollbar-thin"
      data-testid="inline-game-picker"
    >
      {filtered.map(game => (
        <button
          key={game.id}
          onClick={() => onSelect(game)}
          className={cn(
            'flex flex-col items-center gap-1.5 p-3 rounded-xl',
            'border border-border bg-card hover:border-primary/30',
            'transition-all duration-200 hover:shadow-sm',
            'shrink-0 w-[120px]'
          )}
        >
          <div className="h-16 w-16 rounded-lg bg-muted flex items-center justify-center">
            {game.thumbnailUrl ? (
              <img
                src={game.thumbnailUrl}
                alt={game.title}
                className="h-full w-full rounded-lg object-cover"
              />
            ) : (
              <Gamepad2 className="h-6 w-6 text-muted-foreground" />
            )}
          </div>
          <span className="text-xs font-medium text-foreground text-center line-clamp-2">
            {game.title}
          </span>
          {(game.minPlayers || game.maxPlayers) && (
            <span className="text-[10px] text-muted-foreground">
              {game.minPlayers}–{game.maxPlayers} giocatori
            </span>
          )}
        </button>
      ))}
    </div>
  );
}
