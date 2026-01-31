/**
 * Game State Store Tests - Issue #3026
 *
 * Test coverage:
 * - Initial state
 * - setState with undo/redo tracking
 * - updateField with path navigation
 * - Undo/Redo operations
 * - Template loading
 * - State loading/saving
 * - Snapshot management
 * - Conflict detection and resolution
 * - Connection status
 * - Reset functionality
 *
 * Target: >90% coverage
 */

import { renderHook, act, waitFor } from '@testing-library/react';
import { describe, it, expect, beforeEach, vi } from 'vitest';

import { useGameStateStore } from '../game-state-store';

import { api } from '@/lib/api';
import type { GameState, GameStateSnapshot, StateConflict } from '@/types/game-state';

// Mock API
vi.mock('@/lib/api', () => ({
  api: {
    sessions: {
      getState: vi.fn(),
      updateState: vi.fn(),
    },
  },
}));

const mockGameState: GameState = {
  gameId: 'game-123',
  sessionId: 'session-123',
  data: {
    round: 1,
    players: [
      { id: 'player-1', name: 'Alice', score: 10 },
      { id: 'player-2', name: 'Bob', score: 15 },
    ],
    activePlayer: 'player-1',
  },
};

const mockSnapshot: GameStateSnapshot = {
  id: 'snapshot-1',
  state: mockGameState,
  timestamp: '2026-01-31T10:00:00Z',
  action: 'Initial state',
};

const mockConflict: StateConflict = {
  path: ['data', 'round'],
  localValue: 2,
  remoteValue: 3,
  timestamp: '2026-01-31T10:00:00Z',
};

beforeEach(() => {
  vi.clearAllMocks();

  // Reset Zustand store state to initial
  useGameStateStore.setState({
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
  });
});

