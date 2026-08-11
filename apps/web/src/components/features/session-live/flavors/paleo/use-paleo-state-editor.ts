'use client';

import { useCallback, useEffect, useMemo } from 'react';

import { useUpdateLiveGameState } from '@/hooks/mutations/useUpdateLiveGameState';
import { useDebouncedCallback } from '@/lib/session-live/use-debounced-callback';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';

import {
  initialPaleoState,
  nextPaleoStatus,
  parsePaleoGameState,
  type PaleoGameState,
  type PaleoResource,
} from './paleo-state';

export interface PaleoStateEditor {
  state: PaleoGameState | null;
  initializeState: () => void;
  bumpResource: (field: PaleoResource, delta: 1 | -1) => void;
  cycleSurvivorStatus: (playerId: string) => void;
}

const clampMin = (n: number) => (n < 0 ? 0 : n);

export function usePaleoStateEditor(
  sessionId: string,
  playerIds: readonly string[]
): PaleoStateEditor {
  const raw = useLiveSessionStore(s => s.gameState);
  const state = useMemo(() => parsePaleoGameState(raw), [raw]);
  const { mutate } = useUpdateLiveGameState(sessionId);
  const [debouncedMutate, flush] = useDebouncedCallback(
    (next: PaleoGameState) => mutate(next),
    500
  );

  useEffect(() => () => flush(), [flush]);

  const commit = useCallback(
    (next: PaleoGameState) => {
      useLiveSessionStore.getState().setGameState(next); // optimistic
      debouncedMutate(next);
    },
    [debouncedMutate]
  );

  const readState = useCallback(
    (): PaleoGameState | null => parsePaleoGameState(useLiveSessionStore.getState().gameState),
    []
  );

  const initializeState = useCallback(
    () => commit(initialPaleoState(playerIds)),
    [commit, playerIds]
  );

  const bumpResource = useCallback(
    (field: PaleoResource, delta: 1 | -1) => {
      const cur = readState();
      if (cur == null) return;
      commit({
        ...cur,
        resources: { ...cur.resources, [field]: clampMin(cur.resources[field] + delta) },
      });
    },
    [commit, readState]
  );

  const cycleSurvivorStatus = useCallback(
    (playerId: string) => {
      const cur = readState();
      if (cur == null) return;
      const currentStatus = cur.survivors[playerId] ?? 'alive';
      commit({
        ...cur,
        survivors: { ...cur.survivors, [playerId]: nextPaleoStatus(currentStatus) },
      });
    },
    [commit, readState]
  );

  return { state, initializeState, bumpResource, cycleSurvivorStatus };
}
