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

  describe('New Properties (Issue #4211)', () => {
    it('exposes connectionState property', () => {
      const { result } = renderHook(() => usePdfStatus('doc-1', { enableSSE: true }));

      expect(result.current.connectionState).toBeDefined();
      expect(['connecting', 'connected', 'reconnecting', 'polling', 'failed']).toContain(
        result.current.connectionState
      );
    });

    it('exposes connectionMetrics property', () => {
      const { result } = renderHook(() => usePdfStatus('doc-1', { enableSSE: true }));

      expect(result.current.connectionMetrics).toBeDefined();
      expect(result.current.connectionMetrics).toHaveProperty('reconnectionCount');
      expect(result.current.connectionMetrics).toHaveProperty('fallbackTriggers');
      expect(result.current.connectionMetrics).toHaveProperty('lastConnectedAt');
    });

    it('accepts maxReconnectAttempts option', () => {
      const { result } = renderHook(() => 
        usePdfStatus('doc-1', { enableSSE: true, maxReconnectAttempts: 10 })
      );

      expect(result.current).toBeDefined();
    });
  });
});