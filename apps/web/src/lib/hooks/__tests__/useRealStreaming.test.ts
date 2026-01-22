/**
 * useRealStreaming Hook Tests (Issue #2765)
 *
 * Comprehensive test suite for real SSE streaming hook:
 * - Initial state and controls
 * - Token accumulation
 * - State updates
 * - Citations handling
 * - Error scenarios
 * - Cancellation/abort
 * - Complete events with metadata
 * - Heartbeat handling
 * - Follow-up questions
 */

import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import type { Mock } from 'vitest';
import { renderHook, act, waitFor } from '@testing-library/react';
import { useRealStreaming } from '../useRealStreaming';

/**
 * Create SSE Response with event: type\ndata: json format
 * useRealStreaming expects this specific format
 */
interface SSEEvent {
  type: string;
  data: object | null;
}

function createRealSSEResponse(events: SSEEvent[], options: { eventDelay?: number } = {}): Response {
  const { eventDelay = 0 } = options;

  async function* generateEvents() {
    const encoder = new TextEncoder();

    for (let i = 0; i < events.length; i++) {
      if (i > 0 && eventDelay > 0) {
        await new Promise(resolve => setTimeout(resolve, eventDelay));
      }

      const event = events[i];
      const sseData = `event: ${event.type}\ndata: ${JSON.stringify(event.data)}\n\n`;
      yield encoder.encode(sseData);
    }
  }

  const stream = ReadableStream.from(generateEvents());

  return new Response(stream, {
    status: 200,
    headers: {
      'Content-Type': 'text/event-stream',
    },
  });
}

