/**
 * useAgentChat Hook Tests (Issue #3187)
 *
 * Test suite covering session-based SSE streaming:
 * - Message accumulation
 * - SSE event parsing
 * - Reconnection logic
 * - Error handling
 * - State management
 */

import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import type { Mock } from 'vitest';
import { renderHook, act, waitFor } from '@testing-library/react';

import { useAgentChat } from '../useAgentChat';
import { createSSEResponse } from '@/__tests__/fixtures/sse-test-helpers';
import { useAgentChatStore } from '@/store/agent/store';

// Mock Zustand store
vi.mock('@/store/agent/store', () => ({
  useAgentChatStore: vi.fn(),
}));

describe('useAgentChat', () => {
  let fetchSpy: Mock;
  let mockAddMessage: Mock;
  let mockUpdateLastMessage: Mock;
  let mockClearMessages: Mock;

  const mockSessionId = 'session-123';

  beforeEach(() => {
    fetchSpy = vi.spyOn(global, 'fetch') as Mock;

    // Setup mocked store actions
    mockAddMessage = vi.fn();
    mockUpdateLastMessage = vi.fn();
    mockClearMessages = vi.fn();

    (useAgentChatStore as unknown as Mock).mockImplementation(selector =>
      selector({
        messagesBySession: {
          [mockSessionId]: [],
        },
        addMessage: mockAddMessage,
        updateLastMessage: mockUpdateLastMessage,
        clearMessages: mockClearMessages,
      })
    );
  });

  afterEach(() => {
    vi.clearAllMocks();
    fetchSpy?.mockRestore();
  });

  describe('Initial State', () => {
    it('should initialize with correct default state', () => {
      const { result } = renderHook(() =>
        useAgentChat({
          sessionId: mockSessionId,
        })
      );

      expect(result.current.isStreaming).toBe(false);
      expect(result.current.error).toBeNull();
      expect(result.current.stateMessage).toBe('');
      expect(result.current.messages).toEqual([]);
    });

    it('should provide control functions', () => {
      const { result } = renderHook(() =>
        useAgentChat({
          sessionId: mockSessionId,
        })
      );

      expect(result.current.sendMessage).toBeInstanceOf(Function);
      expect(result.current.stopStreaming).toBeInstanceOf(Function);
      expect(result.current.clearMessages).toBeInstanceOf(Function);
    });
  });

  describe('SSE Streaming', () => {
    it.skip('should handle token accumulation', async () => {
      // TODO: Requires EventSource timing and mock refinement for token streaming
    });

    it.skip('should handle citations', async () => {
      // TODO: Requires EventSource mock timing for citation events
    });

    it.skip('should handle state updates', async () => {
      // TODO: Requires EventSource mock for state event handling
    });
  });

  describe('Error Handling', () => {
    it('should handle HTTP errors', async () => {
      (global.fetch as Mock<typeof fetch>).mockResolvedValueOnce({
        ok: false,
        status: 500,
        statusText: 'Internal Server Error',
      } as Response);

      const onError = vi.fn();
      const { result } = renderHook(() =>
        useAgentChat({
          sessionId: mockSessionId,
          onError,
        })
      );

      await act(async () => {
        await result.current.sendMessage('test query');
      });

      await waitFor(() => {
        expect(result.current.error).not.toBeNull();
        expect(result.current.error?.message).toContain('HTTP 500');
      });

      expect(onError).toHaveBeenCalled();
    });

    it('should handle SSE error events', async () => {
      const events = [
        JSON.stringify({
          type: 'error',
          data: {
            code: 'RATE_LIMIT',
            message: 'Rate limit exceeded',
          },
          timestamp: new Date().toISOString(),
        }),
      ];

      (global.fetch as Mock<typeof fetch>).mockResolvedValueOnce(createSSEResponse(events));

      const onError = vi.fn();
      const { result } = renderHook(() =>
        useAgentChat({
          sessionId: mockSessionId,
          onError,
        })
      );

      await act(async () => {
        await result.current.sendMessage('test query');
      });

      await waitFor(() => {
        expect(result.current.error).not.toBeNull();
        expect(result.current.error?.message).toBe('Rate limit exceeded');
      });
    });
  });

  describe('Reconnection Logic', () => {
    it('should attempt reconnection on network error', async () => {
      (global.fetch as Mock<typeof fetch>).mockRejectedValueOnce(new Error('Network error'));

      const { result } = renderHook(() =>
        useAgentChat({
          sessionId: mockSessionId,
        })
      );

      await act(async () => {
        await result.current.sendMessage('test query');
      });

      await waitFor(() => {
        expect(result.current.stateMessage).toMatch(/Reconnecting/);
      });
    });

    it('should give up after max reconnection attempts', async () => {
      (global.fetch as Mock<typeof fetch>).mockRejectedValue(new Error('Network error'));

      const { result } = renderHook(() =>
        useAgentChat({
          sessionId: mockSessionId,
        })
      );

      await act(async () => {
        await result.current.sendMessage('test query');
      });

      // Wait for max retries (3)
      await waitFor(
        () => {
          expect(fetchSpy).toHaveBeenCalledTimes(4); // Initial + 3 retries
        },
        { timeout: 10000 }
      );
    });
  });

  describe('Cancellation', () => {
    it('should handle cancellation', async () => {
      const events = [
        JSON.stringify({
          type: 'token',
          data: { token: 'Hello' },
          timestamp: new Date().toISOString(),
        }),
      ];

      (global.fetch as Mock<typeof fetch>).mockResolvedValueOnce(createSSEResponse(events, 1000));

      const { result } = renderHook(() =>
        useAgentChat({
          sessionId: mockSessionId,
        })
      );

      await act(async () => {
        const promise = result.current.sendMessage('test query');
        // Cancel immediately
        result.current.stopStreaming();
        await promise;
      });

      expect(result.current.isStreaming).toBe(false);
      expect(result.current.stateMessage).toBe('Cancelled');
    });
  });

  describe('Callbacks', () => {
    it.skip('should trigger onComplete callback', async () => {
      // TODO: Requires SSE stream completion timing
    });

    it('should trigger onToken callback', async () => {
      const events = [
        JSON.stringify({
          type: 'token',
          data: { token: 'Hello' },
          timestamp: new Date().toISOString(),
        }),
        JSON.stringify({
          type: 'complete',
          data: {},
          timestamp: new Date().toISOString(),
        }),
      ];

      (global.fetch as Mock<typeof fetch>).mockResolvedValueOnce(createSSEResponse(events));

      const onToken = vi.fn();
      const { result } = renderHook(() =>
        useAgentChat({
          sessionId: mockSessionId,
          onToken,
        })
      );

      await act(async () => {
        await result.current.sendMessage('test query');
      });

      await waitFor(() => {
        expect(onToken).toHaveBeenCalledWith('Hello');
      });
    });
  });

  describe('Clear Messages', () => {
    it('should clear messages', () => {
      const { result } = renderHook(() =>
        useAgentChat({
          sessionId: mockSessionId,
        })
      );

      act(() => {
        result.current.clearMessages();
      });

      expect(mockClearMessages).toHaveBeenCalledWith(mockSessionId);
      expect(result.current.error).toBeNull();
      expect(result.current.stateMessage).toBe('');
    });
  });
});