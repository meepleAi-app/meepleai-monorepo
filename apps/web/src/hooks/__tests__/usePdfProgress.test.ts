/**
 * usePdfProgress Hook Tests (Issue #4210)
 *
 * Basic integration tests for usePdfProgress hook.
 * Full async/timer tests TODO: Follow-up PR
 */

import { renderHook } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { usePdfProgress } from '../usePdfProgress';

// ============================================================================
// Mocks
// ============================================================================

vi.mock('../usePdfStatus', () => ({
  usePdfStatus: vi.fn(() => ({
    status: {
      state: 'uploading',
      progress: 25,
      eta: '5m 0s',
      timestamp: new Date().toISOString(),
    },
    connectionState: 'connected',
    isConnected: true,
    isPolling: false,
    isLoading: false,
    error: null,
    connectionMetrics: {
      connectionUptime: 0,
      reconnectionCount: 0,
      fallbackTriggers: 0,
      lastConnectedAt: new Date().toISOString(),
    },
    reconnect: vi.fn(),
  })),
}));

vi.mock('@/lib/api', () => ({
  api: {
    pdf: {
      getMetrics: vi.fn().mockResolvedValue(null),
    },
  },
}));

import { usePdfStatus } from '../usePdfStatus';
import { api } from '@/lib/api';

const mockedUsePdfStatus = vi.mocked(usePdfStatus);
const mockedGetMetrics = vi.mocked(api.pdf.getMetrics);

// ============================================================================
// Tests
// ============================================================================

describe('usePdfProgress', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('SSE Status Integration', () => {
    it('should return status from usePdfStatus', () => {
      const { result } = renderHook(() => usePdfProgress('doc-123'));

      expect(result.current.status?.state).toBe('uploading');
      expect(result.current.status?.progress).toBe(25);
      expect(result.current.isConnected).toBe(true);
    });

    it('should pass options to usePdfStatus', () => {
      const onStateChange = vi.fn();

      renderHook(() =>
        usePdfProgress('doc-123', {
          onStateChange,
        })
      );

      expect(mockedUsePdfStatus).toHaveBeenCalledWith('doc-123', {
        onStateChange,
      });
    });

    it('should expose reconnect function', () => {
      const mockReconnect = vi.fn();
      mockedUsePdfStatus.mockReturnValue({
        status: null,
        isConnected: false,
        isPolling: false,
        isLoading: false,
        error: null,
        reconnect: mockReconnect,
      });

      const { result } = renderHook(() => usePdfProgress('doc-123'));

      result.current.reconnect();
      expect(mockReconnect).toHaveBeenCalled();
    });

    it('should expose refreshMetrics function', () => {
      const { result } = renderHook(() => usePdfProgress('doc-123'));

      expect(result.current.refreshMetrics).toBeDefined();
      expect(typeof result.current.refreshMetrics).toBe('function');
    });
  });

  describe('Metrics Properties', () => {
    it('should expose metrics state properties', () => {
      const { result } = renderHook(() => usePdfProgress('doc-123'));

      expect(result.current).toHaveProperty('metrics');
      expect(result.current).toHaveProperty('metricsLoading');
      expect(result.current).toHaveProperty('metricsError');
    });

    it('should not call API if documentId is null', () => {
      renderHook(() => usePdfProgress(null));

      expect(mockedGetMetrics).not.toHaveBeenCalled();
    });
  });
});