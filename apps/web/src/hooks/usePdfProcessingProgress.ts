/**
 * usePdfProcessingProgress Hook (Issue #3370)
 *
 * React hook for polling PDF processing progress with:
 * - Configurable polling interval (default 500ms)
 * - Auto-stop on completed/failed status
 * - Cleanup AbortController on unmount
 * - Retry logic on network errors (max 3 attempts)
 *
 * @example
 * ```tsx
 * function PdfProgressDisplay({ pdfId }: { pdfId: string }) {
 *   const { progress, isLoading, error, refetch } = usePdfProcessingProgress(pdfId, {
 *     onComplete: () => console.log('Processing complete!'),
 *     onError: (msg) => console.error('Processing failed:', msg),
 *   });
 *
 *   if (isLoading && !progress) return <Skeleton />;
 *   if (error) return <ErrorMessage error={error} onRetry={refetch} />;
 *   return <ProgressBar value={progress?.percentComplete ?? 0} />;
 * }
 * ```
 */

import { useState, useEffect, useCallback, useRef } from 'react';

import { api, type ProcessingProgress } from '@/lib/api';

// ============================================================================
// Constants
// ============================================================================

const DEFAULT_POLLING_INTERVAL_MS = 500;
const MAX_RETRY_ATTEMPTS = 3;
const RETRY_DELAY_MS = 1000;

// ============================================================================
// Types
// ============================================================================

export interface UsePdfProcessingProgressOptions {
  /** Polling interval in milliseconds (default: 500ms) */
  pollingInterval?: number;
  /** Enable/disable polling (default: true when pdfId is provided) */
  enabled?: boolean;
  /** Callback when processing completes successfully */
  onComplete?: () => void;
  /** Callback when processing fails with error message */
  onError?: (error: string) => void;
}

export interface UsePdfProcessingProgressReturn {
  /** Current processing progress data, null if not yet fetched */
  progress: ProcessingProgress | null;
  /** True during initial fetch or when refetching after error */
  isLoading: boolean;
  /** Error object if fetch failed after all retries */
  error: Error | null;
  /** Manually trigger a refetch */
  refetch: () => void;
}

/**
 * Terminal states that should stop polling
 */
function isTerminalState(step: string): boolean {
  return step === 'Completed' || step === 'Failed';
}

// ============================================================================
// Hook Implementation
// ============================================================================

export function usePdfProcessingProgress(
  pdfId: string | null,
  options: UsePdfProcessingProgressOptions = {}
): UsePdfProcessingProgressReturn {
  const {
    pollingInterval = DEFAULT_POLLING_INTERVAL_MS,
    enabled = true,
    onComplete,
    onError,
  } = options;

  // State
  const [progress, setProgress] = useState<ProcessingProgress | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<Error | null>(null);

  // Refs for cleanup and tracking
  const isMountedRef = useRef(true);
  const abortControllerRef = useRef<AbortController | null>(null);
  const retryCountRef = useRef(0);
  const intervalRef = useRef<NodeJS.Timeout | null>(null);
  const hasNotifiedCompletionRef = useRef(false);
  const hasNotifiedErrorRef = useRef(false);
  const progressRef = useRef<ProcessingProgress | null>(null);

  // Store callbacks in refs to avoid effect dependencies
  const onCompleteRef = useRef(onComplete);
  const onErrorRef = useRef(onError);
  useEffect(() => {
    onCompleteRef.current = onComplete;
    onErrorRef.current = onError;
  }, [onComplete, onError]);

  // Reset state when pdfId changes
  useEffect(() => {
    hasNotifiedCompletionRef.current = false;
    hasNotifiedErrorRef.current = false;
    progressRef.current = null;
    setProgress(null);
    setError(null);
    retryCountRef.current = 0;
  }, [pdfId]);

  // Fetch progress with retry logic
  const fetchProgress = useCallback(async () => {
    if (!pdfId || !isMountedRef.current) return;

    // Check if already in terminal state (use ref to avoid stale closure)
    if (progressRef.current && isTerminalState(progressRef.current.currentStep)) {
      return;
    }

    // Create new AbortController for this request
    abortControllerRef.current?.abort();
    abortControllerRef.current = new AbortController();

    try {
      setIsLoading(true);
      setError(null);

      const data = await api.pdf.getProcessingProgress(pdfId);

      if (!isMountedRef.current) return;

      if (data) {
        progressRef.current = data;
        setProgress(data);
        retryCountRef.current = 0; // Reset retry count on success

        // Check for completion
        if (data.currentStep === 'Completed' && !hasNotifiedCompletionRef.current) {
          hasNotifiedCompletionRef.current = true;
          onCompleteRef.current?.();
        }

        // Check for failure
        if (data.currentStep === 'Failed' && !hasNotifiedErrorRef.current) {
          hasNotifiedErrorRef.current = true;
          onErrorRef.current?.(data.errorMessage ?? 'Processing failed');
        }

        // Stop polling if terminal state
        if (isTerminalState(data.currentStep) && intervalRef.current) {
          clearInterval(intervalRef.current);
          intervalRef.current = null;
        }
      }
    } catch (err) {
      if (!isMountedRef.current) return;

      // Check if it's an abort error (ignore these)
      if (err instanceof Error && err.name === 'AbortError') {
        return;
      }

      // Retry logic
      if (retryCountRef.current < MAX_RETRY_ATTEMPTS) {
        retryCountRef.current += 1;
        // Schedule retry after delay
        setTimeout(() => {
          if (isMountedRef.current) {
            void fetchProgress();
          }
        }, RETRY_DELAY_MS);
        return;
      }

      // Max retries exceeded - set error state
      const errorObj = err instanceof Error ? err : new Error('Failed to fetch processing progress');
      setError(errorObj);
    } finally {
      if (isMountedRef.current) {
        setIsLoading(false);
      }
    }
  }, [pdfId]);

  // Refetch function for manual triggers
  const refetch = useCallback(() => {
    retryCountRef.current = 0;
    hasNotifiedCompletionRef.current = false;
    hasNotifiedErrorRef.current = false;
    setError(null);
    void fetchProgress();
  }, [fetchProgress]);

  // Setup polling effect
  useEffect(() => {
    // Reset mounted flag
    isMountedRef.current = true;

    // Clear existing interval
    if (intervalRef.current) {
      clearInterval(intervalRef.current);
      intervalRef.current = null;
    }

    // Don't poll if disabled or no pdfId
    if (!enabled || !pdfId) {
      return;
    }

    // Initial fetch
    void fetchProgress();

    // Setup polling interval
    intervalRef.current = setInterval(() => {
      if (!isMountedRef.current) return;

      // Stop polling if in terminal state
      if (progressRef.current && isTerminalState(progressRef.current.currentStep)) {
        if (intervalRef.current) {
          clearInterval(intervalRef.current);
          intervalRef.current = null;
        }
        return;
      }

      void fetchProgress();
    }, pollingInterval);

    // Cleanup on unmount or dependencies change
    return () => {
      isMountedRef.current = false;

      if (intervalRef.current) {
        clearInterval(intervalRef.current);
        intervalRef.current = null;
      }

      if (abortControllerRef.current) {
        abortControllerRef.current.abort();
        abortControllerRef.current = null;
      }
    };
  }, [enabled, pdfId, pollingInterval, fetchProgress]);

  return {
    progress,
    isLoading,
    error,
    refetch,
  };
}

// ============================================================================
// Re-export types for convenience
// ============================================================================

export type { ProcessingProgress } from '@/lib/api';
