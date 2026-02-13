/**
 * usePdfStatus Hook (Issue #4218)
 *
 * Real-time PDF status updates using Server-Sent Events with polling fallback.
 * Auto-reconnects SSE, falls back to polling on connection errors.
 *
 * Features:
 * - SSE connection for real-time updates
 * - Automatic polling fallback (5s interval)
 * - Auto-reconnect with exponential backoff
 * - Connection cleanup on unmount
 * - Callbacks: onStateChange, onComplete, onError
 */

import { useState, useEffect, useCallback, useRef } from 'react';

import { api } from '@/lib/api';
import { mapProcessingStepToPdfState } from '@/types/pdf';
import type { PdfState } from '@/types/pdf';
import type { ProcessingStep } from '@/types/pdf';

// ============================================================================
// Types
// ============================================================================

export interface PdfStatusData {
  state: PdfState;
  progress: number;
  eta?: string | null;
  timestamp: string;
  errorMessage?: string | null;
}

export interface UsePdfStatusOptions {
  /** Enable SSE (default: true) */
  enableSSE?: boolean;
  /** Polling interval in ms (default: 5000) */
  pollingInterval?: number;
  /** Callback when state changes */
  onStateChange?: (state: PdfState) => void;
  /** Callback when processing completes (ready or failed) */
  onComplete?: () => void;
  /** Callback on error */
  onError?: (error: string) => void;
}

export interface UsePdfStatusReturn {
  /** Current status data */
  status: PdfStatusData | null;
  /** True if SSE is connected */
  isConnected: boolean;
  /** True if using polling fallback */
  isPolling: boolean;
  /** Loading state */
  isLoading: boolean;
  /** Error state */
  error: Error | null;
  /** Manually reconnect */
  reconnect: () => void;
}

// ============================================================================
// Constants
// ============================================================================

const DEFAULT_POLLING_INTERVAL = 5000;
const SSE_RECONNECT_DELAY = 2000;
const MAX_RECONNECT_ATTEMPTS = 3;

// ============================================================================
// Helper Functions
// ============================================================================

function isTerminalState(state: PdfState): boolean {
  return state === 'ready' || state === 'failed';
}

// ============================================================================
// Hook Implementation
// ============================================================================

