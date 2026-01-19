/**
 * GameSelector - Dropdown for selecting game context
 *
 * Allows users to select which board game they want to chat about.
 * Integrates with ChatProvider for state management.
 */

import React from 'react';

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { useChatStore } from '@/store/chat/store';
import { Game } from '@/types';

import { SkeletonLoader } from '../loading/SkeletonLoader';

export function GameSelector() {
  // Issue #1676: Migrated from useChatContext to direct Zustand store
  const { games, selectedGameId, selectGame, loading } = useChatStore(state => ({
    games: state.games,
    selectedGameId: state.selectedGameId,
    selectGame: state.selectGame,
    loading: state.loading,
  }));

  if (loading.games) {
    return (
      <div className="mb-3">
        <label className="block mb-1.5 font-medium text-[13px]">Cambia Gioco:</label>
        <SkeletonLoader variant="gameSelection" />
      </div>
    );
  }

  const placeholder = games.length === 0 ? 'Nessun gioco disponibile' : 'Seleziona un gioco';

  return (
    <div className="mb-3">
      <label htmlFor="gameSelect" className="block mb-1.5 font-medium text-[13px]">
        Cambia Gioco:
      </label>
      <Select
        value={selectedGameId ?? ''}
        onValueChange={value => {
          if (value) {
            void selectGame(value);
          }
        }}
        disabled={loading.games}
      >
        <SelectTrigger
          id="gameSelect"
          data-testid="game-selector"
          aria-busy={loading.games}
          className="w-full"
        >
          <SelectValue placeholder={placeholder} />
        </SelectTrigger>
        <SelectContent>
          {games.map((game: Game) => (
            <SelectItem key={game.id} value={game.id} data-testid={`game-option-${game.id}`}>
              {game.title}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}
