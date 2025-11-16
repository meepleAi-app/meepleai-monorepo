/**
 * GameSelector - Dropdown for selecting game context
 *
 * Migrated to Zustand (Issue #1083):
 * - Direct store access with granular subscriptions
 * - Only re-renders when games or selectedGameId changes
 */

import React from 'react';
import { useChatStoreWithSelectors } from '@/store/chat';
import { SkeletonLoader } from '../loading/SkeletonLoader';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';

export function GameSelector() {
  const games = useChatStoreWithSelectors.use.games();
  const selectedGameId = useChatStoreWithSelectors.use.selectedGameId();
  const selectGame = useChatStoreWithSelectors.use.selectGame();
  const loading = useChatStoreWithSelectors.use.loading();

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
            selectGame(value);
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