export function usePdfStatus(
  documentId: string | null,
  options: UsePdfStatusOptions = {}
): UsePdfStatusReturn {
  const {
    enableSSE = true,
    pollingInterval = DEFAULT_POLLING_INTERVAL,
    onStateChange,
    onComplete,
    onError,
  } = options;

  // State
  const [status, setStatus] = useState<PdfStatusData | null>(null);
  const [isConnected, setIsConnected] = useState(false);
  const [isPolling, setIsPolling] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<Error | null>(null);

  // Refs
  const eventSourceRef = useRef<EventSource | null>(null);
  const pollingIntervalRef = useRef<NodeJS.Timeout | null>(null);
  const reconnectAttemptsRef = useRef(0);
  const isMountedRef = useRef(true);
  const previousStateRef = useRef<PdfState | null>(null);

  // Store callbacks in refs to avoid effect dependencies
  const onStateChangeRef = useRef(onStateChange);
  const onCompleteRef = useRef(onComplete);
  const onErrorRef = useRef(onError);

  useEffect(() => {
    onStateChangeRef.current = onStateChange;
    onCompleteRef.current = onComplete;
    onErrorRef.current = onError;
  }, [onStateChange, onComplete, onError]);

  // ============================================================================
  // Polling Fallback
  // ============================================================================

  const startPolling = useCallback(() => {
    if (!documentId || !isMountedRef.current) return;

    setIsPolling(true);
    setIsConnected(false);

    const poll = async () => {
      try {
        setIsLoading(true);
        const data = await api.pdf.getProcessingProgress(documentId);

        if (!isMountedRef.current || !data) return;

        const pdfState = mapProcessingStepToPdfState(data.currentStep as ProcessingStep);
        const statusData: PdfStatusData = {
          state: pdfState,
          progress: data.percentComplete,
          eta: data.estimatedTimeRemaining,
          timestamp: new Date().toISOString(),
          errorMessage: data.errorMessage,
        };

        setStatus(statusData);
        setError(null);

        // Trigger callbacks
        if (previousStateRef.current !== pdfState) {
          onStateChangeRef.current?.(pdfState);
          previousStateRef.current = pdfState;
        }

        if (isTerminalState(pdfState)) {
          onCompleteRef.current?.();
          if (pollingIntervalRef.current) {
            clearInterval(pollingIntervalRef.current);
            pollingIntervalRef.current = null;
          }
        }
      } catch (err) {
        if (!isMountedRef.current) return;
        const error = err instanceof Error ? err : new Error('Polling failed');
        setError(error);
        onErrorRef.current?.(error.message);
      } finally {
        if (isMountedRef.current) {
          setIsLoading(false);
        }
      }
    };

    // Initial poll
    void poll();

    // Setup interval
    pollingIntervalRef.current = setInterval(poll, pollingInterval);
  }, [documentId, pollingInterval]);

  // ============================================================================
  // SSE Connection
  // ============================================================================

  const startSSE = useCallback(() => {
    if (!documentId || !isMountedRef.current) return;

    try {
      const eventSource = new EventSource(
        `/api/v1/pdfs/${documentId}/status/stream`
      );

      eventSourceRef.current = eventSource;

      eventSource.onopen = () => {
        if (!isMountedRef.current) return;
        setIsConnected(true);
        setIsPolling(false);
        setError(null);
        reconnectAttemptsRef.current = 0;
      };

      eventSource.onmessage = (e) => {
        if (!isMountedRef.current) return;

        try {
          const data = JSON.parse(e.data) as {
            state: string;
            progress: number;
            eta?: string;
            timestamp: string;
            errorMessage?: string;
          };

          const pdfState = data.state as PdfState;
          const statusData: PdfStatusData = {
            state: pdfState,
            progress: data.progress,
            eta: data.eta,
            timestamp: data.timestamp,
            errorMessage: data.errorMessage,
          };

          setStatus(statusData);

          // Trigger callbacks
          if (previousStateRef.current !== pdfState) {
            onStateChangeRef.current?.(pdfState);
            previousStateRef.current = pdfState;
          }

          if (isTerminalState(pdfState)) {
            onCompleteRef.current?.();
            eventSource.close();
          }
        } catch (err) {
          console.error('Failed to parse SSE message:', err);
        }
      };

      eventSource.onerror = () => {
        if (!isMountedRef.current) return;

        eventSource.close();
        setIsConnected(false);

        // Attempt reconnect or fallback to polling
        if (reconnectAttemptsRef.current < MAX_RECONNECT_ATTEMPTS) {
          reconnectAttemptsRef.current += 1;
          setTimeout(() => {
            if (isMountedRef.current) {
              startSSE();
            }
          }, SSE_RECONNECT_DELAY);
        } else {
          // Fallback to polling
          startPolling();
        }
      };
    } catch (err) {
      // SSE not supported, fallback to polling
      startPolling();
    }
  }, [documentId, startPolling]);

  // ============================================================================
  // Manual Reconnect
  // ============================================================================

  const reconnect = useCallback(() => {
    // Cleanup existing connections
    if (eventSourceRef.current) {
      eventSourceRef.current.close();
      eventSourceRef.current = null;
    }
    if (pollingIntervalRef.current) {
      clearInterval(pollingIntervalRef.current);
      pollingIntervalRef.current = null;
    }

    reconnectAttemptsRef.current = 0;
    setError(null);

    // Restart
    if (enableSSE) {
      startSSE();
    } else {
      startPolling();
    }
  }, [enableSSE, startSSE, startPolling]);

  // ============================================================================
  // Effect: Initialize Connection
  // ============================================================================

  useEffect(() => {
    isMountedRef.current = true;

    if (!documentId) return;

    if (enableSSE) {
      startSSE();
    } else {
      startPolling();
    }

    // Cleanup on unmount
    return () => {
      isMountedRef.current = false;

      if (eventSourceRef.current) {
        eventSourceRef.current.close();
        eventSourceRef.current = null;
      }

      if (pollingIntervalRef.current) {
        clearInterval(pollingIntervalRef.current);
        pollingIntervalRef.current = null;
      }
    };
  }, [documentId, enableSSE, startSSE, startPolling]);

  return {
    status,
    isConnected,
    isPolling,
    isLoading,
    error,
    reconnect,
  };
}
