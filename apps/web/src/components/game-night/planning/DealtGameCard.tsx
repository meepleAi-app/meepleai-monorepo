'use client';

import { X, Gamepad2 } from 'lucide-react';

import { cn } from '@/lib/utils';
import type { GameNightGame } from '@/store/game-night';

interface DealtGameCardProps {
  game: GameNightGame;
  onRemove: (gameId: string) => void;
  rotation: number;
}

export function DealtGameCard({ game, onRemove, rotation }: DealtGameCardProps) {
  return (
    <div
      data-testid="dealt-card"
      style={{ transform: `rotate(${rotation}deg)` }}
      className={cn(
        'relative group w-[140px] rounded-xl border border-border bg-card p-3',
        'shadow-sm hover:shadow-md transition-shadow duration-200'
      )}
    >
      <button
        onClick={() => onRemove(game.id)}
        aria-label={`Rimuovi ${game.title}`}
        className={cn(
          'absolute -top-2 -right-2 h-6 w-6 rounded-full',
          'bg-destructive text-destructive-foreground',
          'flex items-center justify-center',
          'opacity-0 group-hover:opacity-100 transition-opacity'
        )}
      >
        <X className="h-3 w-3" />
      </button>
      <div className="h-16 w-full rounded-lg bg-muted flex items-center justify-center mb-2">
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
      <p className="text-xs font-medium text-center truncate">{game.title}</p>
    </div>
  );
}
