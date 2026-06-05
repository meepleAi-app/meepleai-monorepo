/**
 * ObjectivesEditor — checklist of completed objectives per player.
 *
 * Strategy: server-side `ObjectivesScoringStrategy`. Each player tracks which
 * objectives (from a fixed catalogue, e.g. Tikal temples / Concordia majors)
 * they completed. Duplicates inside a single player's list are impossible by
 * construction (we use a `Set` per player and stable string ids).
 *
 * Asse D follow-up P1 (#1899) T3.
 *
 * @module components/sessions/score-strategies/ObjectivesEditor
 */

'use client';

import { useEffect, useState } from 'react';

import type { ObjectivesScoreData, PlayerOption } from './types';

export interface ObjectivesEditorProps {
  players: readonly PlayerOption[];
  /**
   * Catalogue of selectable objective names. Acts as the column-header of the
   * checklist. Caller is responsible for stable ordering.
   */
  availableObjectives: readonly string[];
  initialData?: ObjectivesScoreData;
  onChange: (data: ObjectivesScoreData) => void;
  disabled?: boolean;
}

function initialState(
  players: readonly PlayerOption[],
  initialData?: ObjectivesScoreData
): Record<string, Set<string>> {
  const map: Record<string, Set<string>> = {};
  for (const player of players) {
    map[player.id] = new Set();
  }
  if (initialData?.completedByPlayer) {
    for (const entry of initialData.completedByPlayer) {
      map[entry.playerId] = new Set(entry.objectives);
    }
  }
  return map;
}

export function ObjectivesEditor({
  players,
  availableObjectives,
  initialData,
  onChange,
  disabled,
}: ObjectivesEditorProps) {
  const [completed, setCompleted] = useState<Record<string, Set<string>>>(() =>
    initialState(players, initialData)
  );

  useEffect(() => {
    onChange({
      completedByPlayer: players.map(p => ({
        playerId: p.id,
        objectives: Array.from(completed[p.id] ?? new Set<string>()),
      })),
    });
  }, [completed, players, onChange]);

  const toggle = (playerId: string, objective: string) => {
    setCompleted(prev => {
      const next = { ...prev };
      const current = new Set(next[playerId] ?? []);
      if (current.has(objective)) {
        current.delete(objective);
      } else {
        current.add(objective);
      }
      next[playerId] = current;
      return next;
    });
  };

  return (
    <div className="space-y-4" data-testid="objectives-editor">
      {players.map(player => {
        const playerSet = completed[player.id] ?? new Set<string>();
        return (
          <fieldset
            key={player.id}
            disabled={disabled}
            className="space-y-2 rounded-md border border-border p-3"
            data-testid={`objectives-fieldset-${player.id}`}
          >
            <legend className="px-1 font-medium">{player.displayName}</legend>
            <div className="flex flex-wrap gap-2">
              {availableObjectives.map(objective => {
                const checked = playerSet.has(objective);
                const testId = `obj-${player.id}-${objective}`;
                return (
                  <label
                    key={objective}
                    className="inline-flex items-center gap-1 rounded-md border border-border-strong px-2 py-1 text-sm"
                  >
                    <input
                      type="checkbox"
                      checked={checked}
                      onChange={() => toggle(player.id, objective)}
                      data-testid={testId}
                    />
                    <span>{objective}</span>
                  </label>
                );
              })}
            </div>
          </fieldset>
        );
      })}
    </div>
  );
}
