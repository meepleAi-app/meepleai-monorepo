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

import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import type { Mock } from 'vitest';
import { renderHook, act, waitFor } from '@testing-library/react';
import { useStreamingChat } from '../useStreamingChat';
import { StreamingEventType } from '@/lib/api/schemas/streaming.schemas';

// No global fetch mock needed - MSW handles interception

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
  let fetchSpy: Mock;

  beforeEach(() => {
    // Spy on fetch to enable mockImplementation for SSE responses
    fetchSpy = vi.spyOn(global, 'fetch') as Mock;
  });

  afterEach(() => {
    vi.clearAllMocks();
    fetchSpy?.mockRestore();
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

      (global.fetch as Mock<typeof fetch>).mockResolvedValueOnce(
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
      const onToken = vi.fn();
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

      (global.fetch as Mock<typeof fetch>).mockResolvedValueOnce(
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

      (global.fetch as Mock<typeof fetch>).mockResolvedValueOnce(
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
      const onStateUpdate = vi.fn();
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

      (global.fetch as Mock<typeof fetch>).mockResolvedValueOnce(
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

      (global.fetch as Mock<typeof fetch>).mockResolvedValueOnce(
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

      (global.fetch as Mock<typeof fetch>).mockResolvedValueOnce(
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

      (global.fetch as MockedFunction<typeof fetch>).mockResolvedValueOnce(
        createSSEResponse(events)
      );

      const { result } = renderHook(() => useStreamingChat());
