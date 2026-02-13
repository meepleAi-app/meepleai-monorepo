/**
 * usePdfProgress Hook (Issue #4210)
 *
 * Unified PDF progress hook combining real-time SSE updates with detailed metrics.
 * Extends usePdfStatus (Issue #4218) with PdfMetrics polling (Issue #4219).
 *
 * Features:
 * - Real-time state/progress via SSE (usePdfStatus)
 * - Detailed metrics via polling (pageCount, stateDurations, retryCount)
 * - Unified data model for UI components
 * - Auto-cleanup and error handling
 *
 * @example
 * ```tsx
 * const { status, metrics, isConnected } = usePdfProgress(documentId, {
 *   onComplete: () => toast.success('PDF ready!'),
 *   onError: (err) => toast.error(err),
 * });
 *
 * return (
 *   <div>
 *     <Progress value={status?.progress ?? 0} />
 *     <p>Pages: {metrics?.pageCount ?? 'N/A'}</p>
 *     <p>ETA: {metrics?.estimatedTimeRemaining ?? 'Calculating...'}</p>
 *   </div>
 * );
 * ```
 */

import { useState, useEffect, useCallback, useRef } from 'react';

import { api } from '@/lib/api';
import type { PdfMetrics } from '@/lib/api';

import { usePdfStatus, type UsePdfStatusOptions, type PdfStatusData } from './usePdfStatus';

// ============================================================================
// Types
// ============================================================================

export interface UsePdfProgressOptions extends UsePdfStatusOptions {
  /** Enable metrics polling (default: true) */
  enableMetrics?: boolean;
  /** Metrics polling interval in ms (default: 3000) */
  metricsInterval?: number;
}

export interface UsePdfProgressReturn {
  // SSE Status (from usePdfStatus)
  /** Current status from SSE stream */
  status: PdfStatusData | null;
  /** True if SSE is connected */
  isConnected: boolean;
  /** True if using polling fallback for status */
  isPolling: boolean;
  /** Loading state for status */
  isLoading: boolean;
  /** Error state for status */
  error: Error | null;
  /** Manually reconnect SSE */
  reconnect: () => void;

  // Metrics (from PdfMetrics API)
  /** Detailed metrics (pageCount, stateDurations, retryCount) */
  metrics: PdfMetrics | null;
  /** Loading state for metrics */
  metricsLoading: boolean;
  /** Error state for metrics */
  metricsError: Error | null;
  /** Manually refresh metrics */
  refreshMetrics: () => Promise<void>;
}

// ============================================================================
// Constants
// ============================================================================

const DEFAULT_METRICS_INTERVAL = 3000; // 3 seconds

// ============================================================================
// Helper Functions
// ============================================================================

function isTerminalState(state: string): boolean {
  return state === 'ready' || state === 'failed';
}

// ============================================================================
// Hook Implementation
// ============================================================================

export function usePdfProgress(
  documentId: string | null,
  options: UsePdfProgressOptions = {}
): UsePdfProgressReturn {
  const {
    enableMetrics = true,
    metricsInterval = DEFAULT_METRICS_INTERVAL,
    ...statusOptions
  } = options;

  // ============================================================================
  // SSE Status (usePdfStatus)
  // ============================================================================

  const pdfStatus = usePdfStatus(documentId, statusOptions);

  // ============================================================================
  // Metrics Polling
  // ============================================================================

  const [metrics, setMetrics] = useState<PdfMetrics | null>(null);
  const [metricsLoading, setMetricsLoading] = useState(false);
  const [metricsError, setMetricsError] = useState<Error | null>(null);

  const metricsIntervalRef = useRef<NodeJS.Timeout | null>(null);
  const isMountedRef = useRef(true);

  // Fetch metrics - using refs to avoid dependency issues
  const fetchMetricsRef = useRef<(() => Promise<void>) | null>(null);

  fetchMetricsRef.current = async () => {
    if (!documentId || !enableMetrics || !isMountedRef.current) return;

    try {
      setMetricsLoading(true);
      const data = await api.pdf.getMetrics(documentId);

      if (!isMountedRef.current || !data) return;

      setMetrics(data);
      setMetricsError(null);

      // Stop polling if terminal state
      if (isTerminalState(data.currentState)) {
        if (metricsIntervalRef.current) {
          clearInterval(metricsIntervalRef.current);
          metricsIntervalRef.current = null;
        }
      }
    } catch (err) {
      if (!isMountedRef.current) return;
      const error = err instanceof Error ? err : new Error('Failed to fetch metrics');
      setMetricsError(error);
      console.error('[usePdfProgress] Metrics fetch failed:', error);
    } finally {
      if (isMountedRef.current) {
        setMetricsLoading(false);
      }
    }
  };

  // Manual refresh
  const refreshMetrics = useCallback(async () => {
    await fetchMetricsRef.current?.();
  }, []);

  // ============================================================================
  // Effect: Start Metrics Polling
  // ============================================================================

  useEffect(() => {
    isMountedRef.current = true;

    if (!documentId || !enableMetrics) return;

    // Initial fetch
    void fetchMetricsRef.current?.();

    // Setup polling interval
    metricsIntervalRef.current = setInterval(() => {
      void fetchMetricsRef.current?.();
    }, metricsInterval);

    // Cleanup
    return () => {
      isMountedRef.current = false;

      if (metricsIntervalRef.current) {
        clearInterval(metricsIntervalRef.current);
        metricsIntervalRef.current = null;
      }
    };
  }, [documentId, enableMetrics, metricsInterval]);

  // ============================================================================
  // Return Combined Data
  // ============================================================================

  return {
    // SSE Status
    status: pdfStatus.status,
    isConnected: pdfStatus.isConnected,
    isPolling: pdfStatus.isPolling,
    isLoading: pdfStatus.isLoading,
    error: pdfStatus.error,
    reconnect: pdfStatus.reconnect,

    // Metrics
    metrics,
    metricsLoading,
    metricsError,
    refreshMetrics,
  };
}