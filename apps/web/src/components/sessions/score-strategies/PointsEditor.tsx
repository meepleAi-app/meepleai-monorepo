/**
 * PointsEditor — numeric points input per player.
 *
 * Strategy: server-side `PointsScoringStrategy`. Higher score wins.
 * Validates non-negative integers; empty / non-numeric input is ignored
 * (the previous value is preserved).
 *
 * Asse D follow-up P1 (#1899) T2.
 *
 * @module components/sessions/score-strategies/PointsEditor
 */

'use client';

import { useEffect, useState } from 'react';

import type { PlayerOption, PointsScoreData } from './types';

export interface PointsEditorProps {
  players: readonly PlayerOption[];
  initialData?: PointsScoreData;
  onChange: (data: PointsScoreData) => void;
  disabled?: boolean;
}

function initialScores(
  players: readonly PlayerOption[],
  initialData?: PointsScoreData
): Record<string, number> {
  if (initialData?.scores) {
    return Object.fromEntries(initialData.scores.map(s => [s.playerId, s.points]));
  }
  return Object.fromEntries(players.map(p => [p.id, 0]));
}

export function PointsEditor({ players, initialData, onChange, disabled }: PointsEditorProps) {
  const [scores, setScores] = useState<Record<string, number>>(() =>
    initialScores(players, initialData)
  );

  // Emit current state on every change. We always emit a full snapshot per
  // player so the backend can persist a coherent `PointsScoreData` without
  // diff resolution.
  useEffect(() => {
    onChange({
      scores: players.map(p => ({ playerId: p.id, points: scores[p.id] ?? 0 })),
    });
  }, [scores, players, onChange]);

  const handleChange = (playerId: string, value: string) => {
    if (value === '') return; // ignore clears — keep previous
    const num = Number.parseInt(value, 10);
    if (Number.isNaN(num) || num < 0) return; // reject negative / non-int
    setScores(prev => ({ ...prev, [playerId]: num }));
  };

  return (
    <div className="space-y-2" data-testid="points-editor">
      {players.map(player => (
        <div key={player.id} className="flex items-center gap-3">
          <label htmlFor={`points-${player.id}`} className="flex-1 font-medium">
            {player.displayName}
          </label>
          <input
            id={`points-${player.id}`}
            type="number"
            min={0}
            value={scores[player.id] ?? 0}
            onChange={e => handleChange(player.id, e.target.value)}
            disabled={disabled}
            data-testid={`points-input-${player.id}`}
            className="w-24 rounded-md border border-border bg-background px-3 py-1 text-right tabular-nums"
          />
        </div>
      ))}
    </div>
  );
}
