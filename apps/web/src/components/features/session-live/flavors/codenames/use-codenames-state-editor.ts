'use client';

import { useCallback, useMemo } from 'react';

import { useUpdateLiveGameState } from '@/hooks/mutations/useUpdateLiveGameState';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';

import { generateCodenamesBoard } from './codenames-board-preset';
import {
  CODENAMES_STATE_VERSION,
  parseCodenamesGameState,
  oppositeTeam,
  type CodenamesGameState,
} from './codenames-state';

export interface CodenamesStateEditor {
  state: CodenamesGameState | null;
  initializeState: () => void;
  regenerateBoard: () => void;
  revealCell: (index: number) => void;
  setClue: (word: string, number: number) => void;
  clearClue: () => void;
  switchTeam: () => void;
}

function freshState(): CodenamesGameState {
  const { board, startingTeam } = generateCodenamesBoard();
  return {
    v: CODENAMES_STATE_VERSION,
    game: 'codenames',
    board,
    currentTeam: startingTeam,
    clue: null,
  };
}

export function useCodenamesStateEditor(sessionId: string): CodenamesStateEditor {
  const raw = useLiveSessionStore(s => s.gameState);
  const state = useMemo(() => parseCodenamesGameState(raw), [raw]);
  const { mutate } = useUpdateLiveGameState(sessionId);

  // Discrete edits: optimistic write + IMMEDIATE PUT (no debounce — a tap must never be dropped).
  const commit = useCallback(
    (next: CodenamesGameState) => {
      useLiveSessionStore.getState().setGameState(next);
      mutate(next);
    },
    [mutate]
  );

  const readState = useCallback(
    (): CodenamesGameState | null =>
      parseCodenamesGameState(useLiveSessionStore.getState().gameState),
    []
  );

  const initializeState = useCallback(() => commit(freshState()), [commit]);
  const regenerateBoard = useCallback(() => {
    if (readState() == null) return;
    commit(freshState());
  }, [commit, readState]);

  const revealCell = useCallback(
    (index: number) => {
      const cur = readState();
      if (cur == null || index < 0 || index >= cur.board.length) return;
      if (cur.board[index].revealed) return; // idempotent
      const board = cur.board.map((c, i) => (i === index ? { ...c, revealed: true } : c));
      commit({ ...cur, board });
    },
    [commit, readState]
  );

  const setClue = useCallback(
    (word: string, number: number) => {
      const cur = readState();
      if (cur == null) return;
      commit({ ...cur, clue: { word, number: number < 0 ? 0 : Math.trunc(number) } });
    },
    [commit, readState]
  );

  const clearClue = useCallback(() => {
    const cur = readState();
    if (cur == null) return;
    commit({ ...cur, clue: null });
  }, [commit, readState]);

  const switchTeam = useCallback(() => {
    const cur = readState();
    if (cur == null) return;
    commit({ ...cur, currentTeam: oppositeTeam(cur.currentTeam), clue: null });
  }, [commit, readState]);

  return { state, initializeState, regenerateBoard, revealCell, setClue, clearClue, switchTeam };
}
