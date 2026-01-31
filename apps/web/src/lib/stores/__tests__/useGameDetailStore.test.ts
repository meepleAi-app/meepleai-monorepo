/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, beforeEach } from 'vitest';
import { act, renderHook } from '@testing-library/react';

import { useGameDetailStore } from '../useGameDetailStore';

describe('useGameDetailStore', () => {
  beforeEach(() => {
    const { result } = renderHook(() => useGameDetailStore());
    act(() => {
      result.current.reset();
    });
  });

  it('should initialize with default state', () => {
    const { result } = renderHook(() => useGameDetailStore());

    expect(result.current.gameId).toBeNull();
    expect(result.current.currentState).toBeNull();
    expect(result.current.isUpdatingState).toBe(false);
    expect(result.current.isRecordingSession).toBe(false);
    expect(result.current.optimisticSessionId).toBeNull();
    expect(result.current.error).toBeNull();
  });

  it('should set gameId', () => {
    const { result } = renderHook(() => useGameDetailStore());
    const testGameId = 'test-game-123';

    act(() => {
      result.current.setGameId(testGameId);
    });

    expect(result.current.gameId).toBe(testGameId);
  });

  it('should set currentState', () => {
    const { result } = renderHook(() => useGameDetailStore());

    act(() => {
      result.current.setCurrentState('Owned');
    });

    expect(result.current.currentState).toBe('Owned');
  });

  it('should toggle isUpdatingState', () => {
    const { result } = renderHook(() => useGameDetailStore());

    act(() => {
      result.current.setIsUpdatingState(true);
    });
    expect(result.current.isUpdatingState).toBe(true);

    act(() => {
      result.current.setIsUpdatingState(false);
    });
    expect(result.current.isUpdatingState).toBe(false);
  });

  it('should toggle isRecordingSession', () => {
    const { result } = renderHook(() => useGameDetailStore());

    act(() => {
      result.current.setIsRecordingSession(true);
    });
    expect(result.current.isRecordingSession).toBe(true);

    act(() => {
      result.current.setIsRecordingSession(false);
    });
    expect(result.current.isRecordingSession).toBe(false);
  });

  it('should set optimisticSessionId', () => {
    const { result } = renderHook(() => useGameDetailStore());
    const sessionId = 'session-123';

    act(() => {
      result.current.setOptimisticSessionId(sessionId);
    });

    expect(result.current.optimisticSessionId).toBe(sessionId);
  });

  it('should set and clear error', () => {
    const { result } = renderHook(() => useGameDetailStore());
    const errorMsg = 'Test error';

    act(() => {
      result.current.setError(errorMsg);
    });
    expect(result.current.error).toBe(errorMsg);

    act(() => {
      result.current.setError(null);
    });
    expect(result.current.error).toBeNull();
  });

  it('should reset to initial state', () => {
    const { result } = renderHook(() => useGameDetailStore());

    // Set various state values
    act(() => {
      result.current.setGameId('test-123');
      result.current.setCurrentState('Owned');
      result.current.setIsUpdatingState(true);
      result.current.setError('Error');
    });

    // Reset
    act(() => {
      result.current.reset();
    });

    expect(result.current.gameId).toBeNull();
    expect(result.current.currentState).toBeNull();
    expect(result.current.isUpdatingState).toBe(false);
    expect(result.current.error).toBeNull();
  });

  describe('State Transitions', () => {
    it('should transition through all valid states', () => {
      const { result } = renderHook(() => useGameDetailStore());
      const states = ['Nuovo', 'Wishlist', 'Owned', 'InPrestito', null] as const;

      states.forEach((state) => {
        act(() => {
          result.current.setCurrentState(state);
        });
        expect(result.current.currentState).toBe(state);
      });
    });

    it('should not affect other properties when changing state', () => {
      const { result } = renderHook(() => useGameDetailStore());

      act(() => {
        result.current.setGameId('game-123');
        result.current.setCurrentState('Wishlist');
      });

      expect(result.current.gameId).toBe('game-123');
      expect(result.current.currentState).toBe('Wishlist');
    });
  });

  describe('Optimistic Updates', () => {
    it('should simulate optimistic state update success flow', () => {
      const { result } = renderHook(() => useGameDetailStore());

      // Initial state
      act(() => {
        result.current.setGameId('game-123');
        result.current.setCurrentState('Wishlist');
      });

      // Start optimistic update
      act(() => {
        result.current.setIsUpdatingState(true);
        result.current.setCurrentState('Owned'); // Optimistic
      });

      expect(result.current.currentState).toBe('Owned');
      expect(result.current.isUpdatingState).toBe(true);

      // Success - finalize
      act(() => {
        result.current.setIsUpdatingState(false);
        result.current.setError(null);
      });

      expect(result.current.currentState).toBe('Owned');
      expect(result.current.isUpdatingState).toBe(false);
      expect(result.current.error).toBeNull();
    });

    it('should simulate optimistic update rollback on error', () => {
      const { result } = renderHook(() => useGameDetailStore());

      // Set initial state
      act(() => {
        result.current.setGameId('game-123');
        result.current.setCurrentState('Wishlist');
      });

      const previousState = result.current.currentState;

      // Start optimistic update
      act(() => {
        result.current.setIsUpdatingState(true);
        result.current.setCurrentState('Owned');
      });

      // Error - rollback
      act(() => {
        result.current.setCurrentState(previousState);
        result.current.setIsUpdatingState(false);
        result.current.setError('Failed to update state');
      });

      expect(result.current.currentState).toBe('Wishlist');
      expect(result.current.isUpdatingState).toBe(false);
      expect(result.current.error).toBe('Failed to update state');
    });

    it('should handle session recording with optimistic ID', () => {
      const { result } = renderHook(() => useGameDetailStore());

      // Start recording with temp ID
      act(() => {
        result.current.setGameId('game-123');
        result.current.setIsRecordingSession(true);
        result.current.setOptimisticSessionId('temp-session-123');
      });

      expect(result.current.isRecordingSession).toBe(true);
      expect(result.current.optimisticSessionId).toBe('temp-session-123');

      // Replace with server ID
      act(() => {
        result.current.setOptimisticSessionId('server-session-456');
        result.current.setIsRecordingSession(false);
      });

      expect(result.current.optimisticSessionId).toBe('server-session-456');
      expect(result.current.isRecordingSession).toBe(false);
    });

    it('should clear optimistic session on error', () => {
      const { result } = renderHook(() => useGameDetailStore());

      act(() => {
        result.current.setIsRecordingSession(true);
        result.current.setOptimisticSessionId('temp-123');
      });

      // Error occurs
      act(() => {
        result.current.setIsRecordingSession(false);
        result.current.setOptimisticSessionId(null);
        result.current.setError('Failed to record session');
      });

      expect(result.current.optimisticSessionId).toBeNull();
      expect(result.current.error).toBe('Failed to record session');
    });
  });

  describe('Error Handling', () => {
    it('should set error message', () => {
      const { result } = renderHook(() => useGameDetailStore());
      const errorMsg = 'Network error occurred';

      act(() => {
        result.current.setError(errorMsg);
      });

      expect(result.current.error).toBe(errorMsg);
    });

    it('should update error to different message', () => {
      const { result } = renderHook(() => useGameDetailStore());

      act(() => {
        result.current.setError('Error 1');
        result.current.setError('Error 2');
      });

      expect(result.current.error).toBe('Error 2');
    });

    it('should preserve error across state changes', () => {
      const { result } = renderHook(() => useGameDetailStore());

      act(() => {
        result.current.setError('Persistent error');
        result.current.setCurrentState('Owned');
      });

      expect(result.current.error).toBe('Persistent error');
      expect(result.current.currentState).toBe('Owned');
    });

    it('should clear error on successful operation', () => {
      const { result } = renderHook(() => useGameDetailStore());

      act(() => {
        result.current.setError('Previous error');
        result.current.setIsUpdatingState(true);
        result.current.setError(null); // Clear on success
        result.current.setIsUpdatingState(false);
      });

      expect(result.current.error).toBeNull();
    });
  });

  describe('Edge Cases', () => {
    it('should handle rapid state changes', () => {
      const { result } = renderHook(() => useGameDetailStore());

      act(() => {
        result.current.setCurrentState('Nuovo');
        result.current.setCurrentState('Wishlist');
        result.current.setCurrentState('Owned');
        result.current.setCurrentState('InPrestito');
      });

      expect(result.current.currentState).toBe('InPrestito');
    });

    it('should handle setting same value multiple times', () => {
      const { result } = renderHook(() => useGameDetailStore());

      act(() => {
        result.current.setGameId('game-123');
        result.current.setGameId('game-123');
        result.current.setGameId('game-123');
      });

      expect(result.current.gameId).toBe('game-123');
    });

    it('should handle concurrent updates to all properties', () => {
      const { result } = renderHook(() => useGameDetailStore());

      act(() => {
        result.current.setGameId('game-1');
        result.current.setCurrentState('Owned');
        result.current.setIsUpdatingState(true);
        result.current.setIsRecordingSession(true);
        result.current.setOptimisticSessionId('session-1');
        result.current.setError('error-1');
      });

      expect(result.current.gameId).toBe('game-1');
      expect(result.current.currentState).toBe('Owned');
      expect(result.current.isUpdatingState).toBe(true);
      expect(result.current.isRecordingSession).toBe(true);
      expect(result.current.optimisticSessionId).toBe('session-1');
      expect(result.current.error).toBe('error-1');
    });

    it('should maintain state across multiple hook instances', () => {
      const { result: result1 } = renderHook(() => useGameDetailStore());
      const { result: result2 } = renderHook(() => useGameDetailStore());

      act(() => {
        result1.current.setGameId('game-shared');
      });

      // Both instances see same state (shared store)
      expect(result1.current.gameId).toBe('game-shared');
      expect(result2.current.gameId).toBe('game-shared');
    });

    it('should allow operations after reset', () => {
      const { result } = renderHook(() => useGameDetailStore());

      act(() => {
        result.current.setGameId('game-1');
        result.current.reset();
        result.current.setGameId('game-2');
      });

      expect(result.current.gameId).toBe('game-2');
    });

    it('should preserve action functions after reset', () => {
      const { result } = renderHook(() => useGameDetailStore());

      act(() => {
        result.current.reset();
      });

      expect(typeof result.current.setGameId).toBe('function');
      expect(typeof result.current.setCurrentState).toBe('function');
      expect(typeof result.current.setIsUpdatingState).toBe('function');
      expect(typeof result.current.setIsRecordingSession).toBe('function');
      expect(typeof result.current.setOptimisticSessionId).toBe('function');
      expect(typeof result.current.setError).toBe('function');
      expect(typeof result.current.reset).toBe('function');
    });
  });

  describe('Complex Workflows', () => {
    it('should handle complete state change workflow with success', () => {
      const { result } = renderHook(() => useGameDetailStore());

      // Setup
      act(() => {
        result.current.setGameId('game-123');
        result.current.setCurrentState('Wishlist');
      });

      // Start update
      act(() => {
        result.current.setIsUpdatingState(true);
        result.current.setCurrentState('Owned'); // Optimistic
      });

      // API success
      act(() => {
        result.current.setIsUpdatingState(false);
        result.current.setError(null);
      });

      expect(result.current.currentState).toBe('Owned');
      expect(result.current.error).toBeNull();
    });

    it('should handle complete session recording workflow', () => {
      const { result } = renderHook(() => useGameDetailStore());

      // Start session
      act(() => {
        result.current.setGameId('game-123');
        result.current.setIsRecordingSession(true);
        result.current.setOptimisticSessionId('temp-abc');
      });

      // API returns real ID
      act(() => {
        result.current.setOptimisticSessionId('real-xyz');
        result.current.setIsRecordingSession(false);
      });

      expect(result.current.optimisticSessionId).toBe('real-xyz');
      expect(result.current.isRecordingSession).toBe(false);
    });

    it('should handle multiple sequential operations', () => {
      const { result } = renderHook(() => useGameDetailStore());

      // Operation 1: Set game
      act(() => {
        result.current.setGameId('game-123');
      });
      expect(result.current.gameId).toBe('game-123');

      // Operation 2: Update state
      act(() => {
        result.current.setIsUpdatingState(true);
        result.current.setCurrentState('Owned');
        result.current.setIsUpdatingState(false);
      });
      expect(result.current.currentState).toBe('Owned');

      // Operation 3: Record session
      act(() => {
        result.current.setIsRecordingSession(true);
        result.current.setOptimisticSessionId('session-1');
        result.current.setIsRecordingSession(false);
      });
      expect(result.current.optimisticSessionId).toBe('session-1');
    });
  });
});
