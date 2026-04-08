'use client';

/**
 * PlayerSetupModal — Inline form for adding a new player.
 *
 * Auto-assigns a color from PLAYER_COLORS based on current player count,
 * with optional color-swatch override.
 */

import React, { useState } from 'react';

import { cn } from '@/lib/utils';

import { useToolkitDrawer } from '../ToolkitDrawerProvider';
import { PLAYER_COLORS } from '../types';

import type { LocalPlayer } from '../types';

export interface PlayerSetupModalProps {
  onClose: () => void;
}

export function PlayerSetupModal({ onClose }: PlayerSetupModalProps) {
  const { store, logEvent } = useToolkitDrawer();
  const players = store(s => s.players);

  const defaultColorIndex = players.length % PLAYER_COLORS.length;
  const [name, setName] = useState('');
  const [selectedColor, setSelectedColor] = useState<string>(PLAYER_COLORS[defaultColorIndex]);

  const canConfirm = name.trim().length > 0;

  const handleConfirm = () => {
    if (!canConfirm) return;

    const newPlayer: LocalPlayer = {
      id: crypto.randomUUID(),
      name: name.trim(),
      color: selectedColor,
    };

    store.getState().addPlayer(newPlayer);

    logEvent(
      'player_joined',
      { playerName: newPlayer.name, color: newPlayer.color },
      {
        playerId: newPlayer.id,
        playerName: newPlayer.name,
      }
    );

    onClose();
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && canConfirm) handleConfirm();
    if (e.key === 'Escape') onClose();
  };

  return (
    <div
      className="flex flex-col gap-3 rounded-xl border border-gray-200 bg-white p-3 shadow-sm"
      data-testid="player-setup-form"
    >
      {/* Name input */}
      <input
        type="text"
        placeholder="Nome giocatore"
        value={name}
        onChange={e => setName(e.target.value)}
        onKeyDown={handleKeyDown}
        autoFocus
        className="rounded-lg border border-gray-300 px-3 py-1.5 text-sm outline-none focus:border-[hsl(142,70%,45%)] focus:ring-1 focus:ring-[hsl(142,70%,45%)]"
        data-testid="player-name-input"
      />

      {/* Color swatches */}
      <div className="flex flex-wrap gap-1.5">
        {PLAYER_COLORS.map(color => (
          <button
            key={color}
            type="button"
            onClick={() => setSelectedColor(color)}
            className={cn(
              'h-6 w-6 rounded-full transition-transform',
              selectedColor === color && 'scale-110 ring-2 ring-gray-800 ring-offset-1'
            )}
            style={{ backgroundColor: color }}
            aria-label={`Colore ${color}`}
            data-testid={`color-swatch-${color}`}
          />
        ))}
      </div>

      {/* Actions */}
      <div className="flex justify-end gap-2">
        <button
          type="button"
          onClick={onClose}
          className="rounded-lg px-3 py-1 text-xs text-gray-500 hover:bg-gray-100"
        >
          Annulla
        </button>
        <button
          type="button"
          onClick={handleConfirm}
          disabled={!canConfirm}
          className={cn(
            'rounded-lg px-3 py-1 text-xs font-medium text-white transition-colors',
            canConfirm
              ? 'bg-[hsl(142,70%,45%)] hover:bg-[hsl(142,70%,40%)]'
              : 'cursor-not-allowed bg-gray-300'
          )}
          data-testid="player-confirm-btn"
        >
          Aggiungi
        </button>
      </div>
    </div>
  );
}
