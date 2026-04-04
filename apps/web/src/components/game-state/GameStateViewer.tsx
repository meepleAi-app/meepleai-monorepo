/**
 * GameStateViewer Component
 * Issue #2406: Game State Editor UI
 *
 * Read-only display of complete game state.
 */

'use client';

import { Badge } from '@/components/ui/data-display/badge';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/data-display/card';
import { Separator } from '@/components/ui/navigation/separator';
import type { GameState } from '@/types/game-state';

import { MeeplePlayerStateCard } from './MeeplePlayerStateCard';

interface GameStateViewerProps {
  state: GameState;
  currentPlayerIndex?: number;
}

export function GameStateViewer({ state, currentPlayerIndex }: GameStateViewerProps) {
  return (
    <div className="space-y-4 sm:space-y-6" data-testid="game-state-viewer">
      {/* Game Info */}
      <Card>
        <CardHeader className="pb-3 sm:pb-6">
          <CardTitle className="text-base sm:text-lg">Game Information</CardTitle>
          <CardDescription className="text-xs sm:text-sm">
            Current game session state
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-3 sm:space-y-4">
          <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-3 sm:gap-4">
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
                <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-2 sm:gap-3">
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
        <h3 className="text-base sm:text-lg font-semibold mb-3 sm:mb-4">
          Players ({state.players.length})
        </h3>
        <div className="grid grid-cols-1 gap-3 sm:gap-4">
          {state.players.map((player, index) => (
            <MeeplePlayerStateCard
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
