/**
 * PlayerSetup — Step 1 of Setup Wizard
 *
 * Issue #5583: Setup Wizard — guided game preparation
 *
 * Features:
 * - Add/remove players (name + color)
 * - Assign roles: Host or Player
 * - Configure turn order via drag reorder (index-based)
 * - Color uniqueness enforcement
 */

'use client';

import { useState, useCallback } from 'react';

import { ArrowDown, ArrowUp, Crown, Plus, Trash2, User } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { cn } from '@/lib/utils';

// ========== Types ==========

export type PlayerRole = 'Host' | 'Player';

export interface SetupPlayer {
  id: string;
  name: string;
  color: string;
  role: PlayerRole;
}

export const PLAYER_COLORS: { value: string; label: string; className: string }[] = [
  { value: '#ef4444', label: 'Rosso', className: 'bg-red-500' },
  { value: '#3b82f6', label: 'Blu', className: 'bg-blue-500' },
  { value: '#22c55e', label: 'Verde', className: 'bg-green-500' },
  { value: '#eab308', label: 'Giallo', className: 'bg-yellow-400' },
  { value: '#a855f7', label: 'Viola', className: 'bg-purple-500' },
  { value: '#f97316', label: 'Arancione', className: 'bg-orange-500' },
  { value: '#ffffff', label: 'Bianco', className: 'bg-white border border-gray-300' },
  { value: '#1f2937', label: 'Nero', className: 'bg-gray-800' },
  { value: '#ec4899', label: 'Rosa', className: 'bg-pink-400' },
  { value: '#14b8a6', label: 'Turchese', className: 'bg-teal-500' },
];

interface PlayerSetupProps {
  players: SetupPlayer[];
  onPlayersChange: (players: SetupPlayer[]) => void;
}

