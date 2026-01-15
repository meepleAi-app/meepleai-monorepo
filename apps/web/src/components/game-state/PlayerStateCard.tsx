/**
 * PlayerStateCard Component
 * Issue #2406: Game State Editor UI
 *
 * Card component displaying per-player game state with color coding.
 */

'use client';

import { Avatar } from '@/components/ui/avatar';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import type { PlayerState } from '@/types/game-state';

import { ResourceTracker } from './ResourceTracker';

interface PlayerStateCardProps {
  player: PlayerState;
  isCurrentPlayer?: boolean;
  onScoreChange?: (newScore: number) => void;
  onResourceChange?: (resourceKey: string, newValue: number) => void;
  editable?: boolean;
}

export function PlayerStateCard({
  player,
  isCurrentPlayer = false,
  onScoreChange,
  onResourceChange,
  editable = false,
}: PlayerStateCardProps) {
  const handleScoreChange = (delta: number) => {
    const currentScore = player.score ?? 0;
    const newScore = Math.max(0, currentScore + delta);
    onScoreChange?.(newScore);
  };

  return (
    <Card className={isCurrentPlayer ? 'ring-2 ring-primary' : ''} data-testid="player-state-card">
      <CardHeader className="pb-3">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <Avatar
              className="h-10 w-10 flex items-center justify-center font-semibold"
              style={{ backgroundColor: player.color || '#666' }}
              aria-label={`Player ${player.playerName} color: ${player.color || 'default'}`}
            >
              <span className="text-white text-sm">
                {player.playerName.charAt(0).toUpperCase()}
              </span>
            </Avatar>
            <div>
              <CardTitle className="text-lg">{player.playerName}</CardTitle>
              <p className="text-sm text-muted-foreground">Player {player.playerOrder}</p>
            </div>
          </div>
          {isCurrentPlayer && <Badge variant="default">Current Turn</Badge>}
        </div>
      </CardHeader>

      <CardContent className="space-y-4">
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
      </CardContent>
    </Card>
  );
}
