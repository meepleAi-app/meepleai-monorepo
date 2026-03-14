'use client';

import React from 'react';

import type { SharedContextDto } from '@/lib/stores/toolboxStore';

interface SharedContextBarProps {
  sharedContext: SharedContextDto;
  className?: string;
  'data-testid'?: string;
}

/**
 * Sticky header bar showing shared game context: players, current turn, and round.
 * Epic #412 — Game Toolbox.
 */
export function SharedContextBar({
  sharedContext,
  className = '',
  'data-testid': testId,
}: SharedContextBarProps) {
  const { players, currentPlayerId, round, maxRounds } = sharedContext;

  return (
    <div
      className={`sticky top-0 z-10 flex items-center gap-4 border-b bg-background/95 px-4 py-2 backdrop-blur ${className}`}
      data-testid={testId ?? 'shared-context-bar'}
    >
      {/* Player list */}
      <div className="flex items-center gap-2">
        {players.map(player => {
          const isCurrent = player.id === currentPlayerId;
          return (
            <div
              key={player.id}
              className={`flex items-center gap-1.5 rounded-full px-2 py-1 text-xs font-medium transition-colors ${
                isCurrent
                  ? 'bg-primary/10 text-primary ring-1 ring-primary/30'
                  : 'text-muted-foreground'
              }`}
              data-testid={`player-chip-${player.id}`}
            >
              <span
                className="inline-block h-2.5 w-2.5 rounded-full"
                style={{ backgroundColor: player.color }}
                aria-hidden="true"
              />
              <span>{player.name}</span>
            </div>
          );
        })}
      </div>

      {/* Round indicator */}
      <div className="ml-auto flex items-center gap-1.5 text-xs text-muted-foreground">
        <span className="font-medium">Round {round}</span>
        {maxRounds !== null && <span>/ {maxRounds}</span>}
      </div>
    </div>
  );
}