describe('useGameStateStore', () => {
  describe('Initial State', () => {
    it('initializes with null state', () => {
      const { result } = renderHook(() => useGameStateStore());

      expect(result.current.currentState).toBeNull();
      expect(result.current.template).toBeNull();
      expect(result.current.isLoading).toBe(false);
      expect(result.current.error).toBeNull();
      expect(result.current.undoStack).toEqual([]);
      expect(result.current.redoStack).toEqual([]);
      expect(result.current.maxHistorySize).toBe(50);
      expect(result.current.snapshots).toEqual([]);
      expect(result.current.conflicts).toEqual([]);
      expect(result.current.isConnected).toBe(false);
      expect(result.current.connectionError).toBeNull();
    });
  });

  describe('setState', () => {
    it('sets state correctly on first call', () => {
      const { result } = renderHook(() => useGameStateStore());

      act(() => {
        result.current.setState(mockGameState, 'Initial state');
      });

      expect(result.current.currentState).toEqual(mockGameState);
      expect(result.current.undoStack).toEqual([]);
      expect(result.current.redoStack).toEqual([]);
    });

    it('pushes previous state to undo stack on update', () => {
      const { result } = renderHook(() => useGameStateStore());

      act(() => {
        result.current.setState(mockGameState, 'Initial state');
      });

      const updatedState = { ...mockGameState, data: { ...mockGameState.data, round: 2 } };

      act(() => {
        result.current.setState(updatedState, 'Round 2');
      });

      expect(result.current.currentState).toEqual(updatedState);
      expect(result.current.undoStack).toHaveLength(1);
      expect(result.current.undoStack[0].state).toEqual(mockGameState);
      expect(result.current.undoStack[0].description).toBe('Round 2');
    });

    it('clears redo stack on new change', () => {
      const { result } = renderHook(() => useGameStateStore());

      act(() => {
        result.current.setState(mockGameState, 'Initial');
        result.current.setState({ ...mockGameState, data: { ...mockGameState.data, round: 2 } }, 'Round 2');
        result.current.undo();
      });

      expect(result.current.redoStack).toHaveLength(1);

      act(() => {
        result.current.setState({ ...mockGameState, data: { ...mockGameState.data, round: 3 } }, 'Round 3');
      });

      expect(result.current.redoStack).toEqual([]);
    });

    it('limits undo stack to maxHistorySize', () => {
      const { result } = renderHook(() => useGameStateStore());

      // Set maxHistorySize to 3 for testing
      useGameStateStore.setState({ maxHistorySize: 3 });

      act(() => {
        result.current.setState(mockGameState, 'Initial');
        for (let i = 1; i <= 5; i++) {
          result.current.setState(
            { ...mockGameState, data: { ...mockGameState.data, round: i + 1 } },
            `Round ${i + 1}`
          );
        }
      });

      expect(result.current.undoStack.length).toBeLessThanOrEqual(3);
    });
  });

  describe('updateField', () => {
    it('updates nested field correctly', () => {
      const { result } = renderHook(() => useGameStateStore());

      act(() => {
        result.current.setState(mockGameState, 'Initial');
        result.current.updateField(['data', 'round'], 5, 'Round updated');
      });

      expect(result.current.currentState?.data.round).toBe(5);
      expect(result.current.undoStack).toHaveLength(1);
    });

    it('creates intermediate objects if missing', () => {
      const { result } = renderHook(() => useGameStateStore());

      act(() => {
        result.current.setState(mockGameState, 'Initial');
        result.current.updateField(['data', 'newField', 'nested'], 'value', 'New field');
      });

      expect(result.current.currentState?.data).toHaveProperty('newField');
      expect((result.current.currentState?.data as { newField?: { nested?: string } }).newField?.nested).toBe('value');
    });

    it('does nothing if no current state', () => {
      const { result } = renderHook(() => useGameStateStore());

      act(() => {
        result.current.updateField(['data', 'round'], 5);
      });

      expect(result.current.currentState).toBeNull();
      expect(result.current.undoStack).toEqual([]);
    });

    it('updates array element by index', () => {
      const { result } = renderHook(() => useGameStateStore());

      act(() => {
        result.current.setState(mockGameState, 'Initial');
        result.current.updateField(['data', 'players', '0', 'score'], 20, 'Score updated');
      });

      expect(result.current.currentState?.data.players[0].score).toBe(20);
    });
  });

  describe('Undo/Redo', () => {
    it('undoes last change', () => {
      const { result } = renderHook(() => useGameStateStore());

      act(() => {
        result.current.setState(mockGameState, 'Initial');
        result.current.setState({ ...mockGameState, data: { ...mockGameState.data, round: 2 } }, 'Round 2');
      });

      expect(result.current.currentState?.data.round).toBe(2);

      act(() => {
        result.current.undo();
      });

      expect(result.current.currentState?.data.round).toBe(1);
      expect(result.current.undoStack).toHaveLength(0);
      expect(result.current.redoStack).toHaveLength(1);
    });

    it('redoes last undone change', () => {
      const { result } = renderHook(() => useGameStateStore());

      act(() => {
        result.current.setState(mockGameState, 'Initial');
        result.current.setState({ ...mockGameState, data: { ...mockGameState.data, round: 2 } }, 'Round 2');
        result.current.undo();
      });

      expect(result.current.currentState?.data.round).toBe(1);

      act(() => {
        result.current.redo();
      });

      expect(result.current.currentState?.data.round).toBe(2);
      expect(result.current.undoStack).toHaveLength(1);
      expect(result.current.redoStack).toHaveLength(0);
    });

    it('does nothing when undo stack is empty', () => {
      const { result } = renderHook(() => useGameStateStore());

      act(() => {
        result.current.setState(mockGameState, 'Initial');
        result.current.undo();
      });

      expect(result.current.currentState).toEqual(mockGameState);
      expect(result.current.undoStack).toEqual([]);
    });

    it('does nothing when redo stack is empty', () => {
      const { result } = renderHook(() => useGameStateStore());

      act(() => {
        result.current.setState(mockGameState, 'Initial');
        result.current.redo();
      });

      expect(result.current.currentState).toEqual(mockGameState);
      expect(result.current.redoStack).toEqual([]);
    });

    it('canUndo returns correct boolean', () => {
      const { result } = renderHook(() => useGameStateStore());

      expect(result.current.canUndo()).toBe(false);

      act(() => {
        result.current.setState(mockGameState, 'Initial');
        result.current.setState({ ...mockGameState, data: { ...mockGameState.data, round: 2 } }, 'Round 2');
      });

      expect(result.current.canUndo()).toBe(true);

      act(() => {
        result.current.undo();
      });

      expect(result.current.canUndo()).toBe(false);
    });

    it('canRedo returns correct boolean', () => {
      const { result } = renderHook(() => useGameStateStore());

      expect(result.current.canRedo()).toBe(false);

      act(() => {
        result.current.setState(mockGameState, 'Initial');
        result.current.setState({ ...mockGameState, data: { ...mockGameState.data, round: 2 } }, 'Round 2');
        result.current.undo();
      });

      expect(result.current.canRedo()).toBe(true);

      act(() => {
        result.current.redo();
      });

      expect(result.current.canRedo()).toBe(false);
    });
  });

  describe('Template Loading', () => {
    it('loads template successfully', async () => {
      const { result } = renderHook(() => useGameStateStore());

      const mockTemplate = { name: 'Catan', fields: [] };
      vi.mocked(api.sessions.getState).mockResolvedValueOnce({ template: mockTemplate });

      await act(async () => {
        await result.current.loadTemplate('session-123');
      });

      await waitFor(() => {
        expect(result.current.template).toEqual(mockTemplate);
        expect(result.current.isLoading).toBe(false);
        expect(result.current.error).toBeNull();
      });
    });

    it('handles template loading error', async () => {
      const { result } = renderHook(() => useGameStateStore());

      vi.mocked(api.sessions.getState).mockRejectedValueOnce(new Error('API error'));

      await act(async () => {
        await result.current.loadTemplate('session-123');
      });

      await waitFor(() => {
        expect(result.current.template).toBeNull();
        expect(result.current.isLoading).toBe(false);
        expect(result.current.error).toBe('API error');
      });
    });

    it('sets template to null if not in response', async () => {
      const { result } = renderHook(() => useGameStateStore());

      vi.mocked(api.sessions.getState).mockResolvedValueOnce({});

      await act(async () => {
        await result.current.loadTemplate('session-123');
      });

      await waitFor(() => {
        expect(result.current.template).toBeNull();
        expect(result.current.isLoading).toBe(false);
      });
    });
  });

  describe('State Loading', () => {
    it('loads state successfully', async () => {
      const { result } = renderHook(() => useGameStateStore());

      vi.mocked(api.sessions.getState).mockResolvedValueOnce({ currentState: mockGameState });

      await act(async () => {
        await result.current.loadState('session-123');
      });

      await waitFor(() => {
        expect(result.current.currentState).toEqual(mockGameState);
        expect(result.current.isLoading).toBe(false);
        expect(result.current.error).toBeNull();
      });
    });

    it('handles state loading error', async () => {
      const { result } = renderHook(() => useGameStateStore());

      vi.mocked(api.sessions.getState).mockRejectedValueOnce(new Error('Failed to fetch'));

      await act(async () => {
        await result.current.loadState('session-123');
      });

      await waitFor(() => {
        expect(result.current.currentState).toBeNull();
        expect(result.current.isLoading).toBe(false);
        expect(result.current.error).toBe('Failed to fetch');
      });
    });

    it('throws error if no state in response', async () => {
      const { result } = renderHook(() => useGameStateStore());

      vi.mocked(api.sessions.getState).mockResolvedValueOnce({});

      await act(async () => {
        await result.current.loadState('session-123');
      });

      await waitFor(() => {
        expect(result.current.error).toBe('No state returned from API');
      });
    });
  });

  describe('State Saving', () => {
    it('saves state successfully', async () => {
      const { result } = renderHook(() => useGameStateStore());

      act(() => {
        result.current.setState(mockGameState, 'Initial');
      });

      vi.mocked(api.sessions.updateState).mockResolvedValueOnce(undefined);

      await act(async () => {
        await result.current.saveState('session-123');
      });

      await waitFor(() => {
        expect(api.sessions.updateState).toHaveBeenCalledWith('session-123', JSON.stringify(mockGameState));
        expect(result.current.isLoading).toBe(false);
        expect(result.current.error).toBeNull();
      });
    });

    it('handles save error', async () => {
      const { result } = renderHook(() => useGameStateStore());

      act(() => {
        result.current.setState(mockGameState, 'Initial');
      });

      vi.mocked(api.sessions.updateState).mockRejectedValueOnce(new Error('Save failed'));

      await act(async () => {
        await result.current.saveState('session-123');
      });

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
        expect(result.current.error).toBe('Save failed');
      });
    });

    it('does nothing if no current state', async () => {
      const { result } = renderHook(() => useGameStateStore());

      await act(async () => {
        await result.current.saveState('session-123');
      });

      expect(api.sessions.updateState).not.toHaveBeenCalled();
    });
  });

  describe('Snapshot Management', () => {
    it('adds snapshot correctly', () => {
      const { result } = renderHook(() => useGameStateStore());

      act(() => {
        result.current.addSnapshot(mockSnapshot);
      });

      expect(result.current.snapshots).toHaveLength(1);
      expect(result.current.snapshots[0]).toEqual(mockSnapshot);
    });

    it('loads snapshot by id', () => {
      const { result } = renderHook(() => useGameStateStore());

      act(() => {
        result.current.setState(mockGameState, 'Initial');
        result.current.addSnapshot(mockSnapshot);
        result.current.loadSnapshot('snapshot-1');
      });

      expect(result.current.currentState).toEqual(mockGameState);
      expect(result.current.undoStack).toHaveLength(1);
    });

    it('does nothing if snapshot not found', () => {
      const { result } = renderHook(() => useGameStateStore());

      act(() => {
        result.current.setState(mockGameState, 'Initial');
        result.current.loadSnapshot('nonexistent');
      });

      expect(result.current.currentState).toEqual(mockGameState);
      expect(result.current.undoStack).toEqual([]);
    });
  });

  describe('Conflict Management', () => {
    it('detects conflict', () => {
      const { result } = renderHook(() => useGameStateStore());

      act(() => {
        result.current.detectConflict(mockConflict);
      });

      expect(result.current.conflicts).toHaveLength(1);
      expect(result.current.conflicts[0]).toEqual(mockConflict);
    });

    it('resolves conflict with local value', () => {
      const { result } = renderHook(() => useGameStateStore());

      act(() => {
        result.current.setState(mockGameState, 'Initial');
        result.current.detectConflict(mockConflict);
        result.current.resolveConflict(0, true);
      });

      expect(result.current.currentState?.data.round).toBe(2);
      expect(result.current.conflicts).toHaveLength(0);
    });

    it('resolves conflict with remote value', () => {
      const { result } = renderHook(() => useGameStateStore());

      act(() => {
        result.current.setState(mockGameState, 'Initial');
        result.current.detectConflict(mockConflict);
        result.current.resolveConflict(0, false);
      });

      expect(result.current.currentState?.data.round).toBe(3);
      expect(result.current.conflicts).toHaveLength(0);
    });

    it('does nothing if conflict index invalid', () => {
      const { result } = renderHook(() => useGameStateStore());

      act(() => {
        result.current.setState(mockGameState, 'Initial');
        result.current.detectConflict(mockConflict);
        result.current.resolveConflict(5, true);
      });

      expect(result.current.conflicts).toHaveLength(1);
    });
  });

  describe('Connection Status', () => {
    it('sets connection status correctly', () => {
      const { result } = renderHook(() => useGameStateStore());

      act(() => {
        result.current.setConnectionStatus(true);
      });

      expect(result.current.isConnected).toBe(true);
      expect(result.current.connectionError).toBeUndefined();
    });

    it('sets connection error', () => {
      const { result } = renderHook(() => useGameStateStore());

      act(() => {
        result.current.setConnectionStatus(false, 'Connection lost');
      });

      expect(result.current.isConnected).toBe(false);
      expect(result.current.connectionError).toBe('Connection lost');
    });
  });

  describe('Reset', () => {
    it('resets store to initial state', () => {
      const { result } = renderHook(() => useGameStateStore());

      act(() => {
        result.current.setState(mockGameState, 'Initial');
        result.current.setTemplate({ name: 'Test', fields: [] });
        result.current.addSnapshot(mockSnapshot);
        result.current.detectConflict(mockConflict);
        result.current.setConnectionStatus(true);
      });

      expect(result.current.currentState).not.toBeNull();
      expect(result.current.template).not.toBeNull();
      expect(result.current.snapshots).toHaveLength(1);
      expect(result.current.conflicts).toHaveLength(1);
      expect(result.current.isConnected).toBe(true);

      act(() => {
        result.current.reset();
      });

      expect(result.current.currentState).toBeNull();
      expect(result.current.template).toBeNull();
      expect(result.current.isLoading).toBe(false);
      expect(result.current.error).toBeNull();
      expect(result.current.undoStack).toEqual([]);
      expect(result.current.redoStack).toEqual([]);
      expect(result.current.snapshots).toEqual([]);
      expect(result.current.conflicts).toEqual([]);
      expect(result.current.isConnected).toBe(false);
      expect(result.current.connectionError).toBeNull();
    });
  });

  describe('Edge Cases', () => {
    it('handles multiple undo/redo cycles', () => {
      const { result } = renderHook(() => useGameStateStore());

      act(() => {
        result.current.setState(mockGameState, 'Initial');
        result.current.setState({ ...mockGameState, data: { ...mockGameState.data, round: 2 } }, 'Round 2');
        result.current.setState({ ...mockGameState, data: { ...mockGameState.data, round: 3 } }, 'Round 3');
        result.current.undo();
        result.current.undo();
        result.current.redo();
        result.current.redo();
      });

      expect(result.current.currentState?.data.round).toBe(3);
    });

    it('handles deep nested path updates', () => {
      const { result } = renderHook(() => useGameStateStore());

      act(() => {
        result.current.setState(mockGameState, 'Initial');
        result.current.updateField(['data', 'players', '0', 'inventory', 'wood'], 5, 'Resource added');
      });

      const player0 = result.current.currentState?.data.players[0] as { inventory?: { wood?: number } };
      expect(player0.inventory?.wood).toBe(5);
    });
  });
});
