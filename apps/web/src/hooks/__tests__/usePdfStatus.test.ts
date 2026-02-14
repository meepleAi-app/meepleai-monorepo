/**
 * usePdfStatus Hook Tests (Issue #4218)
 * Tests SSE connection, polling fallback, auto-reconnect, cleanup
 */

import { renderHook, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { usePdfStatus } from '../usePdfStatus';

// Mock API
vi.mock('@/lib/api', () => ({
  api: {
    pdf: {
      getProcessingProgress: vi.fn(),
    },
  },
}));

// Mock pdf types
vi.mock('@/types/pdf', () => ({
  mapProcessingStepToPdfState: vi.fn((step: string) => {
    const map: Record<string, string> = {
      Uploading: 'uploading',
      Extracting: 'extracting',
      Chunking: 'chunking',
      Embedding: 'embedding',
      Indexing: 'indexing',
      Completed: 'ready',
      Failed: 'failed',
    };
    return map[step] || 'uploading';
  }),
}));

import { api } from '@/lib/api';

// ============================================================================
// MockEventSource - synchronous control for deterministic tests
// ============================================================================

type ESListener = ((event: Event | MessageEvent) => void) | null;

let mockEventSourceInstances: MockEventSource[] = [];

class MockEventSource {
  url: string;
  readyState = 0; // CONNECTING
  onopen: ESListener = null;
  onmessage: ESListener = null;
  onerror: ESListener = null;
  closed = false;

  static CONNECTING = 0;
  static OPEN = 1;
  static CLOSED = 2;

  constructor(url: string) {
    this.url = url;
    mockEventSourceInstances.push(this);
  }

  close() {
    this.readyState = 2;
    this.closed = true;
  }

  // Test helpers - called inside act()
  simulateOpen() {
    this.readyState = 1;
    this.onopen?.(new Event('open'));
  }

  simulateMessage(data: string, lastEventId?: string) {
    const event = new MessageEvent('message', {
      data,
      lastEventId: lastEventId || '',
    });
    this.onmessage?.(event);
  }

  simulateError() {
    this.onerror?.(new Event('error'));
  }
}

// ============================================================================
// Test Setup
// ============================================================================

describe('usePdfStatus', () => {
  const mockGetProgress = api.pdf.getProcessingProgress as ReturnType<typeof vi.fn>;

  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers();
    mockEventSourceInstances = [];

    // Wrap in vi.fn() so we can assert calls
    global.EventSource = vi.fn((url: string) => {
      return new MockEventSource(url);
    }) as unknown as typeof EventSource;
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  // helper to get the latest mock EventSource instance
  function getLatestES(): MockEventSource {
    return mockEventSourceInstances[mockEventSourceInstances.length - 1];
  }

  // ==========================================================================
  // SSE Connection
  // ==========================================================================

  describe('SSE Connection', () => {
    it('creates EventSource with correct URL', () => {
      renderHook(() => usePdfStatus('test-doc-123', { enableSSE: true }));

      expect(global.EventSource).toHaveBeenCalledWith(
        '/api/v1/pdfs/test-doc-123/status/stream'
      );
    });

    it('marks connection as connected on SSE open', async () => {
      const { result } = renderHook(() =>
        usePdfStatus('doc-1', { enableSSE: true })
      );

      expect(result.current.connectionState).toBe('connecting');

      await act(async () => {
        getLatestES().simulateOpen();
      });

      expect(result.current.isConnected).toBe(true);
      expect(result.current.connectionState).toBe('connected');
      expect(result.current.isPolling).toBe(false);
    });

    it('parses SSE message and updates status', async () => {
      const { result } = renderHook(() =>
        usePdfStatus('doc-1', { enableSSE: true })
      );

      await act(async () => {
        getLatestES().simulateOpen();
      });

      const messageData = JSON.stringify({
        state: 'embedding',
        progress: 65,
        eta: '2m 30s',
        timestamp: '2026-02-12T10:30:00Z',
      });

      await act(async () => {
        getLatestES().simulateMessage(messageData);
      });

      expect(result.current.status).toEqual({
        state: 'embedding',
        progress: 65,
        eta: '2m 30s',
        timestamp: '2026-02-12T10:30:00Z',
        errorMessage: undefined,
      });
    });

    it('closes connection on terminal state (ready)', async () => {
      const onComplete = vi.fn();
      const { result } = renderHook(() =>
        usePdfStatus('doc-1', { enableSSE: true, onComplete })
      );

      const es = getLatestES();

      await act(async () => {
        es.simulateOpen();
      });

      const readyMessage = JSON.stringify({
        state: 'ready',
        progress: 100,
        timestamp: '2026-02-12T10:35:00Z',
      });

      await act(async () => {
        es.simulateMessage(readyMessage);
      });

      expect(es.closed).toBe(true);
      expect(onComplete).toHaveBeenCalledOnce();
      expect(result.current.status?.state).toBe('ready');
    });

    it('closes connection on terminal state (failed)', async () => {
      const onComplete = vi.fn();
      renderHook(() =>
        usePdfStatus('doc-1', { enableSSE: true, onComplete })
      );

      const es = getLatestES();

      await act(async () => {
        es.simulateOpen();
      });

      const failedMessage = JSON.stringify({
        state: 'failed',
        progress: 30,
        timestamp: '2026-02-12T10:35:00Z',
        errorMessage: 'OCR failed',
      });

      await act(async () => {
        es.simulateMessage(failedMessage);
      });

      expect(es.closed).toBe(true);
      expect(onComplete).toHaveBeenCalledOnce();
    });

    it('calls onStateChange callback when state changes', async () => {
      const onStateChange = vi.fn();
      renderHook(() =>
        usePdfStatus('doc-1', { enableSSE: true, onStateChange })
      );

      await act(async () => {
        getLatestES().simulateOpen();
      });

      // First state change
      await act(async () => {
        getLatestES().simulateMessage(
          JSON.stringify({ state: 'extracting', progress: 20, timestamp: '2026-02-12T10:30:00Z' })
        );
      });
      expect(onStateChange).toHaveBeenCalledWith('extracting');

      // Same state - should NOT trigger again
      await act(async () => {
        getLatestES().simulateMessage(
          JSON.stringify({ state: 'extracting', progress: 25, timestamp: '2026-02-12T10:30:01Z' })
        );
      });
      expect(onStateChange).toHaveBeenCalledTimes(1);

      // Different state - should trigger
      await act(async () => {
        getLatestES().simulateMessage(
          JSON.stringify({ state: 'chunking', progress: 40, timestamp: '2026-02-12T10:30:02Z' })
        );
      });
      expect(onStateChange).toHaveBeenCalledTimes(2);
      expect(onStateChange).toHaveBeenCalledWith('chunking');
    });

    it('handles malformed SSE JSON gracefully', async () => {
      const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
      const { result } = renderHook(() =>
        usePdfStatus('doc-1', { enableSSE: true })
      );

      await act(async () => {
        getLatestES().simulateOpen();
      });

      await act(async () => {
        getLatestES().simulateMessage('not valid json {{{');
      });

      // Should not crash, status stays null
      expect(result.current.status).toBeNull();
      expect(consoleSpy).toHaveBeenCalled();

      consoleSpy.mockRestore();
    });
  });

  // ==========================================================================
  // Polling Fallback
  // ==========================================================================

  describe('Polling Fallback', () => {
    it('falls back to polling when SSE is disabled', async () => {
      mockGetProgress.mockResolvedValue({
        currentStep: 'Extracting',
        percentComplete: 30,
        estimatedTimeRemaining: '1m',
        errorMessage: null,
      });

      const { result } = renderHook(() =>
        usePdfStatus('doc-1', { enableSSE: false, pollingInterval: 5000 })
      );

      // Flush microtasks (initial poll fires via void poll())
      await act(async () => {
        await vi.advanceTimersByTimeAsync(1);
      });

      expect(result.current.isPolling).toBe(true);
      expect(result.current.isConnected).toBe(false);
      expect(result.current.connectionState).toBe('polling');
      expect(mockGetProgress).toHaveBeenCalledWith('doc-1');
    });

    it('falls back to polling after max SSE reconnect attempts', async () => {
      mockGetProgress.mockResolvedValue({
        currentStep: 'Uploading',
        percentComplete: 0,
        estimatedTimeRemaining: '5m',
        errorMessage: null,
      });

      const { result } = renderHook(() =>
        usePdfStatus('doc-1', { enableSSE: true, maxReconnectAttempts: 2 })
      );

      // First SSE error → reconnecting
      await act(async () => {
        getLatestES().simulateError();
      });
      expect(result.current.connectionState).toBe('reconnecting');

      // Wait for backoff (2s) then second attempt
      await act(async () => {
        await vi.advanceTimersByTimeAsync(2000);
      });

      // Second SSE error → reconnecting
      await act(async () => {
        getLatestES().simulateError();
      });
      expect(result.current.connectionState).toBe('reconnecting');

      // Wait for backoff (4s) then third attempt
      await act(async () => {
        await vi.advanceTimersByTimeAsync(4000);
      });

      // Third error → max reached → fallback to polling
      await act(async () => {
        getLatestES().simulateError();
      });

      // Flush microtasks for polling startup
      await act(async () => {
        await vi.advanceTimersByTimeAsync(1);
      });

      expect(result.current.isPolling).toBe(true);
    });

    it('stops polling on terminal state', async () => {
      mockGetProgress
        .mockResolvedValueOnce({
          currentStep: 'Embedding',
          percentComplete: 80,
          estimatedTimeRemaining: '30s',
          errorMessage: null,
        })
        .mockResolvedValueOnce({
          currentStep: 'Completed',
          percentComplete: 100,
          estimatedTimeRemaining: null,
          errorMessage: null,
        });

      const onComplete = vi.fn();

      renderHook(() =>
        usePdfStatus('doc-1', {
          enableSSE: false,
          pollingInterval: 5000,
          onComplete,
        })
      );

      // Initial poll
      await act(async () => {
        await vi.advanceTimersByTimeAsync(0);
      });

      // Next interval → terminal state
      await act(async () => {
        await vi.advanceTimersByTimeAsync(5000);
      });

      expect(onComplete).toHaveBeenCalledOnce();

      // Further polls should not fire
      mockGetProgress.mockClear();
      await act(async () => {
        await vi.advanceTimersByTimeAsync(10000);
      });
      expect(mockGetProgress).not.toHaveBeenCalled();
    });

    it('calls onError callback on polling failure', async () => {
      mockGetProgress.mockRejectedValue(new Error('Network error'));

      const onError = vi.fn();
      const { result } = renderHook(() =>
        usePdfStatus('doc-1', { enableSSE: false, onError })
      );

      // Flush microtasks for rejected promise
      await act(async () => {
        await vi.advanceTimersByTimeAsync(1);
      });

      expect(result.current.error).toBeTruthy();
      expect(result.current.error?.message).toBe('Network error');
      expect(onError).toHaveBeenCalledWith('Network error');
    });
  });

  // ==========================================================================
  // Cleanup
  // ==========================================================================

  describe('Cleanup', () => {
    it('closes EventSource on unmount', async () => {
      const { unmount } = renderHook(() =>
        usePdfStatus('doc-1', { enableSSE: true })
      );

      const es = getLatestES();

      await act(async () => {
        es.simulateOpen();
      });

      expect(es.closed).toBe(false);

      unmount();

      expect(es.closed).toBe(true);
    });

    it('clears polling interval on unmount', async () => {
      mockGetProgress.mockResolvedValue({
        currentStep: 'Uploading',
        percentComplete: 10,
        estimatedTimeRemaining: '5m',
        errorMessage: null,
      });

      const clearIntervalSpy = vi.spyOn(global, 'clearInterval');

      const { unmount } = renderHook(() =>
        usePdfStatus('doc-1', { enableSSE: false, pollingInterval: 5000 })
      );

      // Let initial poll run
      await act(async () => {
        await vi.advanceTimersByTimeAsync(0);
      });

      unmount();

      expect(clearIntervalSpy).toHaveBeenCalled();
    });
  });

  // ==========================================================================
  // Reconnection
  // ==========================================================================

  describe('Reconnection', () => {
    it('provides manual reconnect function', () => {
      const { result } = renderHook(() =>
        usePdfStatus('doc-1', { enableSSE: true })
      );

      expect(result.current.reconnect).toBeInstanceOf(Function);
    });

    it('reconnects with exponential backoff on SSE error', async () => {
      renderHook(() =>
        usePdfStatus('doc-1', { enableSSE: true, maxReconnectAttempts: 3 })
      );

      // Initial EventSource
      expect(mockEventSourceInstances).toHaveLength(1);

      // First error → reconnect after 2s (backoff attempt 1: 1000 * 2^1 = 2000)
      await act(async () => {
        getLatestES().simulateError();
      });

      await act(async () => {
        await vi.advanceTimersByTimeAsync(2000);
      });

      expect(mockEventSourceInstances).toHaveLength(2);

      // Second error → reconnect after 4s (backoff attempt 2: 1000 * 2^2 = 4000)
      await act(async () => {
        getLatestES().simulateError();
      });

      await act(async () => {
        await vi.advanceTimersByTimeAsync(4000);
      });

      expect(mockEventSourceInstances).toHaveLength(3);
    });

    it('manual reconnect resets attempt counter and creates new connection', async () => {
      const { result } = renderHook(() =>
        usePdfStatus('doc-1', { enableSSE: true })
      );

      const firstES = getLatestES();

      await act(async () => {
        firstES.simulateOpen();
      });

      // Manual reconnect
      await act(async () => {
        result.current.reconnect();
      });

      expect(firstES.closed).toBe(true);
      expect(mockEventSourceInstances.length).toBeGreaterThan(1);
    });

    it('updates reconnectionCount metric after successful reconnect', async () => {
      const { result } = renderHook(() =>
        usePdfStatus('doc-1', { enableSSE: true, maxReconnectAttempts: 5 })
      );

      expect(result.current.connectionMetrics.reconnectionCount).toBe(0);

      // Error → reconnect
      await act(async () => {
        getLatestES().simulateError();
      });

      await act(async () => {
        await vi.advanceTimersByTimeAsync(2000);
      });

      // Successful reconnect
      await act(async () => {
        getLatestES().simulateOpen();
      });

      expect(result.current.connectionMetrics.reconnectionCount).toBe(1);
    });
  });

  // ==========================================================================
  // Edge Cases
  // ==========================================================================

  describe('Edge Cases', () => {
    it('handles null documentId gracefully', () => {
      const { result } = renderHook(() => usePdfStatus(null));

      expect(result.current.status).toBeNull();
      expect(result.current.isConnected).toBe(false);
      expect(result.current.isPolling).toBe(false);
      expect(mockEventSourceInstances).toHaveLength(0);
    });

    it('does not create EventSource for null documentId', () => {
      renderHook(() => usePdfStatus(null, { enableSSE: true }));

      expect(mockEventSourceInstances).toHaveLength(0);
    });
  });

  // ==========================================================================
  // Properties (Issue #4211)
  // ==========================================================================

  describe('Properties (Issue #4211)', () => {
    it('exposes connectionState property', () => {
      const { result } = renderHook(() =>
        usePdfStatus('doc-1', { enableSSE: true })
      );

      expect(result.current.connectionState).toBeDefined();
      expect(
        ['connecting', 'connected', 'reconnecting', 'polling', 'failed']
      ).toContain(result.current.connectionState);
    });

    it('exposes connectionMetrics property', () => {
      const { result } = renderHook(() =>
        usePdfStatus('doc-1', { enableSSE: true })
      );

      expect(result.current.connectionMetrics).toBeDefined();
      expect(result.current.connectionMetrics).toHaveProperty('reconnectionCount');
      expect(result.current.connectionMetrics).toHaveProperty('fallbackTriggers');
      expect(result.current.connectionMetrics).toHaveProperty('lastConnectedAt');
      expect(result.current.connectionMetrics).toHaveProperty('connectionUptime');
    });

    it('accepts maxReconnectAttempts option', () => {
      const { result } = renderHook(() =>
        usePdfStatus('doc-1', { enableSSE: true, maxReconnectAttempts: 10 })
      );

      expect(result.current).toBeDefined();
    });

    it('updates lastConnectedAt on successful SSE connection', async () => {
      const { result } = renderHook(() =>
        usePdfStatus('doc-1', { enableSSE: true })
      );

      expect(result.current.connectionMetrics.lastConnectedAt).toBeNull();

      await act(async () => {
        getLatestES().simulateOpen();
      });

      expect(result.current.connectionMetrics.lastConnectedAt).not.toBeNull();
    });

    it('increments fallbackTriggers when switching to polling', async () => {
      mockGetProgress.mockResolvedValue({
        currentStep: 'Uploading',
        percentComplete: 0,
        estimatedTimeRemaining: '5m',
        errorMessage: null,
      });

      const { result } = renderHook(() =>
        usePdfStatus('doc-1', { enableSSE: false })
      );

      // Flush microtasks for polling startup
      await act(async () => {
        await vi.advanceTimersByTimeAsync(1);
      });

      expect(result.current.connectionMetrics.fallbackTriggers).toBe(1);
    });
  });
});
