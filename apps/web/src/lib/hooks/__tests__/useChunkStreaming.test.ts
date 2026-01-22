/**
 * useChunkStreaming Hook Tests (Issue #2765)
 *
 * Tests for EventSource-based SSE streaming with chunk accumulation.
 * Uses MockEventSource for SSE simulation.
 */

import { renderHook, act, waitFor } from '@testing-library/react';
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';

import {
  useChunkStreaming,
  type ChunkStreamingOptions,
  type ChunkStreamingState,
} from '../useChunkStreaming';

// ============================================================================
// MockEventSource for EventSource-based tests
// ============================================================================

class MockEventSource {
  static instances: MockEventSource[] = [];
  static readonly CONNECTING = 0;
  static readonly OPEN = 1;
  static readonly CLOSED = 2;

  url: string;
  withCredentials: boolean;
  readyState = MockEventSource.CONNECTING;

  onopen: (() => void) | null = null;
  onmessage: ((event: MessageEvent) => void) | null = null;
  onerror: ((event: Event) => void) | null = null;

  private _listeners: Map<string, Set<EventListener>> = new Map();

  constructor(url: string, options?: { withCredentials?: boolean }) {
    this.url = url;
    this.withCredentials = options?.withCredentials ?? false;
    MockEventSource.instances.push(this);

    // Simulate async connection (like real EventSource)
    queueMicrotask(() => {
      if (this.readyState !== MockEventSource.CLOSED) {
        this.readyState = MockEventSource.OPEN;
        this.onopen?.();
        this._emit('open', new Event('open'));
      }
    });
  }

  close() {
    this.readyState = MockEventSource.CLOSED;
  }

  addEventListener(type: string, listener: EventListener) {
    if (!this._listeners.has(type)) {
      this._listeners.set(type, new Set());
    }
    this._listeners.get(type)!.add(listener);
  }

  removeEventListener(type: string, listener: EventListener) {
    this._listeners.get(type)?.delete(listener);
  }

  private _emit(type: string, event: Event) {
    this._listeners.get(type)?.forEach((listener) => listener(event));
  }

  // Test helper methods
  simulateMessage(data: string) {
    if (this.readyState !== MockEventSource.OPEN) return;
    const messageEvent = new MessageEvent('message', { data });
    this.onmessage?.(messageEvent);
    this._emit('message', messageEvent);
  }

  simulateChunk(chunk: object) {
    this.simulateMessage(JSON.stringify(chunk));
  }

  simulateDone() {
    this.simulateMessage('[DONE]');
  }

  simulateError() {
    const errorEvent = new Event('error');
    this.onerror?.(errorEvent);
    this._emit('error', errorEvent);
  }

  static reset() {
    MockEventSource.instances.forEach((es) => es.close());
    MockEventSource.instances = [];
  }

  static getLatest(): MockEventSource | undefined {
    return MockEventSource.instances[MockEventSource.instances.length - 1];
  }
}

// ============================================================================
// Test Setup
// ============================================================================

