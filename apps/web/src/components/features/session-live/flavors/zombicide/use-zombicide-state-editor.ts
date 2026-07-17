'use client';

import { useCallback, useEffect, useMemo } from 'react';

import { useUpdateLiveGameState } from '@/hooks/mutations/useUpdateLiveGameState';
import { useDebouncedCallback } from '@/lib/session-live/use-debounced-callback';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';

import {
  initialZombicideState,
  nextWoundLevel,
  parseZombicideGameState,
  type ZombicideGameState,
  type ZombieType,
} from './zombicide-state';

export interface ZombicideStateEditor {
  state: ZombicideGameState | null;
  initializeState: () => void;
  bumpZombie: (type: ZombieType, delta: 1 | -1) => void;
  cycleWound: (playerId: string) => void;
}

const clampMin = (n: number) => (n < 0 ? 0 : n);

export function useZombicideStateEditor(
  sessionId: string,
  playerIds: readonly string[]
): ZombicideStateEditor {
  const raw = useLiveSessionStore(s => s.gameState);
  const state = useMemo(() => parseZombicideGameState(raw), [raw]);
  const { mutate } = useUpdateLiveGameState(sessionId);
  const [debouncedMutate, flush] = useDebouncedCallback(
    (next: ZombicideGameState) => mutate(next),
    500
  );

  useEffect(() => () => flush(), [flush]);

  const commit = useCallback(
    (next: ZombicideGameState) => {
      useLiveSessionStore.getState().setGameState(next); // optimistic
      debouncedMutate(next);
    },
    [debouncedMutate]
  );

  const commitImmediate = useCallback(
    (next: ZombicideGameState) => {
      commit(next);
      flush(); // wound taps must not be lost
    },
    [commit, flush]
  );

  const readState = useCallback(
    (): ZombicideGameState | null =>
      parseZombicideGameState(useLiveSessionStore.getState().gameState),
    []
  );

  const initializeState = useCallback(
    () => commitImmediate(initialZombicideState(playerIds)),
    [commitImmediate, playerIds]
  );

  const bumpZombie = useCallback(
    (type: ZombieType, delta: 1 | -1) => {
      const cur = readState();
      if (cur == null) return;
      commit({ ...cur, zombies: { ...cur.zombies, [type]: clampMin(cur.zombies[type] + delta) } });
    },
    [commit, readState]
  );

  const cycleWound = useCallback(
    (playerId: string) => {
      const cur = readState();
      if (cur == null) return;
      const currentLevel = cur.survivors[playerId] ?? 0;
      commitImmediate({
        ...cur,
        survivors: { ...cur.survivors, [playerId]: nextWoundLevel(currentLevel) },
      });
    },
    [commitImmediate, readState]
  );

  return { state, initializeState, bumpZombie, cycleWound };
}
