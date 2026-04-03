import { renderHook, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { useOfflineToolkit } from '../useOfflineToolkit';

vi.mock('@/lib/offline/toolkitDb', () => ({
  queueOperation: vi.fn().mockResolvedValue(undefined),
  syncPendingOperations: vi.fn().mockResolvedValue(undefined),
  getPendingOperations: vi.fn().mockResolvedValue([]),
  clearOperation: vi.fn().mockResolvedValue(undefined),
}));

vi.mock('@/lib/api/clients/gameSessionsClient', () => ({
  rollDice: vi.fn().mockResolvedValue(undefined),
  updateScore: vi.fn().mockResolvedValue(undefined),
}));

const SESSION_ID = 'session-abc';

describe('useOfflineToolkit', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    Object.defineProperty(navigator, 'onLine', { value: true, writable: true, configurable: true });
  });

  it('online mode: rollDice returns local results immediately with correct length', async () => {
    const { result } = renderHook(() => useOfflineToolkit(SESSION_ID));

    let results: number[] = [];
    await act(async () => {
      results = await result.current.rollDice('d6', 3);
    });

    expect(results).toHaveLength(3);
    results.forEach(r => {
      expect(r).toBeGreaterThanOrEqual(1);
      expect(r).toBeLessThanOrEqual(6);
    });
  });

  it('offline mode: rollDice still returns local results', async () => {
    Object.defineProperty(navigator, 'onLine', {
      value: false,
      writable: true,
      configurable: true,
    });

    const { result } = renderHook(() => useOfflineToolkit(SESSION_ID));

    // Trigger the offline event so the hook updates isOnlineRef
    act(() => {
      window.dispatchEvent(new Event('offline'));
    });

    let results: number[] = [];
    await act(async () => {
      results = await result.current.rollDice('d6', 2);
    });

    expect(results).toHaveLength(2);
    results.forEach(r => {
      expect(r).toBeGreaterThanOrEqual(1);
      expect(r).toBeLessThanOrEqual(6);
    });
  });

  it('offline mode: updateScore queues the operation', async () => {
    const { queueOperation } = await import('@/lib/offline/toolkitDb');

    Object.defineProperty(navigator, 'onLine', {
      value: false,
      writable: true,
      configurable: true,
    });

    const { result } = renderHook(() => useOfflineToolkit(SESSION_ID));

    act(() => {
      window.dispatchEvent(new Event('offline'));
    });

    await act(async () => {
      await result.current.updateScore('player-1', 42);
    });

    expect(queueOperation).toHaveBeenCalledWith(
      expect.objectContaining({
        sessionId: SESSION_ID,
        type: 'score_update',
        payload: { playerId: 'player-1', score: 42 },
      })
    );
  });
});
