/**
 * Player State Editor - Issue #2420
 *
 * Editor for managing player count, names, and scores.
 *
 * Features:
 * - Add/remove players
 * - Edit player name and score
 * - Optional color assignment
 * - Validation for name (required) and score (>= 0)
 * - Supports readonly mode
 */

'use client';

import { useState } from 'react';

import { Plus, Trash2, User } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';

import type { PlayerState } from './StateEditor';

// ========== Component Props ==========

export interface PlayerStateEditorProps {
  /** Current players list */
  players: PlayerState[];
  /** Callback on players change */
  onChange: (players: PlayerState[]) => void;
  /** Read-only mode */
  readonly?: boolean;
  /** Validation errors map (path -> error message) */
  validationErrors?: Record<string, string>;
}

// ========== Component ==========

export function PlayerStateEditor({
  players,
  onChange,
  readonly = false,
  validationErrors = {},
}: PlayerStateEditorProps) {
  const [_editingId, _setEditingId] = useState<string | null>(null);

  /**
   * Add new player
   */
  const handleAddPlayer = () => {
    const newPlayer: PlayerState = {
      id: crypto.randomUUID(),
      name: `Giocatore ${players.length + 1}`,
      score: 0,
    };
    onChange([...players, newPlayer]);
  };

  /**
   * Remove player
   */
  const handleRemovePlayer = (id: string) => {
    onChange(players.filter(p => p.id !== id));
  };

  /**
   * Update player field
   */
  const handleUpdatePlayer = (id: string, field: keyof PlayerState, value: string | number) => {
    onChange(
      players.map(p =>
        p.id === id
          ? {
              ...p,
              [field]: value,
            }
          : p
      )
    );
  };

  /**
   * Get validation error for specific player field
   */
  const getFieldError = (index: number, field: string): string | undefined => {
    return validationErrors[`players.${index}.${field}`];
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h3 className="text-lg font-semibold">Giocatori</h3>
          <p className="text-sm text-muted-foreground">Gestisci giocatori e punteggi</p>
        </div>
        {!readonly && (
          <Button onClick={handleAddPlayer} size="sm" variant="outline">
            <Plus className="mr-2 h-4 w-4" />
            Aggiungi Giocatore
          </Button>
        )}
      </div>

      {players.length === 0 ? (
        <Card className="border-dashed">
          <CardContent className="flex flex-col items-center justify-center py-8">
            <User className="mb-2 h-12 w-12 text-muted-foreground" />
            <p className="text-sm text-muted-foreground">Nessun giocatore aggiunto</p>
            {!readonly && (
              <Button onClick={handleAddPlayer} size="sm" variant="ghost" className="mt-2">
                Aggiungi il primo giocatore
              </Button>
            )}
          </CardContent>
        </Card>
      ) : (
        <div className="space-y-3">
          {players.map((player, index) => (
            <Card key={player.id} className="p-4">
              <div className="flex items-start gap-4">
                <div className="flex-1 space-y-3">
                  <div>
                    <Label htmlFor={`player-name-${player.id}`}>Nome</Label>
                    <Input
                      id={`player-name-${player.id}`}
                      value={player.name}
                      onChange={e => handleUpdatePlayer(player.id, 'name', e.target.value)}
                      disabled={readonly}
                      className={getFieldError(index, 'name') ? 'border-red-500' : ''}
                    />
                    {getFieldError(index, 'name') && (
                      <p className="mt-1 text-sm text-red-500">{getFieldError(index, 'name')}</p>
                    )}
                  </div>

                  <div className="grid grid-cols-2 gap-3">
                    <div>
                      <Label htmlFor={`player-score-${player.id}`}>Punteggio</Label>
                      <Input
                        id={`player-score-${player.id}`}
                        type="number"
                        value={player.score}
                        onChange={e =>
                          handleUpdatePlayer(player.id, 'score', parseInt(e.target.value) || 0)
                        }
                        disabled={readonly}
                        min={0}
                        className={getFieldError(index, 'score') ? 'border-red-500' : ''}
                      />
                      {getFieldError(index, 'score') && (
                        <p className="mt-1 text-sm text-red-500">{getFieldError(index, 'score')}</p>
                      )}
                    </div>

                    <div>
                      <Label htmlFor={`player-color-${player.id}`}>Colore (opzionale)</Label>
                      <Input
                        id={`player-color-${player.id}`}
                        type="color"
                        value={player.color || '#3b82f6'}
                        onChange={e => handleUpdatePlayer(player.id, 'color', e.target.value)}
                        disabled={readonly}
                        className="h-10"
                      />
                    </div>
                  </div>
                </div>

                {!readonly && (
                  <Button
                    onClick={() => handleRemovePlayer(player.id)}
                    variant="ghost"
                    size="icon"
                    className="text-red-500 hover:bg-red-50 hover:text-red-600"
                  >
                    <Trash2 className="h-4 w-4" />
                  </Button>
                )}
              </div>
            </Card>
          ))}
        </div>
      )}

      <div className="rounded-md border border-blue-200 bg-blue-50 p-3 text-sm text-blue-800">
        <strong>Giocatori:</strong> {players.length}
        {players.length > 0 && (
          <>
            {' '}
            • <strong>Punteggio totale:</strong> {players.reduce((sum, p) => sum + p.score, 0)}
          </>
        )}
      </div>
    </div>
  );
}
