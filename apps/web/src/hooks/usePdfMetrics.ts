/**
 * usePdfMetrics Hook (Issue #4219)
 *
 * React hook for fetching PDF processing metrics with timing and ETA:
 * - Per-state duration tracking
 * - Progress percentage calculation
 * - Estimated time remaining
 * - Total processing duration
 * - Retry count tracking
 *
 * @example
 * ```tsx
 * function PdfMetricsDisplay({ documentId }: { documentId: string }) {
 *   const { metrics, isLoading, error, refetch } = usePdfMetrics(documentId);
 *
 *   if (isLoading) return <Skeleton />;
 *   if (error) return <ErrorMessage error={error} onRetry={refetch} />;
 *   if (!metrics) return null;
 *
 *   return (
 *     <div>
 *       <PdfProgressBar progress={metrics.progressPercentage} state={metrics.currentState} />
 *       {metrics.estimatedTimeRemaining && (
 *         <p>ETA: {formatDuration(metrics.estimatedTimeRemaining)}</p>
 *       )}
 *     </div>
 *   );
 * }
 * ```
 */

import { useState, useEffect, useCallback } from 'react';

import { api, type PdfMetrics } from '@/lib/api';

// ============================================================================
// Types
// ============================================================================

export interface UsePdfMetricsOptions {
  /** Enable/disable fetching (default: true when documentId is provided) */
  enabled?: boolean;
  /** Refetch interval in milliseconds (default: null - fetch once) */
  refetchInterval?: number;
}

export interface UsePdfMetricsReturn {
  /** PDF processing metrics, null if not yet fetched */
  metrics: PdfMetrics | null;
  /** True during initial fetch or when refetching */
  isLoading: boolean;
  /** Error object if fetch failed */
  error: Error | null;
  /** Manually trigger a refetch */
  refetch: () => void;
}

// ============================================================================
// Hook Implementation
// ============================================================================

export function usePdfMetrics(
  documentId: string | null,
  options: UsePdfMetricsOptions = {}
): UsePdfMetricsReturn {
  const { enabled = true, refetchInterval } = options;

  // State
  const [metrics, setMetrics] = useState<PdfMetrics | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<Error | null>(null);

  // Fetch metrics
  const fetchMetrics = useCallback(async () => {
    if (!documentId || !enabled) return;

    try {
      setIsLoading(true);
      setError(null);

      const data = await api.pdf.getMetrics(documentId);
      setMetrics(data);
    } catch (err) {
      const errorObj = err instanceof Error ? err : new Error('Failed to fetch PDF metrics');
      setError(errorObj);
    } finally {
      setIsLoading(false);
    }
  }, [documentId, enabled]);

  // Refetch function for manual triggers
  const refetch = useCallback(() => {
    setError(null);
    void fetchMetrics();
  }, [fetchMetrics]);

  // Auto-fetch on mount and when dependencies change
  useEffect(() => {
    if (!enabled || !documentId) return;

    // Initial fetch
    void fetchMetrics();

    // Setup interval if specified
    if (refetchInterval && refetchInterval > 0) {
      const intervalId = setInterval(() => {
        void fetchMetrics();
      }, refetchInterval);

      return () => clearInterval(intervalId);
    }
  }, [enabled, documentId, refetchInterval, fetchMetrics]);

  return {
    metrics,
    isLoading,
    error,
    refetch,
  };
}