describe('useChunkStreaming', () => {
  const originalEventSource = global.EventSource;

  beforeEach(() => {
    MockEventSource.reset();
    global.EventSource = MockEventSource as unknown as typeof EventSource;
    vi.useFakeTimers({ shouldAdvanceTime: true });
  });

  afterEach(() => {
    global.EventSource = originalEventSource;
    MockEventSource.reset();
    vi.useRealTimers();
  });

  // ==========================================================================
  // Initial State Tests
  // ==========================================================================

  describe('Initial State', () => {
    it('should return initial state', () => {
      const { result } = renderHook(() => useChunkStreaming());

      expect(result.current[0]).toEqual({
        phase: 'idle',
        content: '',
        chunks: [],
        chunkCount: 0,
        isStreaming: false,
        isConnected: false,
        error: null,
        reconnectAttempts: 0,
      });
    });

    it('should return controls object', () => {
      const { result } = renderHook(() => useChunkStreaming());

      const [, controls] = result.current;
      expect(controls).toHaveProperty('connect');
      expect(controls).toHaveProperty('disconnect');
      expect(controls).toHaveProperty('reset');
      expect(controls).toHaveProperty('retry');
    });
  });

  // ==========================================================================
  // Connection Tests
  // ==========================================================================

  describe('Connection', () => {
    it('should transition to connecting phase when connect is called', async () => {
      const { result } = renderHook(() => useChunkStreaming());

      act(() => {
        result.current[1].connect('/api/stream');
      });

      expect(result.current[0].phase).toBe('connecting');
      expect(result.current[0].isStreaming).toBe(true);
    });

    it('should create EventSource with correct URL', async () => {
      const { result } = renderHook(() => useChunkStreaming());

      act(() => {
        result.current[1].connect('/api/stream?query=test');
      });

      const eventSource = MockEventSource.getLatest();
      expect(eventSource).toBeDefined();
      expect(eventSource!.url).toBe('/api/stream?query=test');
    });

    it('should transition to receiving phase when connection opens', async () => {
      const { result } = renderHook(() => useChunkStreaming());

      act(() => {
        result.current[1].connect('/api/stream');
      });

      await waitFor(() => {
        expect(result.current[0].phase).toBe('receiving');
        expect(result.current[0].isConnected).toBe(true);
      });
    });

    it('should call onConnect callback when connected', async () => {
      const onConnect = vi.fn();
      const { result } = renderHook(() => useChunkStreaming({ onConnect }));

      act(() => {
        result.current[1].connect('/api/stream');
      });

      await waitFor(() => {
        expect(onConnect).toHaveBeenCalledTimes(1);
      });
    });

    it('should close previous connection when connecting to new URL', async () => {
      const { result } = renderHook(() => useChunkStreaming());

      act(() => {
        result.current[1].connect('/api/stream1');
      });

      await waitFor(() => {
        expect(result.current[0].isConnected).toBe(true);
      });

      const firstEventSource = MockEventSource.getLatest();

      act(() => {
        result.current[1].connect('/api/stream2');
      });

      expect(firstEventSource!.readyState).toBe(MockEventSource.CLOSED);
    });
  });

  // ==========================================================================
  // Chunk Receiving Tests
  // ==========================================================================

  describe('Chunk Receiving', () => {
    it('should accumulate content from JSON chunks', async () => {
      const { result } = renderHook(() => useChunkStreaming());

      act(() => {
        result.current[1].connect('/api/stream');
      });

      await waitFor(() => {
        expect(result.current[0].isConnected).toBe(true);
      });

      const eventSource = MockEventSource.getLatest()!;

      act(() => {
        eventSource.simulateChunk({ chunk: 'Hello' });
      });

      expect(result.current[0].content).toBe('Hello');
      expect(result.current[0].chunkCount).toBe(1);

      act(() => {
        eventSource.simulateChunk({ chunk: ' World' });
      });

      expect(result.current[0].content).toBe('Hello World');
      expect(result.current[0].chunkCount).toBe(2);
    });

    it('should handle content field in chunks', async () => {
      const { result } = renderHook(() => useChunkStreaming());

      act(() => {
        result.current[1].connect('/api/stream');
      });

      await waitFor(() => {
        expect(result.current[0].isConnected).toBe(true);
      });

      const eventSource = MockEventSource.getLatest()!;

      act(() => {
        eventSource.simulateChunk({ content: 'Test content' });
      });

      expect(result.current[0].content).toBe('Test content');
    });

    it('should handle token field in chunks', async () => {
      const { result } = renderHook(() => useChunkStreaming());

      act(() => {
        result.current[1].connect('/api/stream');
      });

      await waitFor(() => {
        expect(result.current[0].isConnected).toBe(true);
      });

      const eventSource = MockEventSource.getLatest()!;

      act(() => {
        eventSource.simulateChunk({ token: 'Token ' });
        eventSource.simulateChunk({ token: 'by ' });
        eventSource.simulateChunk({ token: 'token' });
      });

      expect(result.current[0].content).toBe('Token by token');
    });

    it('should handle plain text messages', async () => {
      const { result } = renderHook(() => useChunkStreaming());

      act(() => {
        result.current[1].connect('/api/stream');
      });

      await waitFor(() => {
        expect(result.current[0].isConnected).toBe(true);
      });

      const eventSource = MockEventSource.getLatest()!;

      act(() => {
        eventSource.simulateMessage('Plain text');
      });

      expect(result.current[0].content).toBe('Plain text');
    });

    it('should call onChunk callback with chunk and accumulated content', async () => {
      const onChunk = vi.fn();
      const { result } = renderHook(() => useChunkStreaming({ onChunk }));

      act(() => {
        result.current[1].connect('/api/stream');
      });

      await waitFor(() => {
        expect(result.current[0].isConnected).toBe(true);
      });

      const eventSource = MockEventSource.getLatest()!;

      act(() => {
        eventSource.simulateChunk({ chunk: 'Hello' });
      });

      expect(onChunk).toHaveBeenCalledWith(
        expect.objectContaining({ content: 'Hello' }),
        'Hello'
      );

      act(() => {
        eventSource.simulateChunk({ chunk: ' World' });
      });

      expect(onChunk).toHaveBeenCalledWith(
        expect.objectContaining({ content: ' World' }),
        'Hello World'
      );
    });

    it('should store individual chunks in chunks array', async () => {
      const { result } = renderHook(() => useChunkStreaming());

      act(() => {
        result.current[1].connect('/api/stream');
      });

      await waitFor(() => {
        expect(result.current[0].isConnected).toBe(true);
      });

      const eventSource = MockEventSource.getLatest()!;

      act(() => {
        eventSource.simulateChunk({ chunk: 'First', index: 0 });
        eventSource.simulateChunk({ chunk: 'Second', index: 1 });
      });

      expect(result.current[0].chunks).toHaveLength(2);
      expect(result.current[0].chunks[0].content).toBe('First');
      expect(result.current[0].chunks[1].content).toBe('Second');
    });
  });

  // ==========================================================================
  // Completion Tests
  // ==========================================================================

  describe('Completion', () => {
    it('should transition to complete on [DONE] signal', async () => {
      const { result } = renderHook(() => useChunkStreaming());

      act(() => {
        result.current[1].connect('/api/stream');
      });

      await waitFor(() => {
        expect(result.current[0].isConnected).toBe(true);
      });

      const eventSource = MockEventSource.getLatest()!;

      act(() => {
        eventSource.simulateChunk({ chunk: 'Complete content' });
        eventSource.simulateDone();
      });

      expect(result.current[0].phase).toBe('complete');
      expect(result.current[0].isStreaming).toBe(false);
      expect(result.current[0].isConnected).toBe(false);
    });

    it('should call onComplete callback with final content', async () => {
      const onComplete = vi.fn();
      const { result } = renderHook(() => useChunkStreaming({ onComplete }));

      act(() => {
        result.current[1].connect('/api/stream');
      });

      await waitFor(() => {
        expect(result.current[0].isConnected).toBe(true);
      });

      const eventSource = MockEventSource.getLatest()!;

      act(() => {
        eventSource.simulateChunk({ chunk: 'Hello ' });
        eventSource.simulateChunk({ chunk: 'World' });
        eventSource.simulateDone();
      });

      expect(onComplete).toHaveBeenCalledWith('Hello World', 2);
    });

    it('should transition to complete on isLast flag', async () => {
      const { result } = renderHook(() => useChunkStreaming());

      act(() => {
        result.current[1].connect('/api/stream');
      });

      await waitFor(() => {
        expect(result.current[0].isConnected).toBe(true);
      });

      const eventSource = MockEventSource.getLatest()!;

      act(() => {
        eventSource.simulateChunk({ chunk: 'Final', isLast: true });
      });

      expect(result.current[0].phase).toBe('complete');
      expect(result.current[0].content).toBe('Final');
    });

    it('should transition to complete on done flag', async () => {
      const { result } = renderHook(() => useChunkStreaming());

      act(() => {
        result.current[1].connect('/api/stream');
      });

      await waitFor(() => {
        expect(result.current[0].isConnected).toBe(true);
      });

      const eventSource = MockEventSource.getLatest()!;

      act(() => {
        eventSource.simulateChunk({ chunk: 'Done', done: true });
      });

      expect(result.current[0].phase).toBe('complete');
    });

    it('should close EventSource on completion', async () => {
      const { result } = renderHook(() => useChunkStreaming());

      act(() => {
        result.current[1].connect('/api/stream');
      });

      await waitFor(() => {
        expect(result.current[0].isConnected).toBe(true);
      });

      const eventSource = MockEventSource.getLatest()!;

      act(() => {
        eventSource.simulateDone();
      });

      expect(eventSource.readyState).toBe(MockEventSource.CLOSED);
    });
  });

  // ==========================================================================
  // Error Handling Tests
  // ==========================================================================

  describe('Error Handling', () => {
    it('should transition to error phase on EventSource error', async () => {
      const { result } = renderHook(() => useChunkStreaming());

      act(() => {
        result.current[1].connect('/api/stream');
      });

      await waitFor(() => {
        expect(result.current[0].isConnected).toBe(true);
      });

      const eventSource = MockEventSource.getLatest()!;

      act(() => {
        eventSource.simulateError();
      });

      expect(result.current[0].phase).toBe('error');
      expect(result.current[0].error).toBe('Connection failed');
      expect(result.current[0].isStreaming).toBe(false);
    });

    it('should call onError callback on error', async () => {
      const onError = vi.fn();
      const { result } = renderHook(() => useChunkStreaming({ onError }));

      act(() => {
        result.current[1].connect('/api/stream');
      });

      await waitFor(() => {
        expect(result.current[0].isConnected).toBe(true);
      });

      const eventSource = MockEventSource.getLatest()!;

      act(() => {
        eventSource.simulateError();
      });

      expect(onError).toHaveBeenCalledWith('Connection failed');
    });

    it('should not auto-reconnect by default', async () => {
      const { result } = renderHook(() => useChunkStreaming());

      act(() => {
        result.current[1].connect('/api/stream');
      });

      await waitFor(() => {
        expect(result.current[0].isConnected).toBe(true);
      });

      const eventSource = MockEventSource.getLatest()!;

      act(() => {
        eventSource.simulateError();
      });

      expect(result.current[0].phase).toBe('error');
      expect(MockEventSource.instances).toHaveLength(1);
    });

    it('should auto-reconnect when enabled', async () => {
      const { result } = renderHook(() =>
        useChunkStreaming({ autoReconnect: true, maxReconnectAttempts: 3 })
      );

      act(() => {
        result.current[1].connect('/api/stream');
      });

      await waitFor(() => {
        expect(result.current[0].isConnected).toBe(true);
      });

      act(() => {
        MockEventSource.getLatest()!.simulateError();
      });

      expect(result.current[0].phase).toBe('connecting');
      expect(result.current[0].reconnectAttempts).toBe(1);
    });

    it('should stop reconnecting after max attempts', async () => {
      const onError = vi.fn();
      const { result } = renderHook(() =>
        useChunkStreaming({ autoReconnect: true, maxReconnectAttempts: 2, onError })
      );

      act(() => {
        result.current[1].connect('/api/stream');
      });

      await waitFor(() => {
        expect(result.current[0].isConnected).toBe(true);
      });

      // First error - should reconnect
      act(() => {
        MockEventSource.getLatest()!.simulateError();
      });

      // Advance past reconnect delay
      await act(async () => {
        await vi.advanceTimersByTimeAsync(2000);
      });

      await waitFor(() => {
        expect(result.current[0].isConnected).toBe(true);
      });

      // Second error - should reconnect
      act(() => {
        MockEventSource.getLatest()!.simulateError();
      });

      await act(async () => {
        await vi.advanceTimersByTimeAsync(4000);
      });

      await waitFor(() => {
        expect(result.current[0].isConnected).toBe(true);
      });

      // Third error - should give up
      act(() => {
        MockEventSource.getLatest()!.simulateError();
      });

      expect(result.current[0].phase).toBe('error');
      expect(onError).toHaveBeenCalled();
    });
  });

  // ==========================================================================
  // Control Tests
  // ==========================================================================

  describe('Controls', () => {
    it('should disconnect and close EventSource', async () => {
      const { result } = renderHook(() => useChunkStreaming());

      act(() => {
        result.current[1].connect('/api/stream');
      });

      await waitFor(() => {
        expect(result.current[0].isConnected).toBe(true);
      });

      const eventSource = MockEventSource.getLatest()!;

      act(() => {
        result.current[1].disconnect();
      });

      expect(eventSource.readyState).toBe(MockEventSource.CLOSED);
      expect(result.current[0].phase).toBe('idle');
      expect(result.current[0].isStreaming).toBe(false);
    });

    it('should reset state completely', async () => {
      const { result } = renderHook(() => useChunkStreaming());

      act(() => {
        result.current[1].connect('/api/stream');
      });

      await waitFor(() => {
        expect(result.current[0].isConnected).toBe(true);
      });

      const eventSource = MockEventSource.getLatest()!;

      act(() => {
        eventSource.simulateChunk({ chunk: 'Some content' });
      });

      expect(result.current[0].content).toBe('Some content');

      act(() => {
        result.current[1].reset();
      });

      expect(result.current[0]).toEqual({
        phase: 'idle',
        content: '',
        chunks: [],
        chunkCount: 0,
        isStreaming: false,
        isConnected: false,
        error: null,
        reconnectAttempts: 0,
      });
    });

    it('should allow retry after error', async () => {
      const { result } = renderHook(() => useChunkStreaming());

      act(() => {
        result.current[1].connect('/api/stream');
      });

      await waitFor(() => {
        expect(result.current[0].isConnected).toBe(true);
      });

      act(() => {
        MockEventSource.getLatest()!.simulateError();
      });

      expect(result.current[0].phase).toBe('error');

      act(() => {
        result.current[1].retry();
      });

      expect(result.current[0].phase).toBe('connecting');
    });

    it('should do nothing on retry if no previous URL', () => {
      const { result } = renderHook(() => useChunkStreaming());

      // Never connected, try retry
      act(() => {
        result.current[1].retry();
      });

      expect(result.current[0].phase).toBe('idle');
    });
  });

  // ==========================================================================
  // Cleanup Tests
  // ==========================================================================

  describe('Cleanup', () => {
    it('should close EventSource on unmount', async () => {
      const { result, unmount } = renderHook(() => useChunkStreaming());

      act(() => {
        result.current[1].connect('/api/stream');
      });

      await waitFor(() => {
        expect(result.current[0].isConnected).toBe(true);
      });

      const eventSource = MockEventSource.getLatest()!;

      unmount();

      expect(eventSource.readyState).toBe(MockEventSource.CLOSED);
    });

    it('should preserve content after disconnect', async () => {
      const { result } = renderHook(() => useChunkStreaming());

      act(() => {
        result.current[1].connect('/api/stream');
      });

      await waitFor(() => {
        expect(result.current[0].isConnected).toBe(true);
      });

      const eventSource = MockEventSource.getLatest()!;

      act(() => {
        eventSource.simulateChunk({ chunk: 'Preserved content' });
        result.current[1].disconnect();
      });

      expect(result.current[0].content).toBe('Preserved content');
    });
  });

  // ==========================================================================
  // Edge Cases
  // ==========================================================================

  describe('Edge Cases', () => {
    it('should handle empty chunks', async () => {
      const { result } = renderHook(() => useChunkStreaming());

      act(() => {
        result.current[1].connect('/api/stream');
      });

      await waitFor(() => {
        expect(result.current[0].isConnected).toBe(true);
      });

      const eventSource = MockEventSource.getLatest()!;

      act(() => {
        eventSource.simulateChunk({ chunk: '' });
        eventSource.simulateChunk({ chunk: 'Real content' });
      });

      expect(result.current[0].content).toBe('Real content');
      expect(result.current[0].chunkCount).toBe(2);
    });

    it('should handle rapid chunk delivery', async () => {
      const { result } = renderHook(() => useChunkStreaming());

      act(() => {
        result.current[1].connect('/api/stream');
      });

      await waitFor(() => {
        expect(result.current[0].isConnected).toBe(true);
      });

      const eventSource = MockEventSource.getLatest()!;

      act(() => {
        for (let i = 0; i < 100; i++) {
          eventSource.simulateChunk({ chunk: `${i} ` });
        }
      });

      expect(result.current[0].chunkCount).toBe(100);
      expect(result.current[0].content).toContain('99');
    });

    it('should handle malformed JSON gracefully', async () => {
      const { result } = renderHook(() => useChunkStreaming());

      act(() => {
        result.current[1].connect('/api/stream');
      });

      await waitFor(() => {
        expect(result.current[0].isConnected).toBe(true);
      });

      const eventSource = MockEventSource.getLatest()!;

      act(() => {
        eventSource.simulateMessage('{invalid json');
      });

      // Should treat as plain text
      expect(result.current[0].content).toBe('{invalid json');
    });

    it('should handle JSON string values', async () => {
      const { result } = renderHook(() => useChunkStreaming());

      act(() => {
        result.current[1].connect('/api/stream');
      });

      await waitFor(() => {
        expect(result.current[0].isConnected).toBe(true);
      });

      const eventSource = MockEventSource.getLatest()!;

      act(() => {
        eventSource.simulateMessage('"Just a string"');
      });

      expect(result.current[0].content).toBe('Just a string');
    });

    it('should handle messages before connection is open', () => {
      const { result } = renderHook(() => useChunkStreaming());

      act(() => {
        result.current[1].connect('/api/stream');
      });

      // Immediately try to send message before onopen
      const eventSource = MockEventSource.getLatest()!;
      eventSource.readyState = MockEventSource.CONNECTING;

      act(() => {
        eventSource.simulateMessage('Early message');
      });

      // Message should be ignored because connection isn't open
      expect(result.current[0].content).toBe('');
    });

    it('should handle whitespace [DONE] signal', async () => {
      const { result } = renderHook(() => useChunkStreaming());

      act(() => {
        result.current[1].connect('/api/stream');
      });

      await waitFor(() => {
        expect(result.current[0].isConnected).toBe(true);
      });

      const eventSource = MockEventSource.getLatest()!;

      act(() => {
        eventSource.simulateChunk({ chunk: 'Content' });
        eventSource.simulateMessage('  [DONE]  ');
      });

      expect(result.current[0].phase).toBe('complete');
    });
  });

  // ==========================================================================
  // Options Stability Tests
  // ==========================================================================

  describe('Options Stability', () => {
    it('should use updated callbacks without reconnecting', async () => {
      const onChunk1 = vi.fn();
      const onChunk2 = vi.fn();

      const { result, rerender } = renderHook(
        ({ onChunk }: ChunkStreamingOptions) => useChunkStreaming({ onChunk }),
        { initialProps: { onChunk: onChunk1 } }
      );

      act(() => {
        result.current[1].connect('/api/stream');
      });

      await waitFor(() => {
        expect(result.current[0].isConnected).toBe(true);
      });

      const eventSource = MockEventSource.getLatest()!;

      act(() => {
        eventSource.simulateChunk({ chunk: 'First' });
      });

      expect(onChunk1).toHaveBeenCalledTimes(1);

      // Update callback
      rerender({ onChunk: onChunk2 });

      act(() => {
        eventSource.simulateChunk({ chunk: 'Second' });
      });

      expect(onChunk1).toHaveBeenCalledTimes(1);
      expect(onChunk2).toHaveBeenCalledTimes(1);
    });
  });
});
