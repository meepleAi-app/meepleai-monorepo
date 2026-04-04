/**
 * useTurnOrder Hook Tests (Issue #4975)
 *
 * Coverage:
 * - Initial state: turnOrder null, isLoading false, isAdvancing false, error null
 * - loadTurnOrder: success (sets state), 404 (sets null), error (sets error)
 * - advance: optimistic isAdvancing flag, success (sets state), error
 * - reset: success (sets state), error
 * - applySSEAdvance: updates currentIndex, currentPlayer, nextPlayer, roundNumber
 * - SSE v2 listener: created on mount, closed on unmount
 *
 * Note: Full real-time behavior is tested in E2E tests.
 */

import { renderHook, act, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { useTurnOrder } from '../useTurnOrder';
import type { TurnOrderData, TurnAdvancedPayload } from '@/components/session/types';

// ── fixtures ──────────────────────────────────────────────────────────────────

const TURN_ORDER: TurnOrderData = {
  id: 'to-1',
  sessionId: 'sess-abc',
  playerOrder: ['Alice', 'Bob', 'Charlie'],
  currentIndex: 0,
  currentPlayer: 'Alice',
  nextPlayer: 'Bob',
  roundNumber: 1,
};

const ADVANCED: TurnOrderData = {
  ...TURN_ORDER,
  currentIndex: 1,
  currentPlayer: 'Bob',
  nextPlayer: 'Charlie',
  roundNumber: 1,
};

// ── helpers ───────────────────────────────────────────────────────────────────

function mockFetch(status: number, body: unknown) {
  return vi.fn().mockResolvedValue({
    ok: status >= 200 && status < 300,
    status,
    statusText: status === 404 ? 'Not Found' : status === 500 ? 'Internal Server Error' : 'OK',
    json: vi.fn().mockResolvedValue(body),
  });
}

// ── tests ──────────────────────────────────────────────────────────────────────

describe('useTurnOrder', () => {
  let mockEventSource: {
    close: ReturnType<typeof vi.fn>;
    addEventListener: ReturnType<typeof vi.fn>;
    removeEventListener: ReturnType<typeof vi.fn>;
    readyState: number;
  };

  beforeEach(() => {
    mockEventSource = {
      close: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      readyState: 0, // CONNECTING
    };

    global.EventSource = vi
      .fn()
      .mockImplementation(() => mockEventSource) as unknown as typeof EventSource;
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  // ── Initial state ────────────────────────────────────────────────────────────

  describe('initial state', () => {
    it('starts with null turnOrder', async () => {
      global.fetch = mockFetch(404, null);

      const { result } = renderHook(() => useTurnOrder({ sessionId: 'sess-abc', apiBaseUrl: '' }));

      expect(result.current.turnOrder).toBeNull();
    });

    it('starts with isAdvancing=false', async () => {
      global.fetch = mockFetch(404, null);

      const { result } = renderHook(() => useTurnOrder({ sessionId: 'sess-abc', apiBaseUrl: '' }));

      expect(result.current.isAdvancing).toBe(false);
    });

    it('starts with error=null', async () => {
      global.fetch = mockFetch(404, null);

      const { result } = renderHook(() => useTurnOrder({ sessionId: 'sess-abc', apiBaseUrl: '' }));

      expect(result.current.error).toBeNull();
    });
  });

  // ── loadTurnOrder ────────────────────────────────────────────────────────────

  describe('loadTurnOrder', () => {
    it('sets turnOrder on successful fetch', async () => {
      global.fetch = mockFetch(200, TURN_ORDER);

      const { result } = renderHook(() => useTurnOrder({ sessionId: 'sess-abc', apiBaseUrl: '' }));

      await waitFor(() => expect(result.current.isLoading).toBe(false));
      expect(result.current.turnOrder).toEqual(TURN_ORDER);
    });

    it('sets turnOrder to null on 404', async () => {
      global.fetch = mockFetch(404, null);

      const { result } = renderHook(() => useTurnOrder({ sessionId: 'sess-abc', apiBaseUrl: '' }));

      await waitFor(() => expect(result.current.isLoading).toBe(false));
      expect(result.current.turnOrder).toBeNull();
    });

    it('sets error on non-404 HTTP error', async () => {
      global.fetch = mockFetch(500, null);

      const { result } = renderHook(() => useTurnOrder({ sessionId: 'sess-abc', apiBaseUrl: '' }));

      await waitFor(() => expect(result.current.isLoading).toBe(false));
      expect(result.current.error).toMatch(/HTTP 500/);
    });
  });

  // ── advance ──────────────────────────────────────────────────────────────────

  describe('advance', () => {
    it('sets turnOrder to server response', async () => {
      // First load: 404 (not yet initialized)
      // Advance call: 200 with ADVANCED
      let callCount = 0;
      global.fetch = vi.fn().mockImplementation(() => {
        callCount++;
        if (callCount === 1)
          return Promise.resolve({
            ok: false,
            status: 404,
            statusText: 'Not Found',
            json: vi.fn(),
          });
        return Promise.resolve({
          ok: true,
          status: 200,
          statusText: 'OK',
          json: vi.fn().mockResolvedValue(ADVANCED),
        });
      });

      const { result } = renderHook(() => useTurnOrder({ sessionId: 'sess-abc', apiBaseUrl: '' }));

      await waitFor(() => expect(result.current.isLoading).toBe(false));

      await act(async () => {
        await result.current.advance();
      });

      expect(result.current.turnOrder).toEqual(ADVANCED);
    });

    it('sets error on advance failure and re-throws', async () => {
      global.fetch = vi.fn().mockImplementation(() => {
        return Promise.resolve({ ok: false, status: 409, statusText: 'Conflict', json: vi.fn() });
      });

      const { result } = renderHook(() => useTurnOrder({ sessionId: 'sess-abc', apiBaseUrl: '' }));

      await act(async () => {
        await expect(result.current.advance()).rejects.toThrow();
      });

      expect(result.current.error).toMatch(/HTTP 409/);
    });

    it('resets isAdvancing to false after advance completes', async () => {
      global.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        statusText: 'OK',
        json: vi.fn().mockResolvedValue(ADVANCED),
      });

      const { result } = renderHook(() => useTurnOrder({ sessionId: 'sess-abc', apiBaseUrl: '' }));

      await act(async () => {
        await result.current.advance();
      });

      expect(result.current.isAdvancing).toBe(false);
    });
  });

  // ── reset ────────────────────────────────────────────────────────────────────

  describe('reset', () => {
    it('sets turnOrder to server response on success', async () => {
      global.fetch = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        statusText: 'OK',
        json: vi.fn().mockResolvedValue(TURN_ORDER),
      });

      const { result } = renderHook(() => useTurnOrder({ sessionId: 'sess-abc', apiBaseUrl: '' }));

      await act(async () => {
        await result.current.reset();
      });

      expect(result.current.turnOrder).toEqual(TURN_ORDER);
    });

    it('sets error on reset failure', async () => {
      global.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 503,
        statusText: 'Service Unavailable',
        json: vi.fn(),
      });

      const { result } = renderHook(() => useTurnOrder({ sessionId: 'sess-abc', apiBaseUrl: '' }));

      await act(async () => {
        await expect(result.current.reset()).rejects.toThrow();
      });

      expect(result.current.error).toMatch(/HTTP 503/);
    });
  });

  // ── applySSEAdvance ──────────────────────────────────────────────────────────

  describe('applySSEAdvance', () => {
    it('does nothing when turnOrder is null', async () => {
      global.fetch = mockFetch(404, null);

      const { result } = renderHook(() => useTurnOrder({ sessionId: 'sess-abc', apiBaseUrl: '' }));

      await waitFor(() => expect(result.current.isLoading).toBe(false));

      const payload: TurnAdvancedPayload = {
        currentPlayerName: 'Alice',
        previousPlayerName: 'Charlie',
        nextPlayerName: 'Bob',
        roundNumber: 2,
      };

      act(() => {
        result.current.applySSEAdvance(payload);
      });

      expect(result.current.turnOrder).toBeNull();
    });

    it('updates currentPlayer, nextPlayer, roundNumber, and currentIndex from payload', async () => {
      global.fetch = mockFetch(200, TURN_ORDER);

      const { result } = renderHook(() => useTurnOrder({ sessionId: 'sess-abc', apiBaseUrl: '' }));

      await waitFor(() => expect(result.current.turnOrder).not.toBeNull());

      const payload: TurnAdvancedPayload = {
        currentPlayerName: 'Bob',
        previousPlayerName: 'Alice',
        nextPlayerName: 'Charlie',
        roundNumber: 1,
      };

      act(() => {
        result.current.applySSEAdvance(payload);
      });

      expect(result.current.turnOrder?.currentPlayer).toBe('Bob');
      expect(result.current.turnOrder?.nextPlayer).toBe('Charlie');
      expect(result.current.turnOrder?.currentIndex).toBe(1); // Bob is at index 1
      expect(result.current.turnOrder?.roundNumber).toBe(1);
    });

    it('calls onTurnAdvanced callback if provided', async () => {
      global.fetch = mockFetch(200, TURN_ORDER);
      const onTurnAdvanced = vi.fn();

      const { result } = renderHook(() =>
        useTurnOrder({ sessionId: 'sess-abc', apiBaseUrl: '', onTurnAdvanced })
      );

      await waitFor(() => expect(result.current.turnOrder).not.toBeNull());

      const payload: TurnAdvancedPayload = {
        currentPlayerName: 'Bob',
        previousPlayerName: 'Alice',
        nextPlayerName: 'Charlie',
        roundNumber: 1,
      };

      act(() => {
        result.current.applySSEAdvance(payload);
      });

      expect(onTurnAdvanced).toHaveBeenCalledOnce();
      expect(onTurnAdvanced).toHaveBeenCalledWith(payload);
    });
  });

  // ── SSE lifecycle ────────────────────────────────────────────────────────────

  describe('SSE v2 listener', () => {
    it('creates an EventSource on mount', async () => {
      global.fetch = mockFetch(404, null);

      renderHook(() => useTurnOrder({ sessionId: 'sess-123', apiBaseUrl: '' }));

      expect(global.EventSource).toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/game-sessions/sess-123/stream/v2'),
        expect.objectContaining({ withCredentials: true })
      );
    });

    it('closes the EventSource on unmount', async () => {
      global.fetch = mockFetch(404, null);

      const { unmount } = renderHook(() => useTurnOrder({ sessionId: 'sess-123', apiBaseUrl: '' }));

      unmount();

      expect(mockEventSource.close).toHaveBeenCalled();
    });

    it('registers a session:toolkit event listener', async () => {
      global.fetch = mockFetch(404, null);

      renderHook(() => useTurnOrder({ sessionId: 'sess-123', apiBaseUrl: '' }));

      expect(mockEventSource.addEventListener).toHaveBeenCalledWith(
        'session:toolkit',
        expect.any(Function)
      );
    });
  });
});
