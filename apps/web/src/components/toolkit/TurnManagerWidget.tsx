'use client';

import React, { useState, useCallback } from 'react';

import { ChevronRight, RotateCcw, Users } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { useWidgetSync } from '@/lib/hooks/useWidgetSync';

import { WidgetCard } from './WidgetCard';

interface Player {
  id: string;
  name: string;
}

interface TurnManagerState {
  players: Player[];
  currentIndex: number;
  round: number;
}

interface TurnManagerWidgetProps {
  isEnabled: boolean;
  players?: Player[];
  sessionId?: string;
  toolkitId?: string;
  onToggle?: (enabled: boolean) => void;
  onStateChange?: (stateJson: string) => void;
  'data-testid'?: string;
}

/**
 * TurnManagerWidget — turn order tracker with round counter.
 * Issue #5150 — Epic B7.
 */
export function TurnManagerWidget({
  isEnabled,
  players: initialPlayers,
  sessionId,
  toolkitId,
  onToggle,
  onStateChange,
  'data-testid': testId,
}: TurnManagerWidgetProps) {
  const [players, setPlayers] = useState<Player[]>(
    initialPlayers ?? [
      { id: '1', name: 'Player 1' },
      { id: '2', name: 'Player 2' },
    ]
  );
  const [currentIndex, setCurrentIndex] = useState(0);
  const [round, setRound] = useState(1);
  const [newPlayerName, setNewPlayerName] = useState('');

  const { broadcastState, isConnected: _isConnected } = useWidgetSync({
    sessionId,
    toolkitId,
    widgetType: 'TurnManager',
    enabled: !!sessionId && !!toolkitId,
    onRemoteUpdate: (stateJson: string) => {
      try {
        const remote: TurnManagerState = JSON.parse(stateJson);
        setPlayers(remote.players);
        setCurrentIndex(remote.currentIndex);
        setRound(remote.round);
      } catch {
        // Ignore malformed remote state
      }
    },
  });

  const persistState = useCallback(
    (ps: Player[], idx: number, r: number) => {
      const stateJson = JSON.stringify({ players: ps, currentIndex: idx, round: r });
      onStateChange?.(stateJson);
      broadcastState(stateJson);
    },
    [onStateChange, broadcastState]
  );

  const nextTurn = useCallback(() => {
    setCurrentIndex(prev => {
      const next = (prev + 1) % players.length;
      const newRound = next === 0 ? round + 1 : round;
      if (next === 0) setRound(newRound);
      persistState(players, next, newRound);
      return next;
    });
  }, [players, round, persistState]);

  const reset = useCallback(() => {
    setCurrentIndex(0);
    setRound(1);
    persistState(players, 0, 1);
  }, [players, persistState]);

  const addPlayer = useCallback(() => {
    const name = newPlayerName.trim();
    if (!name) return;
    const updated = [...players, { id: crypto.randomUUID(), name }];
    setPlayers(updated);
    setNewPlayerName('');
    persistState(updated, currentIndex, round);
  }, [newPlayerName, players, currentIndex, round, persistState]);

  const removePlayer = useCallback(
    (id: string) => {
      const updated = players.filter(p => p.id !== id);
      if (updated.length === 0) return;
      const newIdx = Math.min(currentIndex, updated.length - 1);
      setPlayers(updated);
      setCurrentIndex(newIdx);
      persistState(updated, newIdx, round);
    },
    [players, currentIndex, round, persistState]
  );

  return (
    <WidgetCard
      title="Turn Manager"
      icon={<Users className="h-4 w-4 text-blue-500" />}
      isEnabled={isEnabled}
      onToggle={onToggle}
      data-testid={testId ?? 'turn-manager-widget'}
    >
      <div className="space-y-3">
        {/* Round indicator */}
        <div className="flex items-center justify-between">
          <Badge variant="outline" className="text-xs">
            Round {round}
          </Badge>
          <Button
            variant="ghost"
            size="icon"
            className="h-7 w-7"
            onClick={reset}
            aria-label="Reset turns"
          >
            <RotateCcw className="h-3 w-3" />
          </Button>
        </div>

        {/* Player list */}
        <div className="space-y-1">
          {players.map((player, idx) => (
            <div
              key={player.id}
              className={`flex items-center justify-between rounded-md px-3 py-2 text-sm transition-colors ${
                idx === currentIndex
                  ? 'bg-primary text-primary-foreground font-medium'
                  : 'bg-muted/50 text-muted-foreground'
              }`}
              data-testid={`player-row-${idx}`}
            >
              <div className="flex items-center gap-2">
                {idx === currentIndex && <ChevronRight className="h-3 w-3" />}
                <span>{player.name}</span>
              </div>
              <Button
                variant="ghost"
                size="icon"
                className="h-5 w-5 opacity-50 hover:opacity-100"
                onClick={() => removePlayer(player.id)}
                aria-label={`Remove ${player.name}`}
              >
                ×
              </Button>
            </div>
          ))}
        </div>

        {/* Next turn button */}
        <Button onClick={nextTurn} className="w-full" size="sm" aria-label="Next turn">
          <ChevronRight className="mr-1 h-4 w-4" />
          Next Turn
        </Button>

        {/* Add player */}
        <div className="flex gap-2">
          <Input
            value={newPlayerName}
            onChange={e => setNewPlayerName(e.target.value)}
            placeholder="Add player…"
            className="h-8 text-xs"
            onKeyDown={e => e.key === 'Enter' && addPlayer()}
            aria-label="New player name"
          />
          <Button
            variant="outline"
            size="sm"
            onClick={addPlayer}
            disabled={!newPlayerName.trim()}
            aria-label="Add player"
          >
            Add
          </Button>
        </div>
      </div>
    </WidgetCard>
  );
}
