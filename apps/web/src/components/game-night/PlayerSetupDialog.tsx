'use client';

import { useState, useCallback } from 'react';

import { Gamepad2, Loader2, Plus, Trash2, Users } from 'lucide-react';

import { Dialog, DialogContent, DialogTitle } from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import type { PlayerColor } from '@/lib/api/schemas/live-sessions.schemas';

/**
 * Color palette for player tokens.
 * Matches the backend PlayerColor enum + White/Black from PlayerColorSchema.
 * `label` is the Italian display name; `name` is the API enum value.
 */
const PLAYER_COLORS = [
  { name: 'Red', label: 'Rosso', hex: '#ef4444' },
  { name: 'Blue', label: 'Blu', hex: '#3b82f6' },
  { name: 'Green', label: 'Verde', hex: '#22c55e' },
  { name: 'Yellow', label: 'Giallo', hex: '#eab308' },
  { name: 'Purple', label: 'Viola', hex: '#a855f7' },
  { name: 'Orange', label: 'Arancione', hex: '#f97316' },
  { name: 'Pink', label: 'Rosa', hex: '#ec4899' },
  { name: 'Teal', label: 'Acquamarina', hex: '#14b8a6' },
  { name: 'White', label: 'Bianco', hex: '#ffffff' },
  { name: 'Black', label: 'Nero', hex: '#1f2937' },
] as const;

export interface PlayerSetup {
  displayName: string;
  color: PlayerColor;
}

/** Internal representation with a stable id for React keys. */
interface PlayerEntry extends PlayerSetup {
  id: string;
}

interface PlayerSetupDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  gameName: string;
  minPlayers: number;
  maxPlayers: number;
  onStart: (players: PlayerSetup[]) => void;
  isLoading: boolean;
}

export function PlayerSetupDialog({
  open,
  onOpenChange,
  gameName,
  minPlayers,
  maxPlayers,
  onStart,
  isLoading,
}: PlayerSetupDialogProps) {
  const [players, setPlayers] = useState<PlayerEntry[]>([
    { id: crypto.randomUUID(), displayName: '', color: PLAYER_COLORS[0].name },
  ]);

  // Issue 2 fix: guard and color computation inside functional updater
  // so `players` is not a stale closure dependency.
  const addPlayer = useCallback(() => {
    setPlayers(prev => {
      if (prev.length >= maxPlayers) return prev;
      const usedColors = new Set(prev.map(p => p.color));
      const nextColor =
        PLAYER_COLORS.find(c => !usedColors.has(c.name))?.name ?? PLAYER_COLORS[0].name;
      return [...prev, { id: crypto.randomUUID(), displayName: '', color: nextColor }];
    });
  }, [maxPlayers]);

  const removePlayer = useCallback((id: string) => {
    setPlayers(prev => prev.filter(p => p.id !== id));
  }, []);

  const updatePlayer = useCallback((id: string, field: keyof PlayerSetup, value: string) => {
    setPlayers(prev => prev.map(p => (p.id === id ? { ...p, [field]: value as PlayerColor } : p)));
  }, []);

  const validPlayers = players.filter(p => p.displayName.trim().length > 0);
  const canStart = validPlayers.length >= Math.max(1, minPlayers);

  // Issue 1 fix: strip internal `id` before passing to consumer
  const handleStart = useCallback(() => {
    onStart(validPlayers.map(({ displayName, color }) => ({ displayName, color })));
  }, [onStart, validPlayers]);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md" data-testid="player-setup-dialog">
        <DialogTitle className="flex items-center gap-2">
          <Users className="h-5 w-5 text-amber-600" />
          {gameName} — Giocatori
        </DialogTitle>

        <div className="space-y-3 pt-2">
          <p className="text-sm text-muted-foreground">
            Aggiungi i giocatori ({minPlayers}&ndash;{maxPlayers} giocatori).
          </p>

          {/* Issue 1 fix: use stable player.id as key instead of array index */}
          {players.map(player => (
            <div key={player.id} className="flex items-center gap-2">
              <div
                className="h-6 w-6 rounded-full flex-shrink-0 border"
                style={{
                  backgroundColor:
                    PLAYER_COLORS.find(c => c.name === player.color)?.hex ?? '#9ca3af',
                }}
              />
              <Input
                value={player.displayName}
                onChange={e => updatePlayer(player.id, 'displayName', e.target.value)}
                placeholder="Nome giocatore"
                className="flex-1"
              />
              {/* Issue 7 fix: show Italian label, send API enum value */}
              <select
                value={player.color}
                onChange={e => updatePlayer(player.id, 'color', e.target.value)}
                className="text-xs border rounded px-2 py-1.5 bg-background"
                aria-label={`Colore giocatore ${players.indexOf(player) + 1}`}
              >
                {PLAYER_COLORS.map(c => (
                  <option key={c.name} value={c.name}>
                    {c.label}
                  </option>
                ))}
              </select>
              {players.length > 1 && (
                <Button
                  variant="ghost"
                  size="icon"
                  className="h-8 w-8"
                  onClick={() => removePlayer(player.id)}
                >
                  <Trash2 className="h-4 w-4 text-muted-foreground" />
                </Button>
              )}
            </div>
          ))}

          {players.length < maxPlayers && (
            <Button variant="outline" size="sm" onClick={addPlayer} className="w-full">
              <Plus className="h-4 w-4 mr-1" /> Aggiungi giocatore
            </Button>
          )}

          <div className="flex justify-end gap-2 pt-2">
            <Button variant="outline" size="sm" onClick={() => onOpenChange(false)}>
              Annulla
            </Button>
            <Button
              size="sm"
              disabled={!canStart || isLoading}
              onClick={handleStart}
              className="bg-amber-600 hover:bg-amber-700 text-white"
            >
              {isLoading ? (
                <Loader2 className="h-4 w-4 mr-2 animate-spin" />
              ) : (
                <Gamepad2 className="h-4 w-4 mr-2" />
              )}
              Inizia Partita
            </Button>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}
