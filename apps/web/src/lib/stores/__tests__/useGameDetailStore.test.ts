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
});
