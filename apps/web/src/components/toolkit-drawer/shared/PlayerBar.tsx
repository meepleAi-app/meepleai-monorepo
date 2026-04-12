'use client';

/**
 * PlayerBar — Always-visible bottom bar showing player avatars and turn state.
 *
 * Tapping an avatar sets the current turn. A [+] button opens the
 * PlayerSetupModal inline. The "Sessione" button is a placeholder for
 * future session-upgrade flow.
 */

import React, { useState } from 'react';

import { cn } from '@/lib/utils';

import { useToolkitDrawer } from '../ToolkitDrawerProvider';
import { PlayerAvatar } from './PlayerAvatar';
import { PlayerSetupModal } from './PlayerSetupModal';

export function PlayerBar() {
  const { store, logEvent } = useToolkitDrawer();
  const players = store(s => s.players);
  const currentTurnIndex = store(s => s.currentTurnIndex);

  const [showAddForm, setShowAddForm] = useState(false);

  const handleSetTurn = (index: number) => {
    const player = players[index];
    if (!player) return;

    store.getState().setTurn(index);
    logEvent(
      'turn_change',
      { playerName: player.name, turnIndex: index },
      {
        playerId: player.id,
        playerName: player.name,
      }
    );
  };

  return (
    <div
      className="shrink-0 border-t border-gray-200 bg-white/90 px-3 py-2"
      data-testid="player-bar"
    >
      {/* Add player form (inline, above the bar) */}
      {showAddForm && (
        <div className="mb-2">
          <PlayerSetupModal onClose={() => setShowAddForm(false)} />
        </div>
      )}

      <div className="flex items-center gap-2">
        {/* Scrollable avatar row */}
        <div className="flex flex-1 items-center gap-2 overflow-x-auto">
          {players.map((player, index) => {
            const isActive = index === currentTurnIndex;
            return (
              <div key={player.id} className="relative flex flex-col items-center">
                <PlayerAvatar
                  name={player.name}
                  color={player.color}
                  active={isActive}
                  size="sm"
                  onClick={() => handleSetTurn(index)}
                />
                {/* Active turn dot */}
                {isActive && (
                  <span
                    className="absolute -bottom-1 h-1.5 w-1.5 rounded-full bg-[hsl(142,70%,45%)]"
                    data-testid="turn-indicator"
                  />
                )}
              </div>
            );
          })}

          {/* Add player button */}
          <button
            type="button"
            onClick={() => setShowAddForm(prev => !prev)}
            className={cn(
              'flex h-7 w-7 shrink-0 items-center justify-center rounded-full border-2 border-dashed border-gray-300',
              'text-gray-400 transition-colors hover:border-gray-400 hover:text-gray-500'
            )}
            aria-label="Aggiungi giocatore"
            data-testid="add-player-btn"
          >
            +
          </button>
        </div>

        {/* Session upgrade button (placeholder) */}
        {players.length >= 2 && (
          <button
            type="button"
            onClick={() => {
              // Placeholder for session upgrade flow
            }}
            className="shrink-0 rounded-lg border border-gray-300 px-2 py-1 text-[10px] font-medium text-gray-600 transition-colors hover:bg-gray-100"
            data-testid="session-upgrade-btn"
          >
            Sessione
          </button>
        )}
      </div>
    </div>
  );
}