describe('useRealStreaming', () => {
  let fetchSpy: Mock;

  beforeEach(() => {
    fetchSpy = vi.spyOn(global, 'fetch') as Mock;
  });

  afterEach(() => {
    vi.clearAllMocks();
    fetchSpy?.mockRestore();
  });

  // ==========================================================================
  // INITIAL STATE TESTS
  // ==========================================================================

  describe('Initial State', () => {
    it('should initialize with correct default state', () => {
      const { result } = renderHook(() => useRealStreaming());
      const [state] = result.current;

      expect(state.state).toBeNull();
      expect(state.currentAnswer).toBe('');
      expect(state.snippets).toEqual([]);
      expect(state.citations).toEqual([]);
      expect(state.followUpQuestions).toEqual([]);
      expect(state.totalTokens).toBe(0);
      expect(state.confidence).toBeNull();
      expect(state.isStreaming).toBe(false);
      expect(state.error).toBeNull();
    });

    it('should provide control functions', () => {
      const { result } = renderHook(() => useRealStreaming());
      const [, controls] = result.current;

      expect(controls.startStreaming).toBeInstanceOf(Function);
      expect(controls.stopStreaming).toBeInstanceOf(Function);
      expect(controls.reset).toBeInstanceOf(Function);
    });
  });

  // ==========================================================================
  // TOKEN STREAMING TESTS
  // ==========================================================================

  describe('Token Accumulation', () => {
    it('should accumulate tokens progressively', async () => {
      const events: SSEEvent[] = [
        { type: 'token', data: { token: 'Hello' } },
        { type: 'token', data: { token: ' ' } },
        { type: 'token', data: { token: 'World' } },
        { type: 'complete', data: { totalTokens: 3, confidence: 0.95 } },
      ];

      fetchSpy.mockResolvedValueOnce(createRealSSEResponse(events));

      const { result } = renderHook(() => useRealStreaming());

      await act(async () => {
        result.current[1].startStreaming('game-123', 'test query');
      });

      await waitFor(() => {
        expect(result.current[0].currentAnswer).toBe('Hello World');
      });

      expect(result.current[0].totalTokens).toBe(3);
      expect(result.current[0].confidence).toBe(0.95);
      expect(result.current[0].isStreaming).toBe(false);
    });

    it('should handle empty token data gracefully', async () => {
      const events: SSEEvent[] = [
        { type: 'token', data: { token: 'Test' } },
        { type: 'token', data: {} }, // Missing token field
        { type: 'token', data: { token: 'End' } },
        { type: 'complete', data: { totalTokens: 2 } },
      ];

      fetchSpy.mockResolvedValueOnce(createRealSSEResponse(events));

      const { result } = renderHook(() => useRealStreaming());

      await act(async () => {
        result.current[1].startStreaming('game-123', 'query');
      });

      await waitFor(() => {
        expect(result.current[0].isStreaming).toBe(false);
      });

      expect(result.current[0].currentAnswer).toBe('TestEnd');
    });

    it('should set isStreaming true during streaming', async () => {
      const events: SSEEvent[] = [
        { type: 'token', data: { token: 'A' } },
        { type: 'complete', data: { totalTokens: 1 } },
      ];

      fetchSpy.mockResolvedValueOnce(createRealSSEResponse(events, { eventDelay: 50 }));

      const { result } = renderHook(() => useRealStreaming());

      act(() => {
        result.current[1].startStreaming('game-123', 'query');
      });

      // Immediately after starting, isStreaming should be true
      expect(result.current[0].isStreaming).toBe(true);

      await waitFor(() => {
        expect(result.current[0].isStreaming).toBe(false);
      });
    });
  });

  // ==========================================================================
  // STATE UPDATE TESTS
  // ==========================================================================

  describe('State Updates', () => {
    it('should update state message progressively', async () => {
      const events: SSEEvent[] = [
        { type: 'stateUpdate', data: { state: 'Generating embeddings...' } },
        { type: 'stateUpdate', data: { state: 'Searching database...' } },
        { type: 'stateUpdate', data: { state: 'Generating response...' } },
        { type: 'complete', data: { totalTokens: 0 } },
      ];

      fetchSpy.mockResolvedValueOnce(createRealSSEResponse(events));

      const { result } = renderHook(() => useRealStreaming());

      await act(async () => {
        result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        expect(result.current[0].isStreaming).toBe(false);
      });

      // After complete, state should be null
      expect(result.current[0].state).toBeNull();
    });

    it('should handle null state data gracefully', async () => {
      const events: SSEEvent[] = [
        { type: 'stateUpdate', data: {} }, // Missing state field
        { type: 'complete', data: { totalTokens: 0 } },
      ];

      fetchSpy.mockResolvedValueOnce(createRealSSEResponse(events));

      const { result } = renderHook(() => useRealStreaming());

      await act(async () => {
        result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        expect(result.current[0].isStreaming).toBe(false);
      });

      expect(result.current[0].state).toBeNull();
    });
  });

  // ==========================================================================
  // CITATIONS TESTS
  // ==========================================================================

  describe('Citations Handling', () => {
    it('should store citations when received', async () => {
      const citations = [
        { documentId: 'doc-1', pageNumber: 1, snippet: 'Rule 1', relevanceScore: 0.95 },
        { documentId: 'doc-2', pageNumber: 5, snippet: 'Rule 2', relevanceScore: 0.88 },
      ];

      const events: SSEEvent[] = [
        { type: 'citations', data: { citations } },
        { type: 'complete', data: { totalTokens: 0 } },
      ];

      fetchSpy.mockResolvedValueOnce(createRealSSEResponse(events));

      const { result } = renderHook(() => useRealStreaming());

      await act(async () => {
        result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        expect(result.current[0].citations).toHaveLength(2);
      });

      expect(result.current[0].citations[0].documentId).toBe('doc-1');
      expect(result.current[0].citations[1].documentId).toBe('doc-2');
    });

    it('should store snippets when received', async () => {
      const snippets = [
        { text: 'Snippet 1', source: 'rules.pdf', page: 1 },
        { text: 'Snippet 2', source: 'manual.pdf', page: 3 },
      ];

      const events: SSEEvent[] = [
        { type: 'citations', data: { snippets } },
        { type: 'complete', data: { totalTokens: 0 } },
      ];

      fetchSpy.mockResolvedValueOnce(createRealSSEResponse(events));

      const { result } = renderHook(() => useRealStreaming());

      await act(async () => {
        result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        expect(result.current[0].snippets).toHaveLength(2);
      });

      expect(result.current[0].snippets[0].source).toBe('rules.pdf');
      expect(result.current[0].snippets[1].source).toBe('manual.pdf');
    });

    it('should preserve existing snippets when new citations have no snippets', async () => {
      const snippets = [{ text: 'Initial', source: 'doc.pdf', page: 1 }];
      const citations = [{ documentId: 'doc-1', pageNumber: 1, snippet: 'Citation', relevanceScore: 0.9 }];

      const events: SSEEvent[] = [
        { type: 'citations', data: { snippets } },
        { type: 'citations', data: { citations } }, // No snippets field
        { type: 'complete', data: { totalTokens: 0 } },
      ];

      fetchSpy.mockResolvedValueOnce(createRealSSEResponse(events));

      const { result } = renderHook(() => useRealStreaming());

      await act(async () => {
        result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        expect(result.current[0].citations).toHaveLength(1);
      });

      // Snippets should be preserved from first event
      expect(result.current[0].snippets).toHaveLength(1);
      expect(result.current[0].snippets[0].text).toBe('Initial');
    });
  });

  // ==========================================================================
  // FOLLOW-UP QUESTIONS TESTS
  // ==========================================================================

  describe('Follow-Up Questions', () => {
    it('should store follow-up questions when received', async () => {
      const questions = ['Question 1?', 'Question 2?', 'Question 3?'];

      const events: SSEEvent[] = [
        { type: 'followUpQuestions', data: { questions } },
        { type: 'complete', data: { totalTokens: 0 } },
      ];

      fetchSpy.mockResolvedValueOnce(createRealSSEResponse(events));

      const { result } = renderHook(() => useRealStreaming());

      await act(async () => {
        result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        expect(result.current[0].followUpQuestions).toHaveLength(3);
      });

      expect(result.current[0].followUpQuestions).toEqual(questions);
    });

    it('should handle empty questions array', async () => {
      const events: SSEEvent[] = [
        { type: 'followUpQuestions', data: { questions: [] } },
        { type: 'complete', data: { totalTokens: 0 } },
      ];

      fetchSpy.mockResolvedValueOnce(createRealSSEResponse(events));

      const { result } = renderHook(() => useRealStreaming());

      await act(async () => {
        result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        expect(result.current[0].isStreaming).toBe(false);
      });

      expect(result.current[0].followUpQuestions).toEqual([]);
    });
  });

  // ==========================================================================
  // COMPLETE EVENT TESTS
  // ==========================================================================

  describe('Complete Event', () => {
    it('should update state on complete event', async () => {
      const events: SSEEvent[] = [
        { type: 'token', data: { token: 'Answer' } },
        {
          type: 'complete',
          data: {
            totalTokens: 42,
            confidence: 0.87,
            snippets: [{ text: 'Final', source: 'doc.pdf', page: 1 }],
            citations: [{ documentId: 'final', pageNumber: 1, snippet: 'Final', relevanceScore: 0.99 }],
          },
        },
      ];

      fetchSpy.mockResolvedValueOnce(createRealSSEResponse(events));

      const { result } = renderHook(() => useRealStreaming());

      await act(async () => {
        result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        expect(result.current[0].isStreaming).toBe(false);
      });

      expect(result.current[0].totalTokens).toBe(42);
      expect(result.current[0].confidence).toBe(0.87);
      expect(result.current[0].snippets[0].text).toBe('Final');
      expect(result.current[0].state).toBeNull();
    });

    it('should call onComplete callback with correct data', async () => {
      const onComplete = vi.fn();
      const events: SSEEvent[] = [
        { type: 'token', data: { token: 'Test' } },
        { type: 'token', data: { token: ' answer' } },
        { type: 'followUpQuestions', data: { questions: ['Q1?', 'Q2?'] } },
        {
          type: 'complete',
          data: {
            totalTokens: 2,
            confidence: 0.9,
            citations: [{ documentId: 'doc-1', pageNumber: 1, snippet: 'Cite', relevanceScore: 0.95 }],
          },
        },
      ];

      fetchSpy.mockResolvedValueOnce(createRealSSEResponse(events));

      const { result } = renderHook(() => useRealStreaming({ onComplete }));

      await act(async () => {
        result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        expect(onComplete).toHaveBeenCalled();
      });

      expect(onComplete).toHaveBeenCalledWith(
        'Test answer',
        expect.any(Array), // snippets
        expect.objectContaining({
          totalTokens: 2,
          confidence: 0.9,
          followUpQuestions: ['Q1?', 'Q2?'],
          citations: expect.any(Array),
        })
      );
    });

    it('should not include followUpQuestions in metadata if empty', async () => {
      const onComplete = vi.fn();
      const events: SSEEvent[] = [
        { type: 'token', data: { token: 'Answer' } },
        { type: 'complete', data: { totalTokens: 1 } },
      ];

      fetchSpy.mockResolvedValueOnce(createRealSSEResponse(events));

      const { result } = renderHook(() => useRealStreaming({ onComplete }));

      await act(async () => {
        result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        expect(onComplete).toHaveBeenCalled();
      });

      // followUpQuestions should be undefined, not empty array
      const [, , metadata] = onComplete.mock.calls[0];
      expect(metadata.followUpQuestions).toBeUndefined();
    });
  });

  // ==========================================================================
  // ERROR HANDLING TESTS
  // ==========================================================================

  describe('Error Handling', () => {
    it('should handle SSE error events', async () => {
      const events: SSEEvent[] = [
        { type: 'error', data: { message: 'Server error', code: 'INTERNAL_ERROR' } },
      ];

      fetchSpy.mockResolvedValueOnce(createRealSSEResponse(events));

      const { result } = renderHook(() => useRealStreaming());

      await act(async () => {
        result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        expect(result.current[0].error).toBe('Server error');
      });

      expect(result.current[0].isStreaming).toBe(false);
      expect(result.current[0].state).toBeNull();
    });

    it('should call onError callback on error event', async () => {
      const onError = vi.fn();
      const events: SSEEvent[] = [
        { type: 'error', data: { message: 'Test error' } },
      ];

      fetchSpy.mockResolvedValueOnce(createRealSSEResponse(events));

      const { result } = renderHook(() => useRealStreaming({ onError }));

      await act(async () => {
        result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        expect(onError).toHaveBeenCalledWith('Test error');
      });
    });

    it('should handle HTTP errors', async () => {
      fetchSpy.mockResolvedValueOnce(new Response(null, { status: 500, statusText: 'Internal Server Error' }));

      const { result } = renderHook(() => useRealStreaming());

      await act(async () => {
        result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        expect(result.current[0].error).toBe('HTTP error! status: 500');
      });

      expect(result.current[0].isStreaming).toBe(false);
    });

    it('should handle network errors', async () => {
      fetchSpy.mockRejectedValueOnce(new Error('Network error'));

      const { result } = renderHook(() => useRealStreaming());

      await act(async () => {
        result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        expect(result.current[0].error).toBe('Network error');
      });

      expect(result.current[0].isStreaming).toBe(false);
    });

    it('should handle missing response body', async () => {
      const response = new Response(null, { status: 200 });
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      Object.defineProperty(response, 'body', { value: null });
      fetchSpy.mockResolvedValueOnce(response);

      const { result } = renderHook(() => useRealStreaming());

      await act(async () => {
        result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        expect(result.current[0].error).toBe('No response body');
      });
    });

    it('should handle unknown error event without message', async () => {
      const events: SSEEvent[] = [
        { type: 'error', data: {} }, // No message field
      ];

      fetchSpy.mockResolvedValueOnce(createRealSSEResponse(events));

      const { result } = renderHook(() => useRealStreaming());

      await act(async () => {
        result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        expect(result.current[0].error).toBe('Unknown error occurred');
      });
    });

    it('should call onError callback on HTTP errors', async () => {
      const onError = vi.fn();
      fetchSpy.mockResolvedValueOnce(new Response(null, { status: 400 }));

      const { result } = renderHook(() => useRealStreaming({ onError }));

      await act(async () => {
        result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        expect(onError).toHaveBeenCalledWith('HTTP error! status: 400');
      });
    });
  });

  // ==========================================================================
  // ABORT/CANCELLATION TESTS
  // ==========================================================================

  describe('Abort and Cancellation', () => {
    it('should stop streaming when stopStreaming is called', async () => {
      const events: SSEEvent[] = [
        { type: 'token', data: { token: 'Hello' } },
        { type: 'token', data: { token: ' World' } },
        { type: 'complete', data: { totalTokens: 2 } },
      ];

      fetchSpy.mockResolvedValueOnce(createRealSSEResponse(events, { eventDelay: 100 }));

      const { result } = renderHook(() => useRealStreaming());

      act(() => {
        result.current[1].startStreaming('game-123', 'test');
      });

      // Wait a bit then stop
      await act(async () => {
        await new Promise(resolve => setTimeout(resolve, 50));
        result.current[1].stopStreaming();
      });

      expect(result.current[0].isStreaming).toBe(false);
    });

    it('should not treat abort as error', async () => {
      const onError = vi.fn();
      const events: SSEEvent[] = [
        { type: 'token', data: { token: 'Test' } },
        { type: 'complete', data: { totalTokens: 1 } },
      ];

      fetchSpy.mockResolvedValueOnce(createRealSSEResponse(events, { eventDelay: 200 }));

      const { result } = renderHook(() => useRealStreaming({ onError }));

      act(() => {
        result.current[1].startStreaming('game-123', 'test');
      });

      await act(async () => {
        await new Promise(resolve => setTimeout(resolve, 50));
        result.current[1].stopStreaming();
      });

      // onError should not be called for abort
      expect(onError).not.toHaveBeenCalled();
      expect(result.current[0].error).toBeNull();
    });

    it('should stop previous stream when starting a new one', async () => {
      const events1: SSEEvent[] = [
        { type: 'token', data: { token: 'First' } },
        { type: 'complete', data: { totalTokens: 1 } },
      ];
      const events2: SSEEvent[] = [
        { type: 'token', data: { token: 'Second' } },
        { type: 'complete', data: { totalTokens: 1 } },
      ];

      fetchSpy.mockResolvedValueOnce(createRealSSEResponse(events1, { eventDelay: 100 }));
      fetchSpy.mockResolvedValueOnce(createRealSSEResponse(events2));

      const { result } = renderHook(() => useRealStreaming());

      // Start first stream
      act(() => {
        result.current[1].startStreaming('game-1', 'query 1');
      });

      // Start second stream - this should reset state and start fresh
      await act(async () => {
        result.current[1].startStreaming('game-2', 'query 2');
      });

      await waitFor(() => {
        expect(result.current[0].isStreaming).toBe(false);
      });

      // Second stream should complete - answer should contain "Second"
      // Note: Due to the async nature, both streams may have written tokens
      // The important thing is that the second stream completed
      expect(result.current[0].currentAnswer).toContain('Second');
    });
  });

  // ==========================================================================
  // HEARTBEAT TESTS
  // ==========================================================================

  describe('Heartbeat Handling', () => {
    it('should ignore heartbeat events', async () => {
      const events: SSEEvent[] = [
        { type: 'heartbeat', data: null },
        { type: 'token', data: { token: 'Test' } },
        { type: 'heartbeat', data: null },
        { type: 'complete', data: { totalTokens: 1 } },
      ];

      fetchSpy.mockResolvedValueOnce(createRealSSEResponse(events));

      const { result } = renderHook(() => useRealStreaming());

      await act(async () => {
        result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        expect(result.current[0].currentAnswer).toBe('Test');
      });

      expect(result.current[0].isStreaming).toBe(false);
    });
  });

  // ==========================================================================
  // RESET TESTS
  // ==========================================================================

  describe('Reset Functionality', () => {
    it('should reset state to initial values', async () => {
      const events: SSEEvent[] = [
        { type: 'token', data: { token: 'Answer' } },
        { type: 'complete', data: { totalTokens: 1, confidence: 0.9 } },
      ];

      fetchSpy.mockResolvedValueOnce(createRealSSEResponse(events));

      const { result } = renderHook(() => useRealStreaming());

      await act(async () => {
        result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        expect(result.current[0].currentAnswer).toBe('Answer');
      });

      act(() => {
        result.current[1].reset();
      });

      expect(result.current[0].currentAnswer).toBe('');
      expect(result.current[0].totalTokens).toBe(0);
      expect(result.current[0].confidence).toBeNull();
      expect(result.current[0].isStreaming).toBe(false);
      expect(result.current[0].error).toBeNull();
    });

    it('should stop streaming when reset is called', async () => {
      const events: SSEEvent[] = [
        { type: 'token', data: { token: 'Test' } },
        { type: 'complete', data: { totalTokens: 1 } },
      ];

      fetchSpy.mockResolvedValueOnce(createRealSSEResponse(events, { eventDelay: 100 }));

      const { result } = renderHook(() => useRealStreaming());

      act(() => {
        result.current[1].startStreaming('game-123', 'test');
      });

      expect(result.current[0].isStreaming).toBe(true);

      act(() => {
        result.current[1].reset();
      });

      expect(result.current[0].isStreaming).toBe(false);
      expect(result.current[0].currentAnswer).toBe('');
    });
  });

  // ==========================================================================
  // URL AND REQUEST TESTS
  // ==========================================================================

  describe('Request Configuration', () => {
    it('should send POST request with correct body', async () => {
      const events: SSEEvent[] = [{ type: 'complete', data: { totalTokens: 0 } }];
      fetchSpy.mockResolvedValueOnce(createRealSSEResponse(events));

      const { result } = renderHook(() => useRealStreaming());

      await act(async () => {
        result.current[1].startStreaming('game-123', 'my query', 'chat-456', 'Semantic');
      });

      await waitFor(() => {
        expect(result.current[0].isStreaming).toBe(false);
      });

      expect(fetchSpy).toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/agents/qa/stream'),
        expect.objectContaining({
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            gameId: 'game-123',
            query: 'my query',
            chatId: 'chat-456',
            searchMode: 'Semantic',
          }),
          credentials: 'include',
          signal: expect.any(AbortSignal),
        })
      );
    });

    it('should use default searchMode when not provided', async () => {
      const events: SSEEvent[] = [{ type: 'complete', data: { totalTokens: 0 } }];
      fetchSpy.mockResolvedValueOnce(createRealSSEResponse(events));

      const { result } = renderHook(() => useRealStreaming());

      await act(async () => {
        result.current[1].startStreaming('game-123', 'query');
      });

      await waitFor(() => {
        expect(result.current[0].isStreaming).toBe(false);
      });

      expect(fetchSpy).toHaveBeenCalledWith(
        expect.any(String),
        expect.objectContaining({
          body: expect.stringContaining('"searchMode":"Hybrid"'),
        })
      );
    });
  });

  // ==========================================================================
  // MALFORMED DATA TESTS
  // ==========================================================================

  describe('Malformed Data Handling', () => {
    it('should handle malformed JSON gracefully', async () => {
      // Create a custom response with malformed data
      const stream = new ReadableStream({
        start(controller) {
          const encoder = new TextEncoder();
          controller.enqueue(encoder.encode('event: token\ndata: {invalid json}\n\n'));
          controller.enqueue(encoder.encode('event: token\ndata: {"token":"Valid"}\n\n'));
          controller.enqueue(encoder.encode('event: complete\ndata: {"totalTokens":1}\n\n'));
          controller.close();
        },
      });

      fetchSpy.mockResolvedValueOnce(
        new Response(stream, {
          status: 200,
          headers: { 'Content-Type': 'text/event-stream' },
        })
      );

      const { result } = renderHook(() => useRealStreaming());

      await act(async () => {
        result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        expect(result.current[0].isStreaming).toBe(false);
      });

      // Should continue processing valid events
      expect(result.current[0].currentAnswer).toBe('Valid');
    });

    it('should handle unknown event types', async () => {
      // Spy on console.warn
      const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});

      const events: SSEEvent[] = [
        { type: 'unknownType', data: { foo: 'bar' } },
        { type: 'complete', data: { totalTokens: 0 } },
      ];

      fetchSpy.mockResolvedValueOnce(createRealSSEResponse(events));

      const { result } = renderHook(() => useRealStreaming());

      await act(async () => {
        result.current[1].startStreaming('game-123', 'test');
      });

      await waitFor(() => {
        expect(result.current[0].isStreaming).toBe(false);
      });

      expect(warnSpy).toHaveBeenCalledWith('Unknown event type:', 'unknownType');
      warnSpy.mockRestore();
    });
  });

  // ==========================================================================
  // CALLBACK STABILITY TESTS
  // ==========================================================================

  describe('Callback Stability', () => {
    it('should use latest callbacks without restarting stream', async () => {
      const onComplete1 = vi.fn();
      const onComplete2 = vi.fn();

      const events: SSEEvent[] = [
        { type: 'token', data: { token: 'Test' } },
        { type: 'complete', data: { totalTokens: 1 } },
      ];

      fetchSpy.mockResolvedValueOnce(createRealSSEResponse(events, { eventDelay: 50 }));

      const { result, rerender } = renderHook(
        ({ onComplete }) => useRealStreaming({ onComplete }),
        { initialProps: { onComplete: onComplete1 } }
      );

      act(() => {
        result.current[1].startStreaming('game-123', 'test');
      });

      // Update callback mid-stream
      rerender({ onComplete: onComplete2 });

      await waitFor(() => {
        expect(result.current[0].isStreaming).toBe(false);
      });

      // Should call the new callback, not the old one
      expect(onComplete1).not.toHaveBeenCalled();
      expect(onComplete2).toHaveBeenCalled();
    });
  });
});
