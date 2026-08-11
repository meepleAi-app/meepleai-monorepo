'use client';

import { useCallback, useEffect, useMemo } from 'react';

import { useUpdateLiveGameState } from '@/hooks/mutations/useUpdateLiveGameState';
import { useDebouncedCallback } from '@/lib/session-live/use-debounced-callback';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';

import { generateStandardBoard } from './catan-board-preset';
import {
  CATAN_PIECE_TOTALS,
  CATAN_STATE_VERSION,
  emptyCatanPlayerState,
  parseCatanGameState,
  type CatanGameState,
  type CatanPiece,
  type CatanPlayerState,
} from './catan-state';

export interface CatanStateEditor {
  state: CatanGameState | null;
  initializeState: () => void;
  regenerateBoard: () => void;
  setDiceRoll: (sum: number) => void;
  moveRobber: (hexId: string) => void;
  bumpBuilt: (playerId: string, piece: CatanPiece, delta: 1 | -1) => void;
  setDevCount: (playerId: string, delta: 1 | -1) => void;
  setHandSize: (playerId: string, delta: 1 | -1) => void;
  toggleBadge: (playerId: string, badge: 'longestRoad' | 'largestArmy') => void;
}

const clampMin = (n: number, min: number) => (n < min ? min : n);
const clampRange = (n: number, min: number, max: number) => (n < min ? min : n > max ? max : n);

export function useCatanStateEditor(
  sessionId: string,
  playerIds: readonly string[]
): CatanStateEditor {
  const raw = useLiveSessionStore(s => s.gameState);
  const state = useMemo(() => parseCatanGameState(raw), [raw]);
  const { mutate } = useUpdateLiveGameState(sessionId);
  const [debouncedMutate, flush] = useDebouncedCallback(
    (next: CatanGameState) => mutate(next),
    500
  );

  // Flush any pending PUT on unmount so a fast edit is not lost.
  useEffect(() => () => flush(), [flush]);

  const commit = useCallback(
    (next: CatanGameState) => {
      useLiveSessionStore.getState().setGameState(next); // optimistic
      debouncedMutate(next);
    },
    [debouncedMutate]
  );

  // Read the freshest parsed state at call time (avoids stale closures across rapid edits).
  const readState = useCallback(
    (): CatanGameState | null => parseCatanGameState(useLiveSessionStore.getState().gameState),
    []
  );

  const editPlayer = useCallback(
    (playerId: string, fn: (p: CatanPlayerState) => CatanPlayerState) => {
      const cur = readState();
      if (cur == null) return;
      const prev = cur.players[playerId] ?? emptyCatanPlayerState();
      commit({ ...cur, players: { ...cur.players, [playerId]: fn(prev) } });
    },
    [commit, readState]
  );

  const initializeState = useCallback(() => {
    const board = generateStandardBoard();
    const players: Record<string, CatanPlayerState> = {};
    for (const id of playerIds) players[id] = emptyCatanPlayerState();
    commit({
      v: CATAN_STATE_VERSION,
      game: 'catan',
      board,
      dice: { last: null, history: [] },
      players,
    });
  }, [commit, playerIds]);

  const regenerateBoard = useCallback(() => {
    const cur = readState();
    if (cur == null) return;
    commit({ ...cur, board: generateStandardBoard() });
  }, [commit, readState]);

  const setDiceRoll = useCallback(
    (sum: number) => {
      const cur = readState();
      if (cur == null) return;
      commit({ ...cur, dice: { last: sum, history: [sum, ...cur.dice.history].slice(0, 20) } });
    },
    [commit, readState]
  );

  const moveRobber = useCallback(
    (hexId: string) => {
      const cur = readState();
      if (cur == null) return;
      commit({ ...cur, board: { ...cur.board, robberHexId: hexId } });
    },
    [commit, readState]
  );

  const bumpBuilt = useCallback(
    (playerId: string, piece: CatanPiece, delta: 1 | -1) =>
      editPlayer(playerId, p => ({
        ...p,
        built: {
          ...p.built,
          [piece]: clampRange(p.built[piece] + delta, 0, CATAN_PIECE_TOTALS[piece]),
        },
      })),
    [editPlayer]
  );

  const setDevCount = useCallback(
    (playerId: string, delta: 1 | -1) =>
      editPlayer(playerId, p => ({ ...p, devCount: clampMin(p.devCount + delta, 0) })),
    [editPlayer]
  );

  const setHandSize = useCallback(
    (playerId: string, delta: 1 | -1) =>
      editPlayer(playerId, p => ({ ...p, handSize: clampMin(p.handSize + delta, 0) })),
    [editPlayer]
  );

  const toggleBadge = useCallback(
    (playerId: string, badge: 'longestRoad' | 'largestArmy') => {
      const cur = readState();
      if (cur == null) return;
      const nextHolds = !(cur.players[playerId]?.badges[badge] ?? false);
      const players: Record<string, CatanPlayerState> = {};
      for (const [id, p] of Object.entries(cur.players)) {
        players[id] = {
          ...p,
          badges: { ...p.badges, [badge]: id === playerId ? nextHolds : false },
        };
      }
      if (cur.players[playerId] == null) {
        players[playerId] = {
          ...emptyCatanPlayerState(),
          badges: { longestRoad: false, largestArmy: false, [badge]: nextHolds },
        };
      }
      commit({ ...cur, players });
    },
    [commit, readState]
  );

  return {
    state,
    initializeState,
    regenerateBoard,
    setDiceRoll,
    moveRobber,
    bumpBuilt,
    setDevCount,
    setHandSize,
    toggleBadge,
  };
}
