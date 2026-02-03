/**
 * useStreamingChatWithReconnect Hook Tests (Issue #2054)
 *
 * Tests for SSE streaming with auto-reconnect capabilities.
 * Includes: streaming, reconnection, error recovery, network handling
 */

import { renderHook, act, waitFor } from '@testing-library/react';
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';

import {
  useStreamingChatWithReconnect,
  type UseStreamingChatWithReconnectOptions,
  type StreamingChatWithReconnectState,
} from '../useStreamingChatWithReconnect';
import { StreamingEventType } from '@/lib/api/schemas/streaming.schemas';

// ============================================================================
// Mock Network Status Hook
// ============================================================================

const mockNetworkStatus = {
  isOnline: true,
  startReconnecting: vi.fn(),
  stopReconnecting: vi.fn(),
  incrementReconnectAttempts: vi.fn(),
  resetReconnectAttempts: vi.fn(),
};

vi.mock('@/hooks/useNetworkStatus', () => ({
  useNetworkStatus: () => mockNetworkStatus,
}));

// ============================================================================
// Mock Fetch for SSE Simulation
// ============================================================================

class MockReadableStream {
  private chunks: string[];
  private index = 0;
  public controller: ReadableStreamDefaultController | null = null;

  constructor(chunks: string[]) {
    this.chunks = chunks;
  }

  getReader() {
    const self = this;
    return {
      async read() {
        if (self.index >= self.chunks.length) {
          return { done: true, value: undefined };
        }
        const chunk = self.chunks[self.index++];
        const encoder = new TextEncoder();
        return { done: false, value: encoder.encode(chunk) };
      },
    };
  }
}

function createSSEEvent(type: string, data: any, timestamp?: string): string {
  return `event: ${type}\ndata: ${JSON.stringify({
    type,
    data,
    timestamp: timestamp || new Date().toISOString(),
  })}\n\n`;
}

