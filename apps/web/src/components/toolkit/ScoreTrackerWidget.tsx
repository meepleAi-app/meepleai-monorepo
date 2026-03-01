'use client';

import React, { useState, useCallback } from 'react';

import { Plus, Trophy, X } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';

import { WidgetCard } from './WidgetCard';

interface ScoreEntry {
  playerId: string;
  playerName: string;
  scores: number[];
}

interface ScoreTrackerWidgetProps {
  isEnabled: boolean;
  players?: Array<{ id: string; name: string }>;
  onToggle?: (enabled: boolean) => void;
  onStateChange?: (stateJson: string) => void;
  'data-testid'?: string;
}

/**
 * ScoreTrackerWidget — multi-player, multi-round score tracking.
 * Issue #5151 — Epic B8.
 */
export function ScoreTrackerWidget({
  isEnabled,
  players: initialPlayers,
  onToggle,
  onStateChange,
  'data-testid': testId,
}: ScoreTrackerWidgetProps) {
  const [entries, setEntries] = useState<ScoreEntry[]>(
    (initialPlayers ?? [{ id: '1', name: 'Player 1' }]).map(p => ({
      playerId: p.id,
      playerName: p.name,
      scores: [],
    }))
  );
  const [newPlayerName, setNewPlayerName] = useState('');
  const [pendingScores, setPendingScores] = useState<Record<string, string>>({});

  const persist = useCallback(
    (e: ScoreEntry[]) => onStateChange?.(JSON.stringify({ entries: e })),
    [onStateChange]
  );

  const addScore = useCallback(
    (playerId: string) => {
      const raw = pendingScores[playerId] ?? '';
      const val = parseInt(raw, 10);
      if (isNaN(val)) return;

      setEntries(prev => {
        const updated = prev.map(e =>
          e.playerId === playerId ? { ...e, scores: [...e.scores, val] } : e
        );
        persist(updated);
        return updated;
      });
      setPendingScores(prev => ({ ...prev, [playerId]: '' }));
    },
    [pendingScores, persist]
  );

  const addPlayer = useCallback(() => {
    const name = newPlayerName.trim();
    if (!name) return;
    setEntries(prev => {
      const updated = [...prev, { playerId: crypto.randomUUID(), playerName: name, scores: [] }];
      persist(updated);
      return updated;
    });
    setNewPlayerName('');
  }, [newPlayerName, persist]);

  const removePlayer = useCallback(
    (playerId: string) => {
      setEntries(prev => {
        const updated = prev.filter(e => e.playerId !== playerId);
        persist(updated);
        return updated;
      });
    },
    [persist]
  );

  const totals = entries.map(e => ({
    ...e,
    total: e.scores.reduce((a, b) => a + b, 0),
  }));

  const maxTotal = Math.max(0, ...totals.map(e => e.total));

  return (
    <WidgetCard
      title="Score Tracker"
      icon={<Trophy className="h-4 w-4 text-amber-500" />}
      isEnabled={isEnabled}
      onToggle={onToggle}
      data-testid={testId ?? 'score-tracker-widget'}
    >
      <div className="space-y-3">
        {/* Score table */}
        <div className="space-y-2">
          {totals.map(entry => (
            <div
              key={entry.playerId}
              className="space-y-1"
              data-testid={`score-row-${entry.playerId}`}
            >
              <div className="flex items-center justify-between text-sm">
                <div className="flex items-center gap-1">
                  {entry.total === maxTotal && entry.total > 0 && (
                    <Trophy className="h-3 w-3 text-amber-500" />
                  )}
                  <span className="font-medium">{entry.playerName}</span>
                </div>
                <div className="flex items-center gap-2">
                  <span className="tabular-nums font-bold">{entry.total}</span>
                  <Button
                    variant="ghost"
                    size="icon"
                    className="h-5 w-5 opacity-40 hover:opacity-100"
                    onClick={() => removePlayer(entry.playerId)}
                    aria-label={`Remove ${entry.playerName}`}
                  >
                    <X className="h-3 w-3" />
                  </Button>
                </div>
              </div>

              {/* Score input row */}
              <div className="flex gap-1">
                <Input
                  type="number"
                  value={pendingScores[entry.playerId] ?? ''}
                  onChange={e =>
                    setPendingScores(prev => ({ ...prev, [entry.playerId]: e.target.value }))
                  }
                  onKeyDown={e => e.key === 'Enter' && addScore(entry.playerId)}
                  placeholder="+/− score"
                  className="h-7 text-xs"
                  aria-label={`Score for ${entry.playerName}`}
                />
                <Button
                  size="sm"
                  variant="outline"
                  className="h-7 px-2"
                  onClick={() => addScore(entry.playerId)}
                  aria-label={`Add score for ${entry.playerName}`}
                >
                  <Plus className="h-3 w-3" />
                </Button>
              </div>
            </div>
          ))}
        </div>

        {/* Add player */}
        <div className="flex gap-2 border-t pt-2">
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
            aria-label="Add player to score tracker"
          >
            Add
          </Button>
        </div>
      </div>
    </WidgetCard>
  );
}
