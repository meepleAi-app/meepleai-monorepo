/**
 * GameStateViewer Component
 * Issue #2406: Game State Editor UI
 *
 * Read-only display of complete game state.
 */

'use client';

import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Separator } from '@/components/ui/separator';
import type { GameState } from '@/types/game-state';

import { PlayerStateCard } from './PlayerStateCard';

interface GameStateViewerProps {
  state: GameState;
  currentPlayerIndex?: number;
}

export function GameStateViewer({ state, currentPlayerIndex }: GameStateViewerProps) {
  return (
    <div className="space-y-6" data-testid="game-state-viewer">
      {/* Game Info */}
      <Card>
        <CardHeader>
          <CardTitle>Game Information</CardTitle>
          <CardDescription>Current game session state</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
            {state.phase && (
              <div>
                <p className="text-sm text-muted-foreground">Phase</p>
                <Badge variant="outline" className="mt-1">
                  {state.phase}
                </Badge>
              </div>
            )}
            {state.roundNumber !== undefined && (
              <div>
                <p className="text-sm text-muted-foreground">Round</p>
                <p className="text-lg font-semibold">{state.roundNumber}</p>
              </div>
            )}
            {state.currentPlayerIndex !== undefined && (
              <div>
                <p className="text-sm text-muted-foreground">Current Player</p>
                <p className="text-lg font-semibold">
                  {state.players[state.currentPlayerIndex]?.playerName || 'Unknown'}
                </p>
              </div>
            )}
          </div>

          {/* Global Resources */}
          {state.globalResources && Object.keys(state.globalResources).length > 0 && (
            <>
              <Separator />
              <div>
                <p className="text-sm font-medium mb-3">Global Resources</p>
                <div className="grid grid-cols-2 md:grid-cols-3 gap-3">
                  {Object.entries(state.globalResources).map(([key, value]) => (
                    <div
                      key={key}
                      className="flex justify-between items-center p-2 rounded-lg bg-muted"
                    >
                      <span className="text-sm capitalize">{key.replace(/_/g, ' ')}</span>
                      <span className="font-semibold">{value}</span>
                    </div>
                  ))}
                </div>
              </div>
            </>
          )}

          {/* Metadata */}
          {state.metadata && Object.keys(state.metadata).length > 0 && (
            <>
              <Separator />
              <div>
                <p className="text-sm font-medium mb-3">Additional Information</p>
                <div className="space-y-2">
                  {Object.entries(state.metadata).map(([key, value]) => (
                    <div key={key} className="flex justify-between text-sm">
                      <span className="text-muted-foreground capitalize">
                        {key.replace(/([A-Z])/g, ' $1').trim()}
                      </span>
                      <span className="font-medium">{String(value)}</span>
                    </div>
                  ))}
                </div>
              </div>
            </>
          )}
        </CardContent>
      </Card>

      {/* Players */}
      <div>
        <h3 className="text-lg font-semibold mb-4">Players ({state.players.length})</h3>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {state.players.map((player, index) => (
            <PlayerStateCard
              key={`${player.playerName}-${index}`}
              player={player}
              isCurrentPlayer={currentPlayerIndex === index}
              editable={false}
            />
          ))}
        </div>
      </div>
    </div>
  );
}
