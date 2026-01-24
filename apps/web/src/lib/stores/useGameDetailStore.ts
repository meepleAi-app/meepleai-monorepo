/**
 * Game Detail Store (Issue #2832)
 *
 * Zustand store for managing game detail page state with optimistic updates
 */

import { create } from 'zustand';
import { devtools } from 'zustand/middleware';

export interface GameDetailState {
  gameId: string | null;
  currentState: 'Nuovo' | 'InPrestito' | 'Wishlist' | 'Owned' | null;
  isUpdatingState: boolean;
  isRecordingSession: boolean;
  optimisticSessionId: string | null;
  error: string | null;
}

export interface GameDetailActions {
  setGameId: (gameId: string) => void;
  setCurrentState: (state: GameDetailState['currentState']) => void;
  setIsUpdatingState: (isUpdating: boolean) => void;
  setIsRecordingSession: (isRecording: boolean) => void;
  setOptimisticSessionId: (sessionId: string | null) => void;
  setError: (error: string | null) => void;
  reset: () => void;
}

export type GameDetailStore = GameDetailState & GameDetailActions;

const initialState: GameDetailState = {
  gameId: null,
  currentState: null,
  isUpdatingState: false,
  isRecordingSession: false,
  optimisticSessionId: null,
  error: null,
};

export const useGameDetailStore = create<GameDetailStore>()(
  devtools(
    (set) => ({
      ...initialState,

      setGameId: (gameId) => set({ gameId }, false, 'setGameId'),

      setCurrentState: (currentState) => set({ currentState }, false, 'setCurrentState'),

      setIsUpdatingState: (isUpdatingState) =>
        set({ isUpdatingState }, false, 'setIsUpdatingState'),

      setIsRecordingSession: (isRecordingSession) =>
        set({ isRecordingSession }, false, 'setIsRecordingSession'),

      setOptimisticSessionId: (optimisticSessionId) =>
        set({ optimisticSessionId }, false, 'setOptimisticSessionId'),

      setError: (error) => set({ error }, false, 'setError'),

      reset: () => set(initialState, false, 'reset'),
    }),
    { name: 'GameDetailStore' }
  )
);
