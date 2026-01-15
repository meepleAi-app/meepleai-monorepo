/**
 * Game State Store
 * Issue #2406: Game State Editor UI
 *
 * Zustand store for game state management with undo/redo and real-time sync.
 */

import { create } from 'zustand';
import { devtools } from 'zustand/middleware';

import type {
  GameState,
  GameStateSnapshot,
  GameStateTemplate,
  StateConflict,
  UndoRedoEntry,
} from '@/types/game-state';

interface GameStateStore {
  // Current state
  currentState: GameState | null;
  template: GameStateTemplate | null;
  isLoading: boolean;
  error: string | null;

  // Undo/Redo stacks
  undoStack: UndoRedoEntry[];
  redoStack: UndoRedoEntry[];
  maxHistorySize: number;

  // History
  snapshots: GameStateSnapshot[];

  // Conflicts
  conflicts: StateConflict[];

  // Real-time status
  isConnected: boolean;
  connectionError: string | null;

  // Actions
  setState: (state: GameState, description?: string) => void;
  setTemplate: (template: GameStateTemplate) => void;
  updateField: (path: string[], value: unknown, description?: string) => void;
  undo: () => void;
  redo: () => void;
  canUndo: () => boolean;
  canRedo: () => boolean;
  loadTemplate: (sessionId: string) => Promise<void>;
  loadState: (sessionId: string) => Promise<void>;
  saveState: (sessionId: string) => Promise<void>;
  addSnapshot: (snapshot: GameStateSnapshot) => void;
  loadSnapshot: (snapshotId: string) => void;
  detectConflict: (conflict: StateConflict) => void;
  resolveConflict: (conflictIndex: number, useLocal: boolean) => void;
  setConnectionStatus: (connected: boolean, error?: string) => void;
  reset: () => void;
}

const initialState = {
  currentState: null,
  template: null,
  isLoading: false,
  error: null,
  undoStack: [],
  redoStack: [],
  maxHistorySize: 50,
  snapshots: [],
  conflicts: [],
  isConnected: false,
  connectionError: null,
};

export const useGameStateStore = create<GameStateStore>()(
  devtools(
    (set, get) => ({
      ...initialState,

      setState: (state, description = 'State updated') => {
        const current = get().currentState;
        if (current) {
          // Push current state to undo stack
          const undoStack = [...get().undoStack];
          undoStack.push({
            state: structuredClone(current),
            description,
            timestamp: new Date().toISOString(),
          });

          // Limit stack size
          if (undoStack.length > get().maxHistorySize) {
            undoStack.shift();
          }

          set({
            currentState: state,
            undoStack,
            redoStack: [], // Clear redo stack on new change
          });
        } else {
          set({ currentState: state });
        }
      },

      setTemplate: template => set({ template }),

      /* eslint-disable security/detect-object-injection -- Safe: path array is user-controlled state update */
      updateField: (path, value, description = 'Field updated') => {
        const state = get().currentState;
        if (!state) return;

        // Deep clone and update
        const newState = structuredClone(state);
        let target: Record<string, unknown> = newState;

        // Navigate to the target object
        for (let i = 0; i < path.length - 1; i++) {
          const key = path[i];
          if (!(key in target)) {
            target[key] = {};
          }
          target = target[key] as Record<string, unknown>;
        }

        // Update the final value
        const finalKey = path[path.length - 1];
        target[finalKey] = value;

        get().setState(newState as GameState, description);
      },
      /* eslint-enable security/detect-object-injection */

      undo: () => {
        const { undoStack, currentState } = get();
        if (undoStack.length === 0 || !currentState) return;

        const entry = undoStack[undoStack.length - 1];
        const newUndoStack = undoStack.slice(0, -1);
        const redoStack = [...get().redoStack];

        // Push current state to redo stack
        redoStack.push({
          state: structuredClone(currentState),
          description: entry.description,
          timestamp: new Date().toISOString(),
        });

        set({
          currentState: entry.state,
          undoStack: newUndoStack,
          redoStack,
        });
      },

      redo: () => {
        const { redoStack, currentState } = get();
        if (redoStack.length === 0 || !currentState) return;

        const entry = redoStack[redoStack.length - 1];
        const newRedoStack = redoStack.slice(0, -1);
        const undoStack = [...get().undoStack];

        // Push current state to undo stack
        undoStack.push({
          state: structuredClone(currentState),
          description: entry.description,
          timestamp: new Date().toISOString(),
        });

        set({
          currentState: entry.state,
          undoStack,
          redoStack: newRedoStack,
        });
      },

      canUndo: () => get().undoStack.length > 0,
      canRedo: () => get().redoStack.length > 0,

      loadTemplate: async _sessionId => {
        set({ isLoading: true, error: null });
        try {
          // TODO: Implement API call to fetch template
          // const template = await api.gameState.getTemplate(_sessionId);
          // set({ template, isLoading: false });

          // Mock for now
          throw new Error('API not implemented');
        } catch (err) {
          set({
            error: err instanceof Error ? err.message : 'Failed to load template',
            isLoading: false,
          });
        }
      },

      loadState: async _sessionId => {
        set({ isLoading: true, error: null });
        try {
          // TODO: Implement API call to fetch state
          // const state = await api.gameState.getState(_sessionId);
          // set({ currentState: state, isLoading: false });

          // Mock for now
          throw new Error('API not implemented');
        } catch (err) {
          set({
            error: err instanceof Error ? err.message : 'Failed to load state',
            isLoading: false,
          });
        }
      },

      saveState: async _sessionId => {
        const state = get().currentState;
        if (!state) return;

        set({ isLoading: true, error: null });
        try {
          // TODO: Implement API call to save state
          // await api.gameState.saveState(_sessionId, state);
          // set({ isLoading: false });

          // Mock for now
          throw new Error('API not implemented');
        } catch (err) {
          set({
            error: err instanceof Error ? err.message : 'Failed to save state',
            isLoading: false,
          });
        }
      },

      addSnapshot: snapshot => {
        set({ snapshots: [...get().snapshots, snapshot] });
      },

      loadSnapshot: snapshotId => {
        const snapshot = get().snapshots.find(s => s.id === snapshotId);
        if (snapshot) {
          get().setState(snapshot.state, `Loaded snapshot: ${snapshot.action}`);
        }
      },

      detectConflict: conflict => {
        set({ conflicts: [...get().conflicts, conflict] });
      },

      resolveConflict: (conflictIndex, useLocal) => {
        const conflicts = [...get().conflicts];
        /* eslint-disable-next-line security/detect-object-injection */
        const conflict = conflicts[conflictIndex];
        if (!conflict) return;

        // Update state with chosen value
        const value = useLocal ? conflict.localValue : conflict.remoteValue;
        get().updateField(conflict.path, value, 'Resolved conflict');

        // Remove conflict
        conflicts.splice(conflictIndex, 1);
        set({ conflicts });
      },

      setConnectionStatus: (connected, error) => {
        set({ isConnected: connected, connectionError: error });
      },

      reset: () => set(initialState),
    }),
    { name: 'GameStateStore' }
  )
);