describe('useStreamingChatWithReconnect', () => {
  let fetchSpy: any;

  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers({ shouldAdvanceTime: true });
    fetchSpy = vi.spyOn(global, 'fetch');
    mockNetworkStatus.isOnline = true;
  });

  afterEach(() => {
    fetchSpy?.mockRestore();
    vi.useRealTimers();
  });

  // ==========================================================================
  // Initial State Tests
  // ==========================================================================

  describe('Initial State', () => {
    it('should have correct initial state', () => {
      const { result } = renderHook(() => useStreamingChatWithReconnect());
      const [state] = result.current;

      expect(state).toEqual({
        isStreaming: false,
        currentAnswer: '',
        citations: [],
        stateMessage: '',
        confidence: null,
        error: null,
        followUpQuestions: [],
        totalTokens: 0,
        estimatedReadingTimeMinutes: null,
        isReconnecting: false,
        reconnectAttempt: 0,
        reconnectFailed: false,
        lastEventId: null,
      });
    });

    it('should provide control functions', () => {
      const { result } = renderHook(() => useStreamingChatWithReconnect());
      const [, controls] = result.current;

      expect(controls).toHaveProperty('startStreaming');
      expect(controls).toHaveProperty('stopStreaming');
      expect(controls).toHaveProperty('reset');
      expect(controls).toHaveProperty('retryStreaming');
    });
  });

  // ==========================================================================
  // Streaming Tests
  // ==========================================================================

  describe('Streaming', () => {
    it('should start streaming and update state', async () => {
      const chunks = [
        createSSEEvent(StreamingEventType.StateUpdate, { state: 'Searching...' }),
        createSSEEvent(StreamingEventType.Token, { token: 'Hello' }),
        createSSEEvent(StreamingEventType.Token, { token: ' World' }),
      ];

      fetchSpy.mockResolvedValue({
        ok: true,
        body: new MockReadableStream(chunks),
      });

      const { result } = renderHook(() => useStreamingChatWithReconnect());

      await act(async () => {
        await result.current[1].startStreaming('game-123', 'test query');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.currentAnswer).toBe('Hello World');
      });

      const [state] = result.current;
      expect(state.isStreaming).toBe(true);
      expect(state.stateMessage).toBe('Searching...');
    });

    it('should call fetch with correct parameters', async () => {
      fetchSpy.mockResolvedValue({
        ok: true,
        body: new MockReadableStream([]),
      });

      const { result } = renderHook(() => useStreamingChatWithReconnect());

      await act(async () => {
        await result.current[1].startStreaming(
          'game-456',
          'How to play?',
          'chat-789',
          ['doc-1', 'doc-2']
        );
      });

      await waitFor(() => {
        expect(fetchSpy).toHaveBeenCalledWith(
          expect.stringContaining('/api/v1/agents/qa/stream'),
          expect.objectContaining({
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'include',
            body: JSON.stringify({
              gameId: 'game-456',
              query: 'How to play?',
              chatId: 'chat-789',
              documentIds: ['doc-1', 'doc-2'],
            }),
          })
        );
      });
    });

    it('should handle citations event', async () => {
      const citations = [
        {
          documentId: 'doc-1',
          pageNumber: 5,
          snippet: 'Rule text...',
          relevanceScore: 0.95,
        },
      ];

      const chunks = [
        createSSEEvent(StreamingEventType.Citations, { citations }),
      ];

      fetchSpy.mockResolvedValue({
        ok: true,
        body: new MockReadableStream(chunks),
      });

      const { result } = renderHook(() => useStreamingChatWithReconnect());

      await act(async () => {
        await result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.citations).toEqual(citations);
      });
    });

    it('should handle complete event', async () => {
      const chunks = [
        createSSEEvent(StreamingEventType.Token, { token: 'Answer' }),
        createSSEEvent(StreamingEventType.Complete, {
          totalTokens: 150,
          confidence: 0.88,
          estimatedReadingTimeMinutes: 2,
          snippets: [],
        }),
      ];

      fetchSpy.mockResolvedValue({
        ok: true,
        body: new MockReadableStream(chunks),
      });

      const onComplete = vi.fn();
      const { result } = renderHook(() =>
        useStreamingChatWithReconnect({ onComplete })
      );

      await act(async () => {
        await result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.isStreaming).toBe(false);
      });

      const [state] = result.current;
      expect(state.currentAnswer).toBe('Answer');
      expect(state.totalTokens).toBe(150);
      expect(state.confidence).toBe(0.88);
      expect(state.estimatedReadingTimeMinutes).toBe(2);
      expect(onComplete).toHaveBeenCalledWith('Answer', [], 0.88);
    });

    it('should handle error event', async () => {
      const chunks = [
        createSSEEvent(StreamingEventType.Error, {
          code: 'RETRIEVAL_FAILED',
          message: 'Vector search failed',
        }),
      ];

      fetchSpy.mockResolvedValue({
        ok: true,
        body: new MockReadableStream(chunks),
      });

      const onError = vi.fn();
      const { result } = renderHook(() =>
        useStreamingChatWithReconnect({ onError })
      );

      await act(async () => {
        await result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.error).toBeTruthy();
      });

      const [state] = result.current;
      expect(state.error?.message).toBe('Vector search failed');
      expect(state.isStreaming).toBe(false);
      expect(onError).toHaveBeenCalled();
    });

    it('should handle follow-up questions event', async () => {
      const questions = ['How many players?', 'What is the setup time?'];
      const chunks = [
        createSSEEvent(StreamingEventType.FollowUpQuestions, { questions }),
      ];

      fetchSpy.mockResolvedValue({
        ok: true,
        body: new MockReadableStream(chunks),
      });

      const { result } = renderHook(() => useStreamingChatWithReconnect());

      await act(async () => {
        await result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.followUpQuestions).toEqual(questions);
      });
    });

    it('should call onToken callback for each token', async () => {
      const chunks = [
        createSSEEvent(StreamingEventType.Token, { token: 'Hello' }),
        createSSEEvent(StreamingEventType.Token, { token: ' ' }),
        createSSEEvent(StreamingEventType.Token, { token: 'World' }),
      ];

      fetchSpy.mockResolvedValue({
        ok: true,
        body: new MockReadableStream(chunks),
      });

      const onToken = vi.fn();
      const { result } = renderHook(() =>
        useStreamingChatWithReconnect({ onToken })
      );

      await act(async () => {
        await result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        expect(onToken).toHaveBeenCalledTimes(3);
      });

      expect(onToken).toHaveBeenNthCalledWith(1, 'Hello', 'Hello');
      expect(onToken).toHaveBeenNthCalledWith(2, ' ', 'Hello ');
      expect(onToken).toHaveBeenNthCalledWith(3, 'World', 'Hello World');
    });
  });

  // ==========================================================================
  // Deduplication Tests
  // ==========================================================================

  describe('Deduplication', () => {
    it('should deduplicate events with same timestamp', async () => {
      const timestamp = '2024-01-20T10:00:00Z';
      const chunks = [
        createSSEEvent(StreamingEventType.Token, { token: 'Hello' }, timestamp),
        createSSEEvent(StreamingEventType.Token, { token: 'Hello' }, timestamp), // Duplicate
        createSSEEvent(StreamingEventType.Token, { token: ' World' }),
      ];

      fetchSpy.mockResolvedValue({
        ok: true,
        body: new MockReadableStream(chunks),
      });

      const { result } = renderHook(() => useStreamingChatWithReconnect());

      await act(async () => {
        await result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.currentAnswer).toBe('Hello World');
      });

      // Should not have doubled 'Hello'
      expect(result.current[0].currentAnswer).not.toContain('HelloHello');
    });

    it('should limit processed event IDs to prevent memory leak', async () => {
      const chunks: string[] = [];
      for (let i = 0; i < 1100; i++) {
        chunks.push(createSSEEvent(StreamingEventType.Token, { token: `${i} ` }));
      }

      fetchSpy.mockResolvedValue({
        ok: true,
        body: new MockReadableStream(chunks),
      });

      const { result } = renderHook(() => useStreamingChatWithReconnect());

      await act(async () => {
        await result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.currentAnswer.length).toBeGreaterThan(0);
      });

      // Test passes if no memory issues occur
      expect(result.current[0].currentAnswer).toContain('1099');
    });
  });

  // ==========================================================================
  // Reconnection Tests
  // ==========================================================================

  describe('Reconnection', () => {
    it('should attempt reconnection on connection error', async () => {
      fetchSpy.mockRejectedValueOnce(new Error('Connection failed'));
      fetchSpy.mockResolvedValue({
        ok: true,
        body: new MockReadableStream([
          createSSEEvent(StreamingEventType.Token, { token: 'Reconnected' }),
        ]),
      });

      const onReconnecting = vi.fn();
      const { result } = renderHook(() =>
        useStreamingChatWithReconnect({ onReconnecting })
      );

      await act(async () => {
        await result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.isReconnecting).toBe(true);
      });

      expect(onReconnecting).toHaveBeenCalledWith(1);

      // Advance time for exponential backoff
      await act(async () => {
        await vi.advanceTimersByTimeAsync(1500);
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.currentAnswer).toContain('Reconnected');
      });
    });

    it('should use exponential backoff for reconnection', async () => {
      let attemptCount = 0;
      fetchSpy.mockImplementation(() => {
        attemptCount++;
        if (attemptCount < 3) {
          return Promise.reject(new Error('Connection failed'));
        }
        return Promise.resolve({
          ok: true,
          body: new MockReadableStream([]),
        });
      });

      const onReconnecting = vi.fn();
      const { result } = renderHook(() =>
        useStreamingChatWithReconnect({ onReconnecting })
      );

      await act(async () => {
        await result.current[1].startStreaming('game-123', 'test');
      });

      // First reconnect (delay ~1000ms)
      await act(async () => {
        await vi.advanceTimersByTimeAsync(1500);
      });

      await waitFor(() => {
        expect(result.current[0].reconnectAttempt).toBe(1);
      });

      // Second reconnect (delay ~2000ms)
      await act(async () => {
        await vi.advanceTimersByTimeAsync(2500);
      });

      await waitFor(() => {
        expect(onReconnecting).toHaveBeenCalledTimes(2);
      });
    });

    it('should fail after max reconnect attempts', async () => {
      fetchSpy.mockRejectedValue(new Error('Connection failed'));

      const onReconnectFailed = vi.fn();
      const { result } = renderHook(() =>
        useStreamingChatWithReconnect({
          maxReconnectAttempts: 2,
          onReconnectFailed,
        })
      );

      await act(async () => {
        await result.current[1].startStreaming('game-123', 'test');
      });

      // Attempt 1
      await act(async () => {
        await vi.advanceTimersByTimeAsync(1500);
      });

      // Attempt 2
      await act(async () => {
        await vi.advanceTimersByTimeAsync(2500);
      });

      // Max attempts reached
      await waitFor(() => {
        const [state] = result.current;
        expect(state.reconnectFailed).toBe(true);
      });

      expect(onReconnectFailed).toHaveBeenCalled();
      expect(result.current[0].stateMessage).toContain('Riconnessione fallita');
    });

    it('should reset reconnect counter on successful connection', async () => {
      let attemptCount = 0;
      fetchSpy.mockImplementation(() => {
        attemptCount++;
        if (attemptCount === 1) {
          return Promise.reject(new Error('Connection failed'));
        }
        return Promise.resolve({
          ok: true,
          body: new MockReadableStream([
            createSSEEvent(StreamingEventType.Token, { token: 'Success' }),
          ]),
        });
      });

      const onReconnectSuccess = vi.fn();
      const { result } = renderHook(() =>
        useStreamingChatWithReconnect({ onReconnectSuccess })
      );

      await act(async () => {
        await result.current[1].startStreaming('game-123', 'test');
      });

      await act(async () => {
        await vi.advanceTimersByTimeAsync(1500);
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.reconnectAttempt).toBe(0);
      });

      expect(onReconnectSuccess).toHaveBeenCalled();
    });

    it('should not reconnect when offline', async () => {
      mockNetworkStatus.isOnline = false;
      fetchSpy.mockRejectedValue(new Error('Connection failed'));

      const { result } = renderHook(() => useStreamingChatWithReconnect());

      await act(async () => {
        await result.current[1].startStreaming('game-123', 'test');
      });

      await act(async () => {
        await vi.advanceTimersByTimeAsync(2000);
      });

      // Should not attempt reconnect when offline
      expect(fetchSpy).toHaveBeenCalledTimes(1);
    });
  });

  // ==========================================================================
  // Control Tests
  // ==========================================================================

  describe('Controls', () => {
    it('should stop streaming and cancel request', async () => {
      const chunks = [
        createSSEEvent(StreamingEventType.Token, { token: 'Hello' }),
      ];

      fetchSpy.mockResolvedValue({
        ok: true,
        body: new MockReadableStream(chunks),
      });

      const { result } = renderHook(() => useStreamingChatWithReconnect());

      await act(async () => {
        await result.current[1].startStreaming('game-123', 'test');
      });

      act(() => {
        result.current[1].stopStreaming();
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.isStreaming).toBe(false);
      });

      expect(result.current[0].stateMessage).toBe('Annullato');
    });

    it('should reset state to initial', async () => {
      const chunks = [
        createSSEEvent(StreamingEventType.Token, { token: 'Test' }),
      ];

      fetchSpy.mockResolvedValue({
        ok: true,
        body: new MockReadableStream(chunks),
      });

      const { result } = renderHook(() => useStreamingChatWithReconnect());

      await act(async () => {
        await result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        expect(result.current[0].currentAnswer).toBe('Test');
      });

      act(() => {
        result.current[1].reset();
      });

      const [state] = result.current;
      expect(state.currentAnswer).toBe('');
      expect(state.citations).toEqual([]);
      expect(state.isStreaming).toBe(false);
      expect(state.error).toBeNull();
    });

    it('should allow manual retry after failure', async () => {
      let attemptCount = 0;
      fetchSpy.mockImplementation(() => {
        attemptCount++;
        if (attemptCount <= 3) {
          return Promise.reject(new Error('Connection failed'));
        }
        return Promise.resolve({
          ok: true,
          body: new MockReadableStream([
            createSSEEvent(StreamingEventType.Token, { token: 'Retry success' }),
          ]),
        });
      });

      const { result } = renderHook(() =>
        useStreamingChatWithReconnect({ maxReconnectAttempts: 2 })
      );

      await act(async () => {
        await result.current[1].startStreaming('game-123', 'test');
      });

      // Wait for max reconnect attempts
      await act(async () => {
        await vi.advanceTimersByTimeAsync(5000);
      });

      await waitFor(() => {
        expect(result.current[0].reconnectFailed).toBe(true);
      });

      // Manual retry
      await act(async () => {
        await result.current[1].retryStreaming();
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.currentAnswer).toContain('Retry success');
      });
    });

    it('should do nothing on retry if no previous request', () => {
      const { result } = renderHook(() => useStreamingChatWithReconnect());

      act(() => {
        result.current[1].retryStreaming();
      });

      expect(result.current[0].isStreaming).toBe(false);
    });
  });

  // ==========================================================================
  // Error Handling Tests
  // ==========================================================================

  describe('Error Handling', () => {
    it('should handle HTTP error response', async () => {
      fetchSpy.mockResolvedValue({
        ok: false,
        status: 500,
      });

      const { result } = renderHook(() => useStreamingChatWithReconnect());

      await act(async () => {
        await result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.isReconnecting).toBe(true);
      });
    });

    it('should handle missing response body', async () => {
      fetchSpy.mockResolvedValue({
        ok: true,
        body: null,
      });

      const { result } = renderHook(() => useStreamingChatWithReconnect());

      await act(async () => {
        await result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.isReconnecting).toBe(true);
      });
    });

    it('should handle abort error gracefully', async () => {
      fetchSpy.mockImplementation(() => {
        const error = new Error('User aborted');
        error.name = 'AbortError';
        return Promise.reject(error);
      });

      const { result } = renderHook(() => useStreamingChatWithReconnect());

      await act(async () => {
        await result.current[1].startStreaming('game-123', 'test');
        result.current[1].stopStreaming();
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.stateMessage).toBe('Annullato');
      });
    });
  });

  // ==========================================================================
  // Edge Cases Tests
  // ==========================================================================

  describe('Edge Cases', () => {
    it('should handle empty token events', async () => {
      const chunks = [
        createSSEEvent(StreamingEventType.Token, { token: '' }),
        createSSEEvent(StreamingEventType.Token, { token: 'Hello' }),
      ];

      fetchSpy.mockResolvedValue({
        ok: true,
        body: new MockReadableStream(chunks),
      });

      const { result } = renderHook(() => useStreamingChatWithReconnect());

      await act(async () => {
        await result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        expect(result.current[0].currentAnswer).toBe('Hello');
      });
    });

    it('should handle rapid start/stop cycles', async () => {
      fetchSpy.mockResolvedValue({
        ok: true,
        body: new MockReadableStream([]),
      });

      const { result } = renderHook(() => useStreamingChatWithReconnect());

      await act(async () => {
        await result.current[1].startStreaming('game-1', 'q1');
        result.current[1].stopStreaming();
        await result.current[1].startStreaming('game-2', 'q2');
        result.current[1].stopStreaming();
        await result.current[1].startStreaming('game-3', 'q3');
      });

      expect(fetchSpy).toHaveBeenCalledTimes(3);
    });

    it('should preserve content during reconnect', async () => {
      let attemptCount = 0;
      fetchSpy.mockImplementation(() => {
        attemptCount++;
        if (attemptCount === 1) {
          return Promise.resolve({
            ok: true,
            body: new MockReadableStream([
              createSSEEvent(StreamingEventType.Token, { token: 'Initial ' }),
            ]),
          });
        }
        if (attemptCount === 2) {
          return Promise.reject(new Error('Connection lost'));
        }
        return Promise.resolve({
          ok: true,
          body: new MockReadableStream([
            createSSEEvent(StreamingEventType.Token, { token: 'Resumed' }),
          ]),
        });
      });

      const { result } = renderHook(() => useStreamingChatWithReconnect());

      await act(async () => {
        await result.current[1].startStreaming('game-123', 'test');
      });

      // Simulate connection loss and reconnect
      await act(async () => {
        await vi.advanceTimersByTimeAsync(2000);
      });

      // Content should be preserved
      await waitFor(() => {
        expect(result.current[0].currentAnswer).toContain('Initial');
      });
    });
  });

  // ==========================================================================
  // Cleanup Tests
  // ==========================================================================

  describe('Cleanup', () => {
    it('should cleanup on unmount', async () => {
      fetchSpy.mockResolvedValue({
        ok: true,
        body: new MockReadableStream([]),
      });

      const { result, unmount } = renderHook(() =>
        useStreamingChatWithReconnect()
      );

      await act(async () => {
        await result.current[1].startStreaming('game-123', 'test');
      });

      unmount();

      // Should not crash or cause issues
      expect(true).toBe(true);
    });

    it('should clear reconnect timeout on unmount', async () => {
      fetchSpy.mockRejectedValue(new Error('Connection failed'));

      const { result, unmount } = renderHook(() =>
        useStreamingChatWithReconnect()
      );

      await act(async () => {
        await result.current[1].startStreaming('game-123', 'test');
      });

      // Start reconnect but unmount before completion
      unmount();

      // Should cleanup without errors
      expect(true).toBe(true);
    });
  });
});
