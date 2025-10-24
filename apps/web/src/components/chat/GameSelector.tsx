/**
 * GameSelector - Dropdown for selecting game context
 *
 * Allows users to select which board game they want to chat about.
 * Integrates with ChatProvider for state management.
 */

import React from 'react';
import { useChatContext } from './ChatProvider';
import { SkeletonLoader } from '../loading/SkeletonLoader';

export function GameSelector() {
  const { games, selectedGameId, selectGame, loading } = useChatContext();

  if (loading.games) {
    return (
      <div style={{ marginBottom: 12 }}>
        <label style={{ display: 'block', marginBottom: 6, fontWeight: 500, fontSize: 13 }}>
          Cambia Gioco:
        </label>
        <SkeletonLoader variant="gameSelection" />
      </div>
    );
  }

  return (
    <div style={{ marginBottom: 12 }}>
      <label
        htmlFor="gameSelect"
        style={{ display: 'block', marginBottom: 6, fontWeight: 500, fontSize: 13 }}
      >
        Cambia Gioco:
      </label>
      <select
        id="gameSelect"
        value={selectedGameId ?? ''}
        onChange={(e) => {
          const value = e.target.value;
          if (value) {
            void selectGame(value);
          }
        }}
        disabled={loading.games}
        aria-busy={loading.games}
        style={{
          width: '100%',
          padding: 8,
          fontSize: 13,
          borderRadius: 4,
          border: '1px solid #dadce0',
          cursor: loading.games ? 'not-allowed' : 'pointer'
        }}
      >
        {games.length === 0 ? (
          <option value="">Nessun gioco disponibile</option>
        ) : (
          <>
            <option value="">Seleziona un gioco</option>
            {games.map((game) => (
              <option key={game.id} value={game.id}>
                {game.name}
              </option>
            ))}
          </>
        )}
      </select>
    </div>
  );
}
