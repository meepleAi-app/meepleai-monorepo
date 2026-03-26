/**
 * LedgerTimelineItem Component
 * Issue #2422: Ledger Mode History Timeline
 *
 * Displays a single timeline item with state diff visualization.
 */

'use client';

import { useState } from 'react';

import { Clock, ChevronDown, ChevronUp } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import type { GameState, GameStateSnapshot } from '@/types/game-state';

interface LedgerTimelineItemProps {
  snapshot: GameStateSnapshot;
  previousSnapshot?: GameStateSnapshot;
  isLatest: boolean;
  onRestore: (snapshot: GameStateSnapshot) => void;
}

/**
 * Computes diff between two states
 */
function computeStateDiff(current: GameState, previous: GameState | undefined) {
  if (!previous) return [];

  const changes: Array<{ field: string; oldValue: unknown; newValue: unknown }> = [];

  // Check phase changes
  if (current.phase !== previous.phase) {
    changes.push({ field: 'phase', oldValue: previous.phase, newValue: current.phase });
  }

  // Check round changes
  if (current.roundNumber !== previous.roundNumber) {
    changes.push({
      field: 'roundNumber',
      oldValue: previous.roundNumber,
      newValue: current.roundNumber,
    });
  }

  // Check player changes
  current.players.forEach((player, idx) => {
    const prevPlayer = previous.players[idx];
    if (!prevPlayer) return;

    if (player.score !== prevPlayer.score) {
      changes.push({
        field: `${player.playerName} score`,
        oldValue: prevPlayer.score,
        newValue: player.score,
      });
    }

    // Check resources
    if (player.resources && prevPlayer.resources) {
      Object.keys(player.resources).forEach(resource => {
        if (player.resources![resource] !== prevPlayer.resources![resource]) {
          changes.push({
            field: `${player.playerName} ${resource}`,
            oldValue: prevPlayer.resources![resource],
            newValue: player.resources![resource],
          });
        }
      });
    }
  });

  return changes;
}

export function LedgerTimelineItem({
  snapshot,
  previousSnapshot,
  isLatest,
  onRestore,
}: LedgerTimelineItemProps) {
  const [isExpanded, setIsExpanded] = useState(false);

  const formatTimestamp = (timestamp: string) => {
    const date = new Date(timestamp);
    return new Intl.DateTimeFormat('en-US', {
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
    }).format(date);
  };

  const diff = computeStateDiff(snapshot.state, previousSnapshot?.state);

  return (
    <Card className="mb-3" data-testid="ledger-timeline-item">
      <CardContent className="p-4">
        {/* Header */}
        <div className="flex items-center justify-between mb-2">
          <div className="flex items-center gap-2">
            <Clock className="h-4 w-4 text-muted-foreground" />
            <span className="text-sm font-medium">{formatTimestamp(snapshot.timestamp)}</span>
            {isLatest && (
              <Badge variant="default" className="text-xs">
                Latest
              </Badge>
            )}
          </div>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => onRestore(snapshot)}
            disabled={isLatest}
            data-testid="restore-button"
          >
            Restore
          </Button>
        </div>

        {/* Action description */}
        <p className="text-sm text-muted-foreground mb-2">{snapshot.action}</p>

        {/* State preview */}
        <div className="flex gap-3 text-xs text-muted-foreground mb-2">
          {snapshot.state.roundNumber !== undefined && (
            <span>Round: {snapshot.state.roundNumber}</span>
          )}
          {snapshot.state.phase && <span>Phase: {snapshot.state.phase}</span>}
          <span>Players: {snapshot.state.players.length}</span>
        </div>

        {/* Diff toggle */}
        {diff.length > 0 && (
          <div className="mt-2 border-t pt-2">
            <Button
              variant="ghost"
              size="sm"
              onClick={() => setIsExpanded(!isExpanded)}
              className="w-full justify-between"
              data-testid="diff-toggle"
            >
              <span className="text-xs">
                {diff.length} change{diff.length > 1 ? 's' : ''}
              </span>
              {isExpanded ? <ChevronUp className="h-4 w-4" /> : <ChevronDown className="h-4 w-4" />}
            </Button>

            {/* Diff details */}
            {isExpanded && (
              <div className="mt-2 space-y-1" data-testid="diff-details">
                {diff.map((change, idx) => (
                  <div key={idx} className="text-xs p-2 bg-muted rounded flex justify-between">
                    <span className="font-medium">{change.field}:</span>
                    <span>
                      <span className="text-red-600">{String(change.oldValue)}</span>
                      {' → '}
                      <span className="text-green-600">{String(change.newValue)}</span>
                    </span>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}
      </CardContent>
    </Card>
  );
}
