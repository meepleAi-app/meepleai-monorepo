/**
 * useStreamingChat Hook Tests (Issue #1007)
 *
 * Comprehensive test suite covering all E2E test scenarios:
 * - Token accumulation
 * - State updates
 * - Citations handling
 * - Error scenarios (network, HTTP, auth)
 * - Cancellation
 * - Callbacks
 */

import { describe, it, expect, jest, beforeEach, afterEach } from '@jest/globals';
import { renderHook, act, waitFor } from '@testing-library/react';
import { useStreamingChat } from '../useStreamingChat';
import { StreamingEventType } from '@/lib/api/schemas/streaming.schemas';

// Mock fetch globally
global.fetch = jest.fn() as jest.MockedFunction<typeof fetch>;

// Helper to create SSE response
function createSSEResponse(events: string[]): Response {
  const sseData = events.map((e) => `data: ${e}\n\n`).join('');
  const encoder = new TextEncoder();
  const data = encoder.encode(sseData);

  const stream = new ReadableStream({
    start(controller) {
      controller.enqueue(data);
      controller.close();
    },
  });

  return new Response(stream, {
    status: 200,
    headers: {
      'Content-Type': 'text/event-stream',
    },
  });
}

describe('useStreamingChat', () => {
  beforeEach(() => {
    (global.fetch as jest.MockedFunction<typeof fetch>).mockClear();
  });

  afterEach(() => {
    jest.clearAllMocks();
  });

  describe('Initial State', () => {
    it('should initialize with correct default state', () => {
      const { result } = renderHook(() => useStreamingChat());
      const [state] = result.current;

      expect(state.isStreaming).toBe(false);
      expect(state.currentAnswer).toBe('');
      expect(state.citations).toEqual([]);
      expect(state.stateMessage).toBe('');
      expect(state.confidence).toBeNull();
      expect(state.error).toBeNull();
      expect(state.followUpQuestions).toEqual([]);
      expect(state.totalTokens).toBe(0);
      expect(state.estimatedReadingTimeMinutes).toBeNull();
    });

    it('should provide control functions', () => {
      const { result } = renderHook(() => useStreamingChat());
      const [, controls] = result.current;

      expect(controls.startStreaming).toBeInstanceOf(Function);
      expect(controls.stopStreaming).toBeInstanceOf(Function);
      expect(controls.reset).toBeInstanceOf(Function);
    });
  });

  describe('Token Accumulation', () => {
    it('should accumulate tokens progressively', async () => {
      const events = [
        JSON.stringify({
          type: 'token',
          data: { token: 'Hello' },
          timestamp: '2025-01-15T10:00:00Z',
        }),
        JSON.stringify({
          type: 'token',
          data: { token: ' ' },
          timestamp: '2025-01-15T10:00:01Z',
        }),
        JSON.stringify({
          type: 'token',
          data: { token: 'World' },
          timestamp: '2025-01-15T10:00:02Z',
        }),
        JSON.stringify({
          type: 'complete',
          data: { totalTokens: 3, confidence: 0.95 },
          timestamp: '2025-01-15T10:00:03Z',
        }),
      ];

      (global.fetch as jest.MockedFunction<typeof fetch>).mockResolvedValueOnce(
        createSSEResponse(events)
      );

      const { result } = renderHook(() => useStreamingChat());

      await act(async () => {
        await result.current[1].startStreaming('game-123', 'test query');
      });

      await waitFor(() => {
        expect(result.current[0].currentAnswer).toBe('Hello World');
      });

      expect(result.current[0].totalTokens).toBe(3);
      expect(result.current[0].confidence).toBe(0.95);
      expect(result.current[0].isStreaming).toBe(false);
    });

    it('should call onToken callback for each token', async () => {
      const onToken = jest.fn();
      const events = [
        JSON.stringify({
          type: 'token',
          data: { token: 'A' },
          timestamp: '2025-01-15T10:00:00Z',
        }),
        JSON.stringify({
          type: 'token',
          data: { token: 'B' },
          timestamp: '2025-01-15T10:00:01Z',
        }),
        JSON.stringify({
          type: 'complete',
          data: { totalTokens: 2, confidence: 0.9 },
          timestamp: '2025-01-15T10:00:02Z',
        }),
      ];

      (global.fetch as jest.MockedFunction<typeof fetch>).mockResolvedValueOnce(
        createSSEResponse(events)
      );

      const { result } = renderHook(() => useStreamingChat({ onToken }));

      await act(async () => {
        await result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        expect(onToken).toHaveBeenCalledTimes(2);
      });

      expect(onToken).toHaveBeenNthCalledWith(1, 'A', 'A');
      expect(onToken).toHaveBeenNthCalledWith(2, 'B', 'AB');
    });
  });

  describe('State Updates', () => {
    it('should update state message progressively', async () => {
      const events = [
        JSON.stringify({
          type: 'stateUpdate',
          data: { state: 'Generating embeddings...' },
          timestamp: '2025-01-15T10:00:00Z',
        }),
        JSON.stringify({
          type: 'stateUpdate',
          data: { state: 'Searching vector database...' },
          timestamp: '2025-01-15T10:00:01Z',
        }),
        JSON.stringify({
          type: 'complete',
          data: { totalTokens: 0, confidence: null },
          timestamp: '2025-01-15T10:00:02Z',
        }),
      ];

      (global.fetch as jest.MockedFunction<typeof fetch>).mockResolvedValueOnce(
        createSSEResponse(events)
      );

      const { result } = renderHook(() => useStreamingChat());

      await act(async () => {
        await result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        expect(result.current[0].stateMessage).toBe('Complete');
      });
    });

    it('should call onStateUpdate callback', async () => {
      const onStateUpdate = jest.fn();
      const events = [
        JSON.stringify({
          type: 'stateUpdate',
          data: { state: 'Processing...' },
          timestamp: '2025-01-15T10:00:00Z',
        }),
        JSON.stringify({
          type: 'complete',
          data: { totalTokens: 0, confidence: null },
          timestamp: '2025-01-15T10:00:01Z',
        }),
      ];

      (global.fetch as jest.MockedFunction<typeof fetch>).mockResolvedValueOnce(
        createSSEResponse(events)
      );

      const { result } = renderHook(() => useStreamingChat({ onStateUpdate }));

      await act(async () => {
        await result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        expect(onStateUpdate).toHaveBeenCalledWith('Processing...');
      });
    });
  });

  describe('Citations Handling', () => {
    it('should store citations when received', async () => {
      const events = [
        JSON.stringify({
          type: 'citations',
          data: {
            citations: [
              { source: 'rules.pdf', page: 1, text: 'Game rules', score: 0.95 },
              { source: 'manual.pdf', page: 5, text: 'Setup guide', score: 0.88 },
            ],
          },
          timestamp: '2025-01-15T10:00:00Z',
        }),
        JSON.stringify({
          type: 'complete',
          data: { totalTokens: 0, confidence: null },
          timestamp: '2025-01-15T10:00:01Z',
        }),
      ];

      (global.fetch as jest.MockedFunction<typeof fetch>).mockResolvedValueOnce(
        createSSEResponse(events)
      );

      const { result } = renderHook(() => useStreamingChat());

      await act(async () => {
        await result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        expect(result.current[0].citations).toHaveLength(2);
      });

      expect(result.current[0].citations[0].source).toBe('rules.pdf');
      expect(result.current[0].citations[1].source).toBe('manual.pdf');
    });

    it('should use snippets from complete event if provided', async () => {
      const events = [
        JSON.stringify({
          type: 'complete',
          data: {
            totalTokens: 5,
            confidence: 0.9,
            snippets: [{ source: 'final.pdf', page: 10, text: 'Final citation', score: 0.99 }],
          },
          timestamp: '2025-01-15T10:00:00Z',
        }),
      ];

      (global.fetch as jest.MockedFunction<typeof fetch>).mockResolvedValueOnce(
        createSSEResponse(events)
      );

      const { result } = renderHook(() => useStreamingChat());

      await act(async () => {
        await result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        expect(result.current[0].citations).toHaveLength(1);
      });

      expect(result.current[0].citations[0].source).toBe('final.pdf');
    });

    it('should fallback to snippets field when citations is empty', async () => {
      // Test for bug fix: empty arrays are truthy, so we need to check length
      const events = [
        JSON.stringify({
          type: 'citations',
          data: {
            citations: [], // Empty array (truthy but no data)
            snippets: [
              { source: 'guide.pdf', page: 3, text: 'Setup instructions', score: 0.92 },
              { source: 'faq.pdf', page: 7, text: 'Common questions', score: 0.87 },
            ],
          },
          timestamp: '2025-01-15T10:00:00Z',
        }),
        JSON.stringify({
          type: 'complete',
          data: { totalTokens: 0, confidence: null },
          timestamp: '2025-01-15T10:00:01Z',
        }),
      ];

      (global.fetch as jest.MockedFunction<typeof fetch>).mockResolvedValueOnce(
        createSSEResponse(events)
      );

      const { result } = renderHook(() => useStreamingChat());

      await act(async () => {
        await result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        expect(result.current[0].citations).toHaveLength(2);
      });

      expect(result.current[0].citations[0].source).toBe('guide.pdf');
      expect(result.current[0].citations[1].source).toBe('faq.pdf');
    });
  });

  describe('Error Handling', () => {
    it('should handle SSE error event', async () => {
      const onError = jest.fn();
      const events = [
        JSON.stringify({
          type: 'error',
          data: { message: 'Something went wrong', code: 'INTERNAL_ERROR' },
          timestamp: '2025-01-15T10:00:00Z',
        }),
      ];

      (global.fetch as jest.MockedFunction<typeof fetch>).mockResolvedValueOnce(
        createSSEResponse(events)
      );

      const { result } = renderHook(() => useStreamingChat({ onError }));

      await act(async () => {
        await result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        expect(result.current[0].error).not.toBeNull();
      });

      expect(result.current[0].error?.message).toBe('Something went wrong');
      expect(result.current[0].error?.name).toBe('INTERNAL_ERROR');
      expect(result.current[0].isStreaming).toBe(false);
      expect(onError).toHaveBeenCalled();
    });

    it('should handle HTTP errors (non-200 status)', async () => {
      const onError = jest.fn();

      (global.fetch as jest.MockedFunction<typeof fetch>).mockResolvedValueOnce(
        new Response(null, { status: 500, statusText: 'Internal Server Error' })
      );

      const { result } = renderHook(() => useStreamingChat({ onError }));

      await act(async () => {
        await result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        expect(result.current[0].error).not.toBeNull();
      });

      expect(result.current[0].error?.message).toContain('500');
      expect(onError).toHaveBeenCalled();
    });

    it('should handle network errors', async () => {
      const onError = jest.fn();

      (global.fetch as jest.MockedFunction<typeof fetch>).mockRejectedValueOnce(
        new Error('Network error')
      );

      const { result } = renderHook(() => useStreamingChat({ onError }));

      await act(async () => {
        await result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        expect(result.current[0].error).not.toBeNull();
      });

      expect(result.current[0].error?.message).toBe('Network error');
      expect(onError).toHaveBeenCalled();
    });

    it('should handle 401 unauthorized', async () => {
      const onError = jest.fn();

      (global.fetch as jest.MockedFunction<typeof fetch>).mockResolvedValueOnce(
        new Response(null, { status: 401, statusText: 'Unauthorized' })
      );

      const { result } = renderHook(() => useStreamingChat({ onError }));

      await act(async () => {
        await result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        expect(result.current[0].error).not.toBeNull();
      });

      expect(result.current[0].error?.message).toContain('401');
    });
  });

  describe('Cancellation', () => {
    it('should stop streaming when stopStreaming is called', async () => {
      let capturedAbortSignal: AbortSignal | null = null;

      (global.fetch as jest.MockedFunction<typeof fetch>).mockImplementationOnce((url, options) => {
        // Capture the abort signal from the request
        capturedAbortSignal = (options as RequestInit).signal as AbortSignal;

        // Create a promise that will be rejected when aborted
        const promise = new Promise<Response>((resolve, reject) => {
          const checkAbort = () => {
            if (capturedAbortSignal?.aborted) {
              reject(new DOMException('Aborted', 'AbortError'));
              return true;
            }
            return false;
          };

          // Check immediately
          if (checkAbort()) return;

          // Listen for abort event
          capturedAbortSignal?.addEventListener('abort', () => {
            reject(new DOMException('Aborted', 'AbortError'));
          });

          // Simulate slow response
          setTimeout(() => {
            if (!checkAbort()) {
              const events = [
                JSON.stringify({
                  type: 'token',
                  data: { token: 'Start' },
                  timestamp: '2025-01-15T10:00:00Z',
                }),
              ];
              resolve(createSSEResponse(events));
            }
          }, 200);
        });

        return promise;
      });

      const { result } = renderHook(() => useStreamingChat());

      // Start streaming
      act(() => {
        result.current[1].startStreaming('game-123', 'test');
      });

      // Wait for streaming to actually start
      await waitFor(() => {
        expect(result.current[0].isStreaming).toBe(true);
      });

      // Stop streaming
      act(() => {
        result.current[1].stopStreaming();
      });

      // Wait for cancellation to complete
      await waitFor(() => {
        expect(result.current[0].stateMessage).toBe('Cancelled');
      });

      expect(result.current[0].isStreaming).toBe(false);
    });
  });

  describe('Completion Callback', () => {
    it('should call onComplete when streaming finishes', async () => {
      const onComplete = jest.fn();
      const events = [
        JSON.stringify({
          type: 'token',
          data: { token: 'Answer' },
          timestamp: '2025-01-15T10:00:00Z',
        }),
        JSON.stringify({
          type: 'complete',
          data: { totalTokens: 1, confidence: 0.88 },
          timestamp: '2025-01-15T10:00:01Z',
        }),
      ];

      (global.fetch as jest.MockedFunction<typeof fetch>).mockResolvedValueOnce(
        createSSEResponse(events)
      );

      const { result } = renderHook(() => useStreamingChat({ onComplete }));

      await act(async () => {
        await result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        expect(onComplete).toHaveBeenCalled();
      });

      expect(onComplete).toHaveBeenCalledWith('Answer', [], 0.88);
    });
  });

  describe('State Reset', () => {
    it('should reset state when reset() is called', async () => {
      const events = [
        JSON.stringify({
          type: 'token',
          data: { token: 'Test' },
          timestamp: '2025-01-15T10:00:00Z',
        }),
        JSON.stringify({
          type: 'complete',
          data: { totalTokens: 1, confidence: 0.9 },
          timestamp: '2025-01-15T10:00:01Z',
        }),
      ];

      (global.fetch as jest.MockedFunction<typeof fetch>).mockResolvedValueOnce(
        createSSEResponse(events)
      );

      const { result } = renderHook(() => useStreamingChat());

      await act(async () => {
        await result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        expect(result.current[0].currentAnswer).toBe('Test');
      });

      act(() => {
        result.current[1].reset();
      });

      expect(result.current[0].currentAnswer).toBe('');
      expect(result.current[0].totalTokens).toBe(0);
      expect(result.current[0].confidence).toBeNull();
    });
  });

  describe('Follow-up Questions', () => {
    it('should store follow-up questions when received', async () => {
      const events = [
        JSON.stringify({
          type: 'followUpQuestions',
          data: { questions: ['Question 1?', 'Question 2?', 'Question 3?'] },
          timestamp: '2025-01-15T10:00:00Z',
        }),
        JSON.stringify({
          type: 'complete',
          data: { totalTokens: 0, confidence: null },
          timestamp: '2025-01-15T10:00:01Z',
        }),
      ];

      (global.fetch as jest.MockedFunction<typeof fetch>).mockResolvedValueOnce(
        createSSEResponse(events)
      );

      const { result } = renderHook(() => useStreamingChat());

      await act(async () => {
        await result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        expect(result.current[0].followUpQuestions).toHaveLength(3);
      });

      expect(result.current[0].followUpQuestions[0]).toBe('Question 1?');
    });
  });

  describe('API Integration', () => {
    it('should call correct endpoint with credentials', async () => {
      const events = [
        JSON.stringify({
          type: 'complete',
          data: { totalTokens: 0, confidence: null },
          timestamp: '2025-01-15T10:00:00Z',
        }),
      ];

      (global.fetch as jest.MockedFunction<typeof fetch>).mockResolvedValueOnce(
        createSSEResponse(events)
      );

      const { result } = renderHook(() =>
        useStreamingChat({ apiBaseUrl: 'http://localhost:8080' })
      );

      await act(async () => {
        await result.current[1].startStreaming('game-456', 'test query', 'chat-789');
      });

      await waitFor(() => {
        expect(global.fetch).toHaveBeenCalled();
      });

      expect(global.fetch).toHaveBeenCalledWith(
        'http://localhost:8080/api/v1/agents/qa/stream',
        expect.objectContaining({
          method: 'POST',
          credentials: 'include',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ gameId: 'game-456', query: 'test query', chatId: 'chat-789' }),
        })
      );
    });
  });
});