export function PlayerSetup({ players, onPlayersChange }: PlayerSetupProps) {
  const [newName, setNewName] = useState('');

  const usedColors = players.map(p => p.color);
  const nextAvailableColor =
    PLAYER_COLORS.find(c => !usedColors.includes(c.value))?.value ?? PLAYER_COLORS[0].value;

  const addPlayer = useCallback(() => {
    if (!newName.trim()) return;
    const isFirst = players.length === 0;
    onPlayersChange([
      ...players,
      {
        id: `player-${Date.now()}`,
        name: newName.trim(),
        color: nextAvailableColor,
        role: isFirst ? 'Host' : 'Player',
      },
    ]);
    setNewName('');
  }, [newName, players, onPlayersChange, nextAvailableColor]);

  const removePlayer = useCallback(
    (id: string) => {
      const updated = players.filter(p => p.id !== id);
      // If the removed player was Host, promote the first remaining
      if (updated.length > 0 && !updated.some(p => p.role === 'Host')) {
        updated[0] = { ...updated[0], role: 'Host' };
      }
      onPlayersChange(updated);
    },
    [players, onPlayersChange]
  );

  const changeColor = useCallback(
    (id: string, color: string) => {
      onPlayersChange(players.map(p => (p.id === id ? { ...p, color } : p)));
    },
    [players, onPlayersChange]
  );

  const toggleRole = useCallback(
    (id: string) => {
      onPlayersChange(
        players.map(p => {
          if (p.id === id) {
            return { ...p, role: p.role === 'Host' ? 'Player' : 'Host' };
          }
          // If promoting to Host, demote others
          return p;
        })
      );
    },
    [players, onPlayersChange]
  );

  const movePlayer = useCallback(
    (index: number, direction: 'up' | 'down') => {
      const newIndex = direction === 'up' ? index - 1 : index + 1;
      if (newIndex < 0 || newIndex >= players.length) return;
      const updated = [...players];
      [updated[index], updated[newIndex]] = [updated[newIndex], updated[index]];
      onPlayersChange(updated);
    },
    [players, onPlayersChange]
  );

  return (
    <div className="space-y-4">
      <div>
        <h2 className="text-lg font-semibold font-quicksand">Giocatori</h2>
        <p className="text-sm text-muted-foreground">
          Aggiungi i giocatori, assegna ruoli e ordine di turno
        </p>
      </div>

      {/* Player list */}
      <div className="space-y-2" role="list" aria-label="Lista giocatori">
        {players.map((player, index) => (
          <div
            key={player.id}
            role="listitem"
            className="flex items-center gap-2 rounded-lg border border-border bg-card p-3"
          >
            {/* Turn order controls */}
            <div className="flex flex-col gap-0.5">
              <button
                type="button"
                onClick={() => movePlayer(index, 'up')}
                disabled={index === 0}
                className="p-0.5 rounded hover:bg-muted disabled:opacity-30"
                aria-label={`Sposta ${player.name} su`}
              >
                <ArrowUp className="h-3 w-3" />
              </button>
              <button
                type="button"
                onClick={() => movePlayer(index, 'down')}
                disabled={index === players.length - 1}
                className="p-0.5 rounded hover:bg-muted disabled:opacity-30"
                aria-label={`Sposta ${player.name} giu`}
              >
                <ArrowDown className="h-3 w-3" />
              </button>
            </div>

            {/* Turn number */}
            <span className="text-xs text-muted-foreground w-4 text-center font-mono">
              {index + 1}
            </span>

            {/* Color dot */}
            <div
              className={cn(
                'h-8 w-8 rounded-full shrink-0',
                PLAYER_COLORS.find(c => c.value === player.color)?.className ?? 'bg-gray-400'
              )}
            />

            {/* Name */}
            <span className="font-medium text-sm flex-1 truncate">{player.name}</span>

            {/* Role badge */}
            <button
              type="button"
              onClick={() => toggleRole(player.id)}
              aria-label={`Ruolo di ${player.name}: ${player.role}`}
            >
              <Badge
                variant={player.role === 'Host' ? 'default' : 'outline'}
                className={cn(
                  'cursor-pointer text-xs',
                  player.role === 'Host' && 'bg-amber-500 hover:bg-amber-600'
                )}
              >
                {player.role === 'Host' ? (
                  <Crown className="h-3 w-3 mr-1" />
                ) : (
                  <User className="h-3 w-3 mr-1" />
                )}
                {player.role === 'Host' ? 'Host' : 'Player'}
              </Badge>
            </button>

            {/* Color selector (compact) */}
            <div className="flex gap-1">
              {PLAYER_COLORS.filter(c => c.value === player.color || !usedColors.includes(c.value))
                .slice(0, 4)
                .map(c => (
                  <button
                    key={c.value}
                    type="button"
                    onClick={() => changeColor(player.id, c.value)}
                    className={cn(
                      'h-5 w-5 rounded-full transition-all',
                      c.className,
                      c.value === player.color && 'ring-2 ring-offset-1 ring-indigo-500'
                    )}
                    title={c.label}
                    aria-label={`Colore ${c.label}`}
                  />
                ))}
            </div>

            {/* Remove */}
            <Button
              variant="ghost"
              size="sm"
              onClick={() => removePlayer(player.id)}
              aria-label={`Rimuovi ${player.name}`}
            >
              <Trash2 className="h-4 w-4 text-muted-foreground" />
            </Button>
          </div>
        ))}
      </div>

      {players.length === 0 && (
        <p className="text-sm text-muted-foreground text-center py-4">
          Nessun giocatore aggiunto. Aggiungi almeno un giocatore per continuare.
        </p>
      )}

      {/* Add player */}
      <div className="flex gap-2">
        <Input
          placeholder="Nome giocatore"
          value={newName}
          onChange={e => setNewName(e.target.value)}
          className="flex-1"
          onKeyDown={e => e.key === 'Enter' && addPlayer()}
          aria-label="Nome nuovo giocatore"
        />
        <Button variant="outline" onClick={addPlayer} disabled={!newName.trim()}>
          <Plus className="h-4 w-4 mr-1" />
          Aggiungi
        </Button>
      </div>

      {players.length > 0 && (
        <p className="text-xs text-muted-foreground">
          L&apos;ordine dei giocatori determina l&apos;ordine di turno. Usa le frecce per
          riordinare.
        </p>
      )}
    </div>
  );
}
