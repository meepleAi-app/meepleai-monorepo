'use client';

import { useCallback, useEffect, useMemo } from 'react';

import { useUpdateLiveGameState } from '@/hooks/mutations/useUpdateLiveGameState';
import { useDebouncedCallback } from '@/lib/session-live/use-debounced-callback';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';

import {
  emptyPuertoRicoPlayerState,
  initialPuertoRicoState,
  parsePuertoRicoGameState,
  type PuertoRicoGameState,
  type PuertoRicoGood,
  type PuertoRicoPlayerState,
} from './puerto-rico-state';

type PlayerCounter = 'doubloons' | 'colonists' | 'plantations' | 'quarries' | 'buildings';

export interface PuertoRicoStateEditor {
  state: PuertoRicoGameState | null;
  initializeState: () => void;
  bumpPlayerCounter: (playerId: string, field: PlayerCounter, delta: 1 | -1) => void;
  bumpPlayerGood: (playerId: string, good: PuertoRicoGood, delta: 1 | -1) => void;
  setGalleonGood: (index: number, good: PuertoRicoGood | null) => void;
  bumpGalleonLoaded: (index: number, delta: 1 | -1) => void;
  setTradingSlot: (index: number, good: PuertoRicoGood | null) => void;
  bumpColonistShip: (field: 'onShip' | 'supply', delta: 1 | -1) => void;
}

const clampMin = (n: number) => (n < 0 ? 0 : n);
const clampRange = (n: number, max: number) => (n < 0 ? 0 : n > max ? max : n);

export function usePuertoRicoStateEditor(
  sessionId: string,
  playerIds: readonly string[]
): PuertoRicoStateEditor {
  const raw = useLiveSessionStore(s => s.gameState);
  const state = useMemo(() => parsePuertoRicoGameState(raw), [raw]);
  const { mutate } = useUpdateLiveGameState(sessionId);
  const [debouncedMutate, flush] = useDebouncedCallback(
    (next: PuertoRicoGameState) => mutate(next),
    500
  );

  useEffect(() => () => flush(), [flush]);

  const commit = useCallback(
    (next: PuertoRicoGameState) => {
      useLiveSessionStore.getState().setGameState(next); // optimistic
      debouncedMutate(next);
    },
    [debouncedMutate]
  );

  const readState = useCallback(
    (): PuertoRicoGameState | null =>
      parsePuertoRicoGameState(useLiveSessionStore.getState().gameState),
    []
  );

  const editPlayer = useCallback(
    (playerId: string, fn: (p: PuertoRicoPlayerState) => PuertoRicoPlayerState) => {
      const cur = readState();
      if (cur == null) return;
      const prev = cur.players[playerId] ?? emptyPuertoRicoPlayerState();
      commit({ ...cur, players: { ...cur.players, [playerId]: fn(prev) } });
    },
    [commit, readState]
  );

  const initializeState = useCallback(
    () => commit(initialPuertoRicoState(playerIds)),
    [commit, playerIds]
  );

  const bumpPlayerCounter = useCallback(
    (playerId: string, field: PlayerCounter, delta: 1 | -1) =>
      editPlayer(playerId, p => ({ ...p, [field]: clampMin(p[field] + delta) })),
    [editPlayer]
  );

  const bumpPlayerGood = useCallback(
    (playerId: string, good: PuertoRicoGood, delta: 1 | -1) =>
      editPlayer(playerId, p => ({
        ...p,
        storehouse: { ...p.storehouse, [good]: clampMin(p.storehouse[good] + delta) },
      })),
    [editPlayer]
  );

  const setGalleonGood = useCallback(
    (index: number, good: PuertoRicoGood | null) => {
      const cur = readState();
      if (cur == null || index < 0 || index >= cur.galleons.length) return;
      const galleons = cur.galleons.map((g, i) => (i === index ? { ...g, good, loaded: 0 } : g));
      commit({ ...cur, galleons });
    },
    [commit, readState]
  );

  const bumpGalleonLoaded = useCallback(
    (index: number, delta: 1 | -1) => {
      const cur = readState();
      if (cur == null || index < 0 || index >= cur.galleons.length) return;
      const galleons = cur.galleons.map((g, i) =>
        i === index ? { ...g, loaded: clampRange(g.loaded + delta, g.cap) } : g
      );
      commit({ ...cur, galleons });
    },
    [commit, readState]
  );

  const setTradingSlot = useCallback(
    (index: number, good: PuertoRicoGood | null) => {
      const cur = readState();
      if (cur == null || index < 0 || index >= cur.tradingHouse.slots.length) return;
      const slots = cur.tradingHouse.slots.map((s, i) => (i === index ? good : s));
      commit({ ...cur, tradingHouse: { slots } });
    },
    [commit, readState]
  );

  const bumpColonistShip = useCallback(
    (field: 'onShip' | 'supply', delta: 1 | -1) => {
      const cur = readState();
      if (cur == null) return;
      commit({
        ...cur,
        colonistShip: { ...cur.colonistShip, [field]: clampMin(cur.colonistShip[field] + delta) },
      });
    },
    [commit, readState]
  );

  return {
    state,
    initializeState,
    bumpPlayerCounter,
    bumpPlayerGood,
    setGalleonGood,
    bumpGalleonLoaded,
    setTradingSlot,
    bumpColonistShip,
  };
}
