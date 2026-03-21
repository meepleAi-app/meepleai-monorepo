/**
 * PdfMetricsDisplay Component (Issue #4219)
 *
 * Displays PDF processing metrics with progress bar and ETA.
 * Integrates PdfProgressBar with detailed metrics including:
 * - Progress percentage and state label
 * - Estimated time remaining (ETA)
 * - Total processing duration (optional)
 *
 * Features:
 * - Auto-polling via usePdfMetrics hook
 * - ETA display with human-readable format
 * - Graceful loading and error states
 * - WCAG 2.1 AA compliant
 */

'use client';

import { usePdfMetrics } from '@/hooks/usePdfMetrics';
import { formatETA, formatTimeSpan } from '@/lib/utils/formatTimeSpan';
import type { PdfState } from '@/types/pdf';

import { PdfProgressBar } from './PdfProgressBar';

// ============================================================================
// Types
// ============================================================================

export interface PdfMetricsDisplayProps {
  /** Document ID to fetch metrics for */
  documentId: string;
  /** Show ETA below progress bar (default: true) */
  showETA?: boolean;
  /** Show total duration (default: false) */
  showTotalDuration?: boolean;
  /** Auto-refresh interval in milliseconds (default: null - fetch once) */
  refreshInterval?: number;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Helper Functions
// ============================================================================

/**
 * Maps backend PdfProcessingState to frontend PdfState
 */
function mapStateToFrontend(backendState: string): PdfState {
  const stateMap: Record<string, PdfState> = {
    Pending: 'pending',
    Uploading: 'uploading',
    Extracting: 'extracting',
    Chunking: 'chunking',
    Embedding: 'embedding',
    Indexing: 'indexing',
    Ready: 'ready',
    Failed: 'failed',
  };

  return stateMap[backendState] ?? 'pending';
}

// ============================================================================
// Component
// ============================================================================

export function PdfMetricsDisplay({
  documentId,
  showETA = true,
  showTotalDuration = false,
  refreshInterval,
  className,
}: PdfMetricsDisplayProps) {
  const { metrics, isLoading, error, refetch } = usePdfMetrics(documentId, {
    enabled: true,
    refetchInterval: refreshInterval ?? undefined,
  });

  // Loading state
  if (isLoading && !metrics) {
    return (
      <div className={className} data-testid="pdf-metrics-loading">
        <div className="h-12 w-full animate-pulse rounded bg-gray-200" />
      </div>
    );
  }

  // Error state
  if (error) {
    return (
      <div className={className} data-testid="pdf-metrics-error">
        <p className="text-sm text-red-600">Failed to load metrics</p>
        <button
          onClick={refetch}
          className="mt-1 text-xs text-blue-600 hover:underline"
          type="button"
        >
          Retry
        </button>
      </div>
    );
  }

  // No data
  if (!metrics) {
    return null;
  }

  const frontendState = mapStateToFrontend(metrics.currentState);

  return (
    <div className={className} data-testid="pdf-metrics-display">
      {/* Progress Bar */}
      <PdfProgressBar
        state={frontendState}
        progress={metrics.progressPercentage}
        showLabel={true}
      />

      {/* ETA and Duration */}
      <div className="mt-2 flex items-center justify-between text-xs text-muted-foreground">
        {showETA && metrics.estimatedTimeRemaining && (
          <span data-testid="eta-display" aria-live="polite">
            {formatETA(metrics.estimatedTimeRemaining)} remaining
          </span>
        )}

        {showTotalDuration && metrics.totalDuration && (
          <span data-testid="total-duration-display">
            Total: {formatTimeSpan(metrics.totalDuration)}
          </span>
        )}

        {/* Retry count indicator */}
        {metrics.retryCount > 0 && (
          <span
            data-testid="retry-count"
            className="text-orange-600"
            title={`Retried ${metrics.retryCount} time(s)`}
          >
            Retry: {metrics.retryCount}
          </span>
        )}
      </div>
    </div>
  );
}

export default PdfMetricsDisplay;
