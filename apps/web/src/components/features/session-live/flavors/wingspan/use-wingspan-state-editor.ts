'use client';

import { useCallback, useEffect, useMemo } from 'react';

import { useUpdateLiveGameState } from '@/hooks/mutations/useUpdateLiveGameState';
import { useDebouncedCallback } from '@/lib/session-live/use-debounced-callback';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';

import {
  initialWingspanState,
  parseWingspanGameState,
  type WingspanGameState,
  type WingspanRoundGoal,
} from './wingspan-state';

export interface WingspanStateEditor {
  state: WingspanGameState | null;
  initializeState: () => void;
  setRound: (round: number) => void;
  advanceRound: () => void;
  setRoundGoal: (index: number, label: string) => void;
}

const clampRound = (n: number) => (n < 1 ? 1 : n > 4 ? 4 : n);

export function useWingspanStateEditor(sessionId: string): WingspanStateEditor {
  const raw = useLiveSessionStore(s => s.gameState);
  const state = useMemo(() => parseWingspanGameState(raw), [raw]);
  const { mutate } = useUpdateLiveGameState(sessionId);
  const [debouncedMutate, flush] = useDebouncedCallback(
    (next: WingspanGameState) => mutate(next),
    500
  );

  useEffect(() => () => flush(), [flush]);

  const commit = useCallback(
    (next: WingspanGameState) => {
      useLiveSessionStore.getState().setGameState(next); // optimistic
      debouncedMutate(next);
    },
    [debouncedMutate]
  );

  const readState = useCallback(
    (): WingspanGameState | null =>
      parseWingspanGameState(useLiveSessionStore.getState().gameState),
    []
  );

  const initializeState = useCallback(() => commit(initialWingspanState()), [commit]);

  const setRound = useCallback(
    (round: number) => {
      const cur = readState();
      if (cur == null) return;
      commit({ ...cur, round: clampRound(round) });
    },
    [commit, readState]
  );

  const advanceRound = useCallback(() => {
    const cur = readState();
    if (cur == null) return;
    commit({ ...cur, round: clampRound(cur.round + 1) });
  }, [commit, readState]);

  const setRoundGoal = useCallback(
    (index: number, label: string) => {
      const cur = readState();
      if (cur == null || index < 0 || index > 3) return;
      const goals: WingspanRoundGoal[] = [...cur.roundGoals];
      while (goals.length <= index) goals.push({ label: '' });
      goals[index] = { label };
      commit({ ...cur, roundGoals: goals });
    },
    [commit, readState]
  );

  return { state, initializeState, setRound, advanceRound, setRoundGoal };
}
