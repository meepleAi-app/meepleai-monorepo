/**
 * BinaryWinEditor — radio Win / Lose per player.
 *
 * Strategy: server-side `BinaryWinScoringStrategy`. Each player is either
 * winner or loser; cooperative games allow multiple winners.
 *
 * Asse D follow-up P1 (#1899) T2.
 *
 * @module components/sessions/score-strategies/BinaryWinEditor
 */

'use client';

import { useEffect, useState } from 'react';

import type { BinaryWinScoreData, PlayerOption } from './types';

export interface BinaryWinEditorProps {
  players: readonly PlayerOption[];
  initialData?: BinaryWinScoreData;
  onChange: (data: BinaryWinScoreData) => void;
  disabled?: boolean;
}

function initialResults(
  players: readonly PlayerOption[],
  initialData?: BinaryWinScoreData
): Record<string, boolean> {
  if (initialData?.results) {
    return Object.fromEntries(initialData.results.map(r => [r.playerId, r.isWinner]));
  }
  return Object.fromEntries(players.map(p => [p.id, false]));
}

export function BinaryWinEditor({
  players,
  initialData,
  onChange,
  disabled,
}: BinaryWinEditorProps) {
  const [results, setResults] = useState<Record<string, boolean>>(() =>
    initialResults(players, initialData)
  );

  useEffect(() => {
    onChange({
      results: players.map(p => ({ playerId: p.id, isWinner: results[p.id] ?? false })),
    });
  }, [results, players, onChange]);

  const handleChange = (playerId: string, isWinner: boolean) => {
    setResults(prev => ({ ...prev, [playerId]: isWinner }));
  };

  return (
    <div className="space-y-2" data-testid="binary-win-editor">
      {players.map(player => {
        const won = results[player.id] ?? false;
        return (
          <div key={player.id} className="flex items-center gap-4">
            <span className="flex-1 font-medium">{player.displayName}</span>
            <fieldset className="flex gap-3" disabled={disabled}>
              <legend className="sr-only">{`Risultato per ${player.displayName}`}</legend>
              <label className="flex min-h-[44px] items-center gap-2 rounded-md border border-border px-3 md:min-h-0 md:gap-1 md:rounded-none md:border-0 md:px-0">
                <input
                  type="radio"
                  name={`binary-${player.id}`}
                  checked={won === true}
                  onChange={() => handleChange(player.id, true)}
                  data-testid={`binary-win-${player.id}`}
                />
                <span>Win</span>
              </label>
              <label className="flex min-h-[44px] items-center gap-2 rounded-md border border-border px-3 md:min-h-0 md:gap-1 md:rounded-none md:border-0 md:px-0">
                <input
                  type="radio"
                  name={`binary-${player.id}`}
                  checked={won === false}
                  onChange={() => handleChange(player.id, false)}
                  data-testid={`binary-lose-${player.id}`}
                />
                <span>Lose</span>
              </label>
            </fieldset>
          </div>
        );
      })}
    </div>
  );
}
