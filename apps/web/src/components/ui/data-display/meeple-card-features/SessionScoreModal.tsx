/**
 * SessionScoreModal - Score Editing Modal
 * Issue #4751 - MeepleCard Session Front
 *
 * Modal panel for editing session scores with:
 * - Player x Round matrix (editable input cells)
 * - Multi-dimension support (tabs per dimension)
 * - Save/Cancel buttons
 * - Validation: numeric values only
 */

'use client';

import React, { useCallback, useEffect, useState } from 'react';

import { Save, X } from 'lucide-react';

import { cn } from '@/lib/utils';

import {
  PLAYER_COLOR_BG,
  type SessionPlayerInfo,
  type SessionRoundScore,
  type SessionScoringConfig,
} from './session-types';

// ============================================================================
// Types
// ============================================================================

export interface SessionScoreModalProps {
  open: boolean;
  onClose: () => void;
  players: SessionPlayerInfo[];
  roundScores: SessionRoundScore[];
  scoringConfig: SessionScoringConfig;
  onSave: (updatedScores: SessionRoundScore[]) => void;
  className?: string;
}

// ============================================================================
// Component
// ============================================================================

export function SessionScoreModal({
  open,
  onClose,
  players,
  roundScores,
  scoringConfig,
  onSave,
  className,
}: SessionScoreModalProps) {
  const [activeDimension, setActiveDimension] = useState(
    scoringConfig.enabledDimensions[0] ?? 'default'
  );
  const [editedScores, setEditedScores] = useState<Map<string, number>>(new Map());

  // Reset active dimension when scoring config changes (e.g. different session)
  useEffect(() => {
    setActiveDimension(scoringConfig.enabledDimensions[0] ?? 'default');
  }, [scoringConfig]);

  // Build key for score lookup (JSON to avoid separator conflicts in dimension names)
  const scoreKey = (playerId: string, round: number, dim: string) =>
    JSON.stringify([playerId, round, dim]);

  // Initialize edited scores from props
  useEffect(() => {
    const map = new Map<string, number>();
    for (const s of roundScores) {
      map.set(scoreKey(s.playerId, s.round, s.dimension), s.value);
    }
    setEditedScores(map);
  }, [roundScores]);

  const allRounds = [
    ...new Set(roundScores.filter(s => s.dimension === activeDimension).map(s => s.round)),
  ].sort((a, b) => a - b);

  // Add an empty round for new entries
  const maxRound = allRounds.length > 0 ? Math.max(...allRounds) : 0;
  const displayRounds = [...allRounds, maxRound + 1];

  const handleChange = useCallback(
    (playerId: string, round: number, value: string) => {
      const numValue = parseInt(value, 10);
      if (value === '' || (/^-?\d+$/.test(value) && !isNaN(numValue))) {
        setEditedScores(prev => {
          const next = new Map(prev);
          if (value === '') {
            next.delete(scoreKey(playerId, round, activeDimension));
          } else {
            next.set(scoreKey(playerId, round, activeDimension), numValue);
          }
          return next;
        });
      }
    },
    [activeDimension]
  );

  const handleSave = useCallback(() => {
    const updated: SessionRoundScore[] = [];
    editedScores.forEach((value, key) => {
      const [playerId, round, dimension] = JSON.parse(key) as [string, number, string];
      updated.push({ playerId, round, dimension, value });
    });
    onSave(updated);
    onClose();
  }, [editedScores, onSave, onClose]);

  if (!open) return null;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm"
      onClick={onClose}
      data-testid="score-modal-overlay"
    >
      <div
        className={cn(
          'bg-card rounded-2xl shadow-2xl border border-border',
          'w-[90vw] max-w-[500px] max-h-[80vh]',
          'flex flex-col overflow-hidden',
          className
        )}
        onClick={e => e.stopPropagation()}
        role="dialog"
        aria-modal="true"
        aria-labelledby="score-modal-title"
        onKeyDown={e => {
          if (e.key === 'Escape') onClose();
        }}
        data-testid="score-modal"
      >
        {/* Header */}
        <div className="flex items-center justify-between px-5 py-4 border-b border-border">
          <h2
            id="score-modal-title"
            className="font-quicksand text-lg font-bold text-card-foreground"
          >
            Modifica Punteggio
          </h2>
          <button
            onClick={onClose}
            className="p-1 rounded-md hover:bg-muted transition-colors"
            aria-label="Chiudi"
          >
            <X className="w-5 h-5 text-muted-foreground" />
          </button>
        </div>

        {/* Dimension tabs */}
        {scoringConfig.enabledDimensions.length > 1 && (
          <div className="flex gap-1 px-5 pt-3">
            {scoringConfig.enabledDimensions.map(dim => (
              <button
                key={dim}
                onClick={() => setActiveDimension(dim)}
                className={cn(
                  'px-3 py-1 rounded-md text-xs font-medium transition-colors',
                  dim === activeDimension
                    ? 'bg-indigo-100 text-indigo-700 dark:bg-indigo-900/30 dark:text-indigo-300'
                    : 'text-muted-foreground hover:bg-muted'
                )}
              >
                {dim}
                {scoringConfig.dimensionUnits[dim] ? ` (${scoringConfig.dimensionUnits[dim]})` : ''}
              </button>
            ))}
          </div>
        )}

        {/* Score matrix */}
        <div className="flex-1 overflow-auto px-5 py-3">
          <table className="w-full text-sm">
            <thead>
              <tr className="text-muted-foreground text-xs">
                <th className="text-left pr-3 pb-2 font-medium">Player</th>
                {displayRounds.map(r => (
                  <th key={r} className="text-center px-1 pb-2 font-medium min-w-[48px]">
                    R{r}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {players.map(player => (
                <tr key={player.id} className="border-t border-border/30">
                  <td className="pr-3 py-2">
                    <span className="flex items-center gap-2">
                      <span
                        className={cn(
                          'w-3 h-3 rounded-full flex-shrink-0',

                          PLAYER_COLOR_BG[player.color]
                        )}
                      />
                      <span className="font-medium truncate max-w-[100px]">
                        {player.displayName}
                      </span>
                    </span>
                  </td>
                  {displayRounds.map(r => {
                    const key = scoreKey(player.id, r, activeDimension);
                    const value = editedScores.get(key);
                    return (
                      <td key={r} className="px-1 py-1">
                        <input
                          type="text"
                          inputMode="numeric"
                          pattern="[0-9]*"
                          value={value ?? ''}
                          onChange={e => handleChange(player.id, r, e.target.value)}
                          className={cn(
                            'w-full text-center px-1 py-1 rounded-md',
                            'bg-muted/50 border border-border/30',
                            'text-sm font-medium tabular-nums',
                            'focus:outline-none focus:ring-2 focus:ring-indigo-300',
                            'transition-colors'
                          )}
                          data-testid={`score-input-${player.id}-r${r}`}
                          aria-label={`Score for ${player.displayName} round ${r}`}
                        />
                      </td>
                    );
                  })}
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {/* Footer */}
        <div className="flex items-center justify-end gap-2 px-5 py-3 border-t border-border">
          <button
            onClick={onClose}
            className="px-4 py-2 rounded-lg text-sm font-medium text-muted-foreground hover:bg-muted transition-colors"
          >
            Annulla
          </button>
          <button
            onClick={handleSave}
            className="flex items-center gap-1.5 px-4 py-2 rounded-lg text-sm font-semibold bg-indigo-500 text-white hover:bg-indigo-600 shadow-sm transition-colors"
            data-testid="score-modal-save"
          >
            <Save className="w-4 h-4" />
            Salva
          </button>
        </div>
      </div>
    </div>
  );
}
