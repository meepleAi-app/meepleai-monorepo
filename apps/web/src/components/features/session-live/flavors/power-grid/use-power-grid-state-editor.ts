'use client';

import { useCallback, useEffect, useMemo } from 'react';

import { useUpdateLiveGameState } from '@/hooks/mutations/useUpdateLiveGameState';
import { useDebouncedCallback } from '@/lib/session-live/use-debounced-callback';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';

import {
  initialPowerGridState,
  parsePowerGridGameState,
  type PowerGridGameState,
  type PowerGridPlantBank,
  type PowerGridResource,
} from './power-grid-state';

export interface PowerGridStateEditor {
  state: PowerGridGameState | null;
  initializeState: () => void;
  bumpResource: (field: PowerGridResource, delta: 1 | -1) => void;
  setPlant: (bank: PowerGridPlantBank, index: number, plant: number | null) => void;
}

const clampMin = (n: number) => (n < 0 ? 0 : n);

export function usePowerGridStateEditor(sessionId: string): PowerGridStateEditor {
  const raw = useLiveSessionStore(s => s.gameState);
  const state = useMemo(() => parsePowerGridGameState(raw), [raw]);
  const { mutate } = useUpdateLiveGameState(sessionId);
  const [debouncedMutate, flush] = useDebouncedCallback(
    (next: PowerGridGameState) => mutate(next),
    500
  );

  useEffect(() => () => flush(), [flush]);

  const commit = useCallback(
    (next: PowerGridGameState) => {
      useLiveSessionStore.getState().setGameState(next); // optimistic
      debouncedMutate(next);
    },
    [debouncedMutate]
  );

  const commitImmediate = useCallback(
    (next: PowerGridGameState) => {
      commit(next);
      flush(); // send the full fresh state now (plant edits must not be lost)
    },
    [commit, flush]
  );

  const readState = useCallback(
    (): PowerGridGameState | null =>
      parsePowerGridGameState(useLiveSessionStore.getState().gameState),
    []
  );

  const initializeState = useCallback(
    () => commitImmediate(initialPowerGridState()),
    [commitImmediate]
  );

  const bumpResource = useCallback(
    (field: PowerGridResource, delta: 1 | -1) => {
      const cur = readState();
      if (cur == null) return;
      commit({
        ...cur,
        resources: { ...cur.resources, [field]: clampMin(cur.resources[field] + delta) },
      });
    },
    [commit, readState]
  );

  const setPlant = useCallback(
    (bankName: PowerGridPlantBank, index: number, plant: number | null) => {
      const cur = readState();
      if (cur == null || index < 0 || index >= cur.plants[bankName].length) return;
      const value = plant == null ? null : clampMin(Math.trunc(plant));
      const nextBank = cur.plants[bankName].map((p, i) => (i === index ? value : p));
      commitImmediate({ ...cur, plants: { ...cur.plants, [bankName]: nextBank } });
    },
    [commitImmediate, readState]
  );

  return { state, initializeState, bumpResource, setPlant };
}
