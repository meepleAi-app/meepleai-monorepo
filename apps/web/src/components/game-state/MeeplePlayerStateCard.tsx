/**
 * MeeplePlayerStateCard Component
 * Migrated from PlayerStateCard to use MeepleCard MtG visual styling.
 *
 * Uses entity="player" styling (purple accent) with the MeepleCard CVA classes
 * for consistent visual language (entity-colored left border, warm shadows,
 * parchment texture, rounded corners). Interactive resource tracking and
 * editable score controls are preserved as-is.
 *
 * When a player hex color is provided, it overrides the default purple accent
 * via the --mc-entity-color CSS variable.
 */

'use client';

import React from 'react';

import { Avatar } from '@/components/ui/data-display/avatar';
import { Badge } from '@/components/ui/data-display/badge';
import {
  entityColors,
  meepleCardVariants,
  contentVariants,
} from '@/components/ui/data-display/meeple-card-styles';
import { hexToHsl } from '@/lib/color-utils';
import { cn } from '@/lib/utils';
import type { PlayerState } from '@/types/game-state';

import { ResourceTracker } from './ResourceTracker';

interface MeeplePlayerStateCardProps {
  player: PlayerState;
  isCurrentPlayer?: boolean;
  onScoreChange?: (newScore: number) => void;
  onResourceChange?: (resourceKey: string, newValue: number) => void;
  editable?: boolean;
}

export function MeeplePlayerStateCard({
  player,
  isCurrentPlayer = false,
  onScoreChange,
  onResourceChange,
  editable = false,
}: MeeplePlayerStateCardProps) {
  const handleScoreChange = (delta: number) => {
    const currentScore = player.score ?? 0;
    const newScore = Math.max(0, currentScore + delta);
    onScoreChange?.(newScore);
  };

  // Resolve entity color: player hex color → HSL, or default player purple
  const playerHsl = player.color ? hexToHsl(player.color) : undefined;
  const entityColor = playerHsl || entityColors.player.hsl;

  return (
    <article
      className={cn(
        meepleCardVariants({ variant: 'expanded' }),
        // Remove the default cursor-pointer since this is an interactive editor, not a link
        'cursor-default',
        // Current player highlight ring
        isCurrentPlayer && 'ring-2 ring-primary'
      )}
      style={
        {
          '--mc-entity-color': `hsl(${entityColor})`,
        } as React.CSSProperties
      }
      aria-label={`Player: ${player.playerName}`}
      data-testid="player-state-card"
      data-entity="player"
      data-variant="expanded"
    >
      {/* Mana badge: entity type indicator at top-left */}
      <div className="absolute top-2 left-2.5 z-10">
        <span
          className="px-2 py-0.5 text-[9px] font-extrabold uppercase tracking-[0.04em] text-white rounded-[6px] shadow-sm"
          style={{ backgroundColor: `hsl(${entityColor})` }}
        >
          Player
        </span>
      </div>

      {/* Current turn badge at top-right */}
      {isCurrentPlayer && (
        <div className="absolute top-2 right-2.5 z-10">
          <Badge variant="default" className="text-xs">
            Current Turn
          </Badge>
        </div>
      )}

      {/* Content area */}
      <div className={cn(contentVariants({ variant: 'expanded' }), 'pt-8')}>
        {/* Player header: avatar + name + order */}
        <div className="flex items-center gap-3">
          <Avatar
            className="h-10 w-10 flex items-center justify-center font-semibold flex-shrink-0"
            style={{ backgroundColor: player.color || `hsl(${entityColors.player.hsl})` }}
            aria-label={`Player ${player.playerName} color: ${player.color || 'default'}`}
          >
            <span className="text-white text-sm">{player.playerName.charAt(0).toUpperCase()}</span>
          </Avatar>
          <div className="min-w-0">
            <h3 className="text-base font-heading font-bold text-card-foreground truncate">
              {player.playerName}
            </h3>
            <p className="text-xs text-muted-foreground">Player {player.playerOrder}</p>
          </div>
        </div>

        {/* Score */}
        {player.score !== undefined && (
          <div className="flex items-center justify-between">
            <label className="text-sm font-medium">Score</label>
            <ResourceTracker
              value={player.score}
              onChange={delta => handleScoreChange(delta)}
              editable={editable}
              testId={`score-${player.playerName}`}
            />
          </div>
        )}

        {/* Resources */}
        {player.resources && Object.keys(player.resources).length > 0 && (
          <div className="space-y-2">
            <label className="text-sm font-medium">Resources</label>
            <div className="space-y-2">
              {Object.entries(player.resources).map(([key, value]) => (
                <div key={key} className="flex items-center justify-between">
                  <span className="text-sm capitalize">{key.replace(/_/g, ' ')}</span>
                  <ResourceTracker
                    value={value}
                    onChange={delta => {
                      const newValue = Math.max(0, value + delta);
                      onResourceChange?.(key, newValue);
                    }}
                    editable={editable}
                    testId={`resource-${player.playerName}-${key}`}
                  />
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Additional player-specific fields */}
        {Object.entries(player)
          .filter(
            ([key]) => !['playerName', 'playerOrder', 'color', 'score', 'resources'].includes(key)
          )
          .map(([key, value]) => (
            <div key={key} className="flex items-center justify-between text-sm">
              <span className="capitalize text-muted-foreground">
                {key.replace(/([A-Z])/g, ' $1').trim()}
              </span>
              <span className="font-medium">{String(value)}</span>
            </div>
          ))}
      </div>
    </article>
  );
}
