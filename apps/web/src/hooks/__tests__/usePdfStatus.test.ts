/**
 * usePdfStatus Hook Tests (Issue #4218)
 * Tests SSE connection, polling fallback, auto-reconnect, cleanup
 */

import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { usePdfStatus } from '../usePdfStatus';
import * as apiModule from '@/lib/api';

// Mock API
vi.mock('@/lib/api', () => ({
  api: {
    pdf: {
      getProcessingProgress: vi.fn(),
    },
  },
}));

// Mock EventSource
class MockEventSource {
  url: string;
  onopen: ((event: Event) => void) | null = null;
  onmessage: ((event: MessageEvent) => void) | null = null;
  onerror: ((event: Event) => void) | null = null;
  readyState = 0;

  constructor(url: string) {
    this.url = url;
    // Simulate async connection
    setTimeout(() => {
      this.readyState = 1;
      this.onopen?.(new Event('open'));
    }, 10);
  }

  close() {
    this.readyState = 2;
  }

  dispatchMessage(data: string) {
    this.onmessage?.(new MessageEvent('message', { data }));
  }

  dispatchError() {
    this.onerror?.(new Event('error'));
  }
}

describe('usePdfStatus', () => {
  let mockEventSource: MockEventSource;

  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers();

    // Mock global EventSource
    mockEventSource = new MockEventSource('');
    global.EventSource = vi.fn((url: string) => {
      mockEventSource = new MockEventSource(url);
      return mockEventSource as unknown as EventSource;
    }) as unknown as typeof EventSource;
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  describe('SSE Connection', () => {
    it('creates EventSource with correct URL', () => {
      const documentId = 'test-doc-123';
      renderHook(() => usePdfStatus(documentId, { enableSSE: true }));

      expect(global.EventSource).toHaveBeenCalledWith(
        `/api/v1/pdfs/${documentId}/status/stream`
      );
    });

    it('marks connection as connected on SSE open', async () => {
      const { result } = renderHook(() => usePdfStatus('doc-1', { enableSSE: true }));

      await waitFor(() => {
        expect(result.current.isConnected).toBe(true);
      });

      expect(result.current.isPolling).toBe(false);
    });

    it('parses SSE message and updates status', async () => {
      const { result } = renderHook(() => usePdfStatus('doc-1', { enableSSE: true }));

      await waitFor(() => expect(result.current.isConnected).toBe(true));

      const eventData = {
        state: 'embedding',
        progress: 75,
        eta: '00:01:30',
        timestamp: '2026-02-13T10:00:00Z',
      };

      mockEventSource.dispatchMessage(JSON.stringify(eventData));

      await waitFor(() => {
        expect(result.current.status).toEqual(eventData);
      });
    });

    it('closes connection on terminal state (ready)', async () => {
      const onComplete = vi.fn();
      const { result } = renderHook(() =>
        usePdfStatus('doc-1', { enableSSE: true, onComplete })
      );

      await waitFor(() => expect(result.current.isConnected).toBe(true));

      const readyEvent = {
        state: 'ready',
        progress: 100,
        timestamp: '2026-02-13T10:00:00Z',
      };

      mockEventSource.dispatchMessage(JSON.stringify(readyEvent));

      await waitFor(() => {
        expect(onComplete).toHaveBeenCalledTimes(1);
      });
    });

    it('calls onStateChange callback when state changes', async () => {
      const onStateChange = vi.fn();
      const { result } = renderHook(() =>
        usePdfStatus('doc-1', { enableSSE: true, onStateChange })
      );

      await waitFor(() => expect(result.current.isConnected).toBe(true));

      mockEventSource.dispatchMessage(
        JSON.stringify({ state: 'uploading', progress: 10, timestamp: new Date().toISOString() })
      );

      await waitFor(() => {
        expect(onStateChange).toHaveBeenCalledWith('uploading');
      });

      mockEventSource.dispatchMessage(
        JSON.stringify({ state: 'extracting', progress: 30, timestamp: new Date().toISOString() })
      );

      await waitFor(() => {
        expect(onStateChange).toHaveBeenCalledWith('extracting');
      });

      expect(onStateChange).toHaveBeenCalledTimes(2);
    });
  });

  describe('Polling Fallback', () => {
    it('falls back to polling when SSE is disabled', async () => {
      const mockProgress = {
        currentStep: 'Uploading',
        percentComplete: 25,
        estimatedTimeRemaining: '00:02:00',
        elapsedTime: '00:00:30',
        pagesProcessed: 2,
        totalPages: 10,
        startedAt: new Date().toISOString(),
        completedAt: null,
      };

      vi.mocked(apiModule.api.pdf.getProcessingProgress).mockResolvedValue(mockProgress);

      const { result } = renderHook(() => usePdfStatus('doc-1', { enableSSE: false }));

      await waitFor(() => {
        expect(result.current.isPolling).toBe(true);
        expect(result.current.isConnected).toBe(false);
      });

      await waitFor(() => {
        expect(result.current.status?.state).toBe('uploading');
        expect(result.current.status?.progress).toBe(25);
      });
    });

    it('falls back to polling after SSE errors', async () => {
      const mockProgress = {
        currentStep: 'Extracting',
        percentComplete: 40,
        estimatedTimeRemaining: null,
        elapsedTime: '00:01:00',
        pagesProcessed: 4,
        totalPages: 10,
        startedAt: new Date().toISOString(),
        completedAt: null,
      };

      vi.mocked(apiModule.api.pdf.getProcessingProgress).mockResolvedValue(mockProgress);

      const { result } = renderHook(() => usePdfStatus('doc-1', { enableSSE: true }));

      await waitFor(() => expect(result.current.isConnected).toBe(true));

      // Simulate SSE error
      mockEventSource.dispatchError();

      await waitFor(() => {
        expect(result.current.isConnected).toBe(false);
        expect(result.current.isPolling).toBe(true);
      });
    });
  });

  describe('Cleanup', () => {
    it('closes EventSource on unmount', async () => {
      const { unmount } = renderHook(() => usePdfStatus('doc-1', { enableSSE: true }));

      await waitFor(() => expect(mockEventSource.readyState).toBe(1));

      const closeSpy = vi.spyOn(mockEventSource, 'close');
      unmount();

      expect(closeSpy).toHaveBeenCalled();
    });

    it('clears polling interval on unmount', async () => {
      vi.mocked(apiModule.api.pdf.getProcessingProgress).mockResolvedValue({
        currentStep: 'Uploading',
        percentComplete: 10,
        estimatedTimeRemaining: null,
        elapsedTime: '00:00:10',
        pagesProcessed: 0,
        totalPages: 10,
        startedAt: new Date().toISOString(),
        completedAt: null,
      });

      const { unmount } = renderHook(() => usePdfStatus('doc-1', { enableSSE: false }));

      await waitFor(() => expect(apiModule.api.pdf.getProcessingProgress).toHaveBeenCalled());

      const callCountBefore = vi.mocked(apiModule.api.pdf.getProcessingProgress).mock.calls.length;

      unmount();

      // Advance timers and verify no more polling
      vi.advanceTimersByTime(10000);

      const callCountAfter = vi.mocked(apiModule.api.pdf.getProcessingProgress).mock.calls.length;
      expect(callCountAfter).toBe(callCountBefore); // No new calls after unmount
    });
  });

  describe('Reconnection', () => {
    it('provides manual reconnect function', async () => {
      const { result } = renderHook(() => usePdfStatus('doc-1', { enableSSE: true }));

      expect(result.current.reconnect).toBeInstanceOf(Function);
    });
  });

  describe('Edge Cases', () => {
    it('handles null documentId gracefully', () => {
      const { result } = renderHook(() => usePdfStatus(null));

      expect(result.current.status).toBeNull();
      expect(result.current.isConnected).toBe(false);
      expect(result.current.isPolling).toBe(false);
    });

    it('handles malformed SSE JSON gracefully', async () => {
      const { result } = renderHook(() => usePdfStatus('doc-1', { enableSSE: true }));

      await waitFor(() => expect(result.current.isConnected).toBe(true));

      // Send invalid JSON
      const consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
      mockEventSource.dispatchMessage('invalid json{');

      await waitFor(() => {
        expect(consoleErrorSpy).toHaveBeenCalled();
      });

      // Status should remain unchanged
      expect(result.current.status).toBeNull();

      consoleErrorSpy.mockRestore();
    });
  });

  describe('Exponential Backoff (Issue #4211)', () => {
    it('uses exponential backoff for reconnection attempts', async () => {
      const { result } = renderHook(() => usePdfStatus('doc-1', { enableSSE: true }));

      await waitFor(() => expect(result.current.isConnected).toBe(true));

      // Track delays between reconnection attempts
      const delays: number[] = [];
      const originalSetTimeout = global.setTimeout;
      vi.spyOn(global, 'setTimeout').mockImplementation(((cb: () => void, delay: number) => {
        if (delay >= 1000) { // Only track reconnection delays
          delays.push(delay);
        }
        return originalSetTimeout(cb, 0); // Execute immediately for test
      }) as typeof setTimeout);

      // Trigger multiple errors to test backoff sequence
      for (let i = 0; i < 5; i++) {
        mockEventSource.dispatchError();
        await vi.advanceTimersByTimeAsync(1);
      }

      // Verify exponential backoff: 1s, 2s, 4s, 8s, 16s
      expect(delays).toEqual([1000, 2000, 4000, 8000, 16000]);
    });

    it('caps backoff delay at 30 seconds', async () => {
      const { result } = renderHook(() => usePdfStatus('doc-1', { enableSSE: true }));

      await waitFor(() => expect(result.current.isConnected).toBe(true));

      const delays: number[] = [];
      vi.spyOn(global, 'setTimeout').mockImplementation(((cb: () => void, delay: number) => {
        if (delay >= 1000) {
          delays.push(delay);
        }
        return global.setTimeout(cb, 0);
      }) as typeof setTimeout);

      // Trigger many errors to exceed max delay
      for (let i = 0; i < 10; i++) {
        mockEventSource.dispatchError();
        await vi.advanceTimersByTimeAsync(1);
      }

      // All delays should be <= 30s
      expect(delays.every(d => d <= 30000)).toBe(true);
    });

    it('respects maxReconnectAttempts option (default 5)', async () => {
      const mockProgress = {
        currentStep: 'Extracting',
        percentComplete: 40,
        estimatedTimeRemaining: null,
        elapsedTime: '00:01:00',
        pagesProcessed: 4,
        totalPages: 10,
        startedAt: new Date().toISOString(),
        completedAt: null,
      };

      vi.mocked(apiModule.api.pdf.getProcessingProgress).mockResolvedValue(mockProgress);

      const { result } = renderHook(() => usePdfStatus('doc-1', { enableSSE: true }));

      await waitFor(() => expect(result.current.isConnected).toBe(true));

      // Trigger 5 errors
      for (let i = 0; i < 5; i++) {
        mockEventSource.dispatchError();
        await vi.advanceTimersByTimeAsync(1000);
      }

      // Should fallback to polling after 5 attempts
      await waitFor(() => {
        expect(result.current.isPolling).toBe(true);
        expect(result.current.isConnected).toBe(false);
      });
    });
  });

  describe('Connection State Tracking (Issue #4211)', () => {
    it('transitions through connection states correctly', async () => {
      const { result } = renderHook(() => usePdfStatus('doc-1', { enableSSE: true }));

      // Initial: connecting
      expect(result.current.connectionState).toBe('connecting');

      // After successful connection: connected
      await waitFor(() => {
        expect(result.current.connectionState).toBe('connected');
      });

      // After error: reconnecting
      mockEventSource.dispatchError();
      await vi.advanceTimersByTimeAsync(1000);

      await waitFor(() => {
        expect(result.current.connectionState).toBe('reconnecting');
      });
    });

    it('sets state to "polling" when fallback triggered', async () => {
      vi.mocked(apiModule.api.pdf.getProcessingProgress).mockResolvedValue({
        currentStep: 'Uploading',
        percentComplete: 10,
        estimatedTimeRemaining: null,
        elapsedTime: '00:00:10',
        pagesProcessed: 0,
        totalPages: 10,
        startedAt: new Date().toISOString(),
        completedAt: null,
      });

      const { result } = renderHook(() => usePdfStatus('doc-1', { enableSSE: false }));

      await waitFor(() => {
        expect(result.current.connectionState).toBe('polling');
      });
    });

    it('sets state to "failed" after max reconnect attempts', async () => {
      vi.mocked(apiModule.api.pdf.getProcessingProgress).mockResolvedValue({
        currentStep: 'Extracting',
        percentComplete: 40,
        estimatedTimeRemaining: null,
        elapsedTime: '00:01:00',
        pagesProcessed: 4,
        totalPages: 10,
        startedAt: new Date().toISOString(),
        completedAt: null,
      });

      const { result } = renderHook(() => usePdfStatus('doc-1', { enableSSE: true }));

      await waitFor(() => expect(result.current.isConnected).toBe(true));

      // Trigger max attempts (5)
      for (let i = 0; i < 5; i++) {
        mockEventSource.dispatchError();
        await vi.advanceTimersByTimeAsync(1000);
      }

      await waitFor(() => {
        expect(result.current.connectionState).toBe('failed');
      });
    });
  });

  describe('Connection Metrics (Issue #4211)', () => {
    it('initializes metrics with zero values', () => {
      const { result } = renderHook(() => usePdfStatus('doc-1', { enableSSE: true }));

      expect(result.current.connectionMetrics).toEqual({
        connectionUptime: 0,
        reconnectionCount: 0,
        fallbackTriggers: 0,
        lastConnectedAt: null,
      });
    });

    it('increments reconnectionCount on successful reconnect', async () => {
      const { result } = renderHook(() => usePdfStatus('doc-1', { enableSSE: true }));

      await waitFor(() => expect(result.current.isConnected).toBe(true));

      const initialCount = result.current.connectionMetrics.reconnectionCount;

      // Trigger error and reconnect
      mockEventSource.dispatchError();
      await vi.advanceTimersByTimeAsync(1000);

      await waitFor(() => {
        expect(result.current.connectionMetrics.reconnectionCount).toBeGreaterThan(initialCount);
      });
    });

    it('increments fallbackTriggers when polling starts', async () => {
      vi.mocked(apiModule.api.pdf.getProcessingProgress).mockResolvedValue({
        currentStep: 'Uploading',
        percentComplete: 10,
        estimatedTimeRemaining: null,
        elapsedTime: '00:00:10',
        pagesProcessed: 0,
        totalPages: 10,
        startedAt: new Date().toISOString(),
        completedAt: null,
      });

      const { result } = renderHook(() => usePdfStatus('doc-1', { enableSSE: true }));

      await waitFor(() => expect(result.current.isConnected).toBe(true));

      // Trigger fallback to polling (5 failures)
      for (let i = 0; i < 5; i++) {
        mockEventSource.dispatchError();
        await vi.advanceTimersByTimeAsync(1000);
      }

      await waitFor(() => {
        expect(result.current.connectionMetrics.fallbackTriggers).toBeGreaterThan(0);
      });
    });

    it('sets lastConnectedAt timestamp on successful connection', async () => {
      const { result } = renderHook(() => usePdfStatus('doc-1', { enableSSE: true }));

      await waitFor(() => {
        expect(result.current.connectionMetrics.lastConnectedAt).not.toBeNull();
        expect(new Date(result.current.connectionMetrics.lastConnectedAt!).getTime()).toBeGreaterThan(0);
      });
    });
  });

  describe('Network Detection (Issue #4211)', () => {
    it('falls back to polling for slow-2g network', async () => {
      // Mock navigator.connection
      Object.defineProperty(navigator, 'connection', {
        value: { effectiveType: 'slow-2g' },
        writable: true,
        configurable: true,
      });

      vi.mocked(apiModule.api.pdf.getProcessingProgress).mockResolvedValue({
        currentStep: 'Uploading',
        percentComplete: 10,
        estimatedTimeRemaining: null,
        elapsedTime: '00:00:10',
        pagesProcessed: 0,
        totalPages: 10,
        startedAt: new Date().toISOString(),
        completedAt: null,
      });

      const { result } = renderHook(() => usePdfStatus('doc-1', { enableSSE: true }));

      await waitFor(() => {
        expect(result.current.isPolling).toBe(true);
        expect(result.current.connectionState).toBe('polling');
      });

      // Cleanup
      delete (navigator as any).connection;
    });

    it('uses SSE for fast networks (4g)', async () => {
      Object.defineProperty(navigator, 'connection', {
        value: { effectiveType: '4g' },
        writable: true,
        configurable: true,
      });

      const { result } = renderHook(() => usePdfStatus('doc-1', { enableSSE: true }));

      await waitFor(() => {
        expect(result.current.isConnected).toBe(true);
        expect(result.current.connectionState).toBe('connected');
      });

      delete (navigator as any).connection;
    });
  });

  describe('Last-Event-ID Preservation (Issue #4211)', () => {
    it('preserves Last-Event-ID from SSE messages', async () => {
      const { result } = renderHook(() => usePdfStatus('doc-1', { enableSSE: true }));

      await waitFor(() => expect(result.current.isConnected).toBe(true));

      // Simulate SSE message with lastEventId
      const messageEvent = new MessageEvent('message', {
        data: JSON.stringify({
          state: 'extracting',
          progress: 50,
          timestamp: new Date().toISOString(),
        }),
        lastEventId: 'event-123',
      });

      mockEventSource.onmessage?.(messageEvent);

      // Trigger reconnection to verify lastEventId is used in URL
      mockEventSource.dispatchError();

      await waitFor(() => {
        // Verify EventSource was created with lastEventId in URL
        expect(global.EventSource).toHaveBeenCalledWith(
          expect.stringContaining('lastEventId=event-123')
        );
      });
    });
  });
});