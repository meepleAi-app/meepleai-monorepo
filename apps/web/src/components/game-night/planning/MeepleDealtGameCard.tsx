'use client';

import { X } from 'lucide-react';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import type { GameNightGame } from '@/stores/game-night';

interface MeepleDealtGameCardProps {
  game: GameNightGame;
  onRemove: (gameId: string) => void;
  rotation: number;
}

export function MeepleDealtGameCard({ game, onRemove, rotation }: MeepleDealtGameCardProps) {
  return (
    <div
      data-testid="dealt-card"
      style={{ transform: `rotate(${rotation}deg)` }}
      className="w-[140px]"
    >
      <MeepleCard
        entity="game"
        variant="compact"
        title={game.title}
        imageUrl={game.thumbnailUrl}
        quickActions={[
          {
            icon: X,
            label: `Rimuovi ${game.title}`,
            onClick: () => onRemove(game.id),
          },
        ]}
      />
    </div>
  );
}
