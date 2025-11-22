/**
 * GameSelector - Dropdown for selecting game context
 *
 * Allows users to select which board game they want to chat about.
 * Integrates with ChatProvider for state management.
 */

import React from 'react';
import { useChatContext } from './ChatProvider';
import { SkeletonLoader } from '../loading/SkeletonLoader';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';

export function GameSelector() {
  const { games, selectedGameId, selectGame, loading } = useChatContext();

  if (loading.games) {
    return (
      <div className="mb-3">
        <label className="block mb-1.5 font-medium text-[13px]">
          Cambia Gioco:
        </label>
        <SkeletonLoader variant="gameSelection" />
      </div>
    );
  }

  const placeholder = games.length === 0
    ? 'Nessun gioco disponibile'
    : 'Seleziona un gioco';

  return (
    <div className="mb-3">
      <label
        htmlFor="gameSelect"
        className="block mb-1.5 font-medium text-[13px]"
      >
        Cambia Gioco:
      </label>
      <Select
        value={selectedGameId ?? ''}
        onValueChange={(value) => {
          if (value) {
            void selectGame(value);
          }
        }}
        disabled={loading.games}
      >
        <SelectTrigger
          id="gameSelect"
          aria-busy={loading.games}
          className="w-full"
        >
          <SelectValue placeholder={placeholder} />
        </SelectTrigger>
        <SelectContent>
          {games.map((game) => (
            <SelectItem key={game.id} value={game.id}>
              {game.name}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}
