/**
 * usePdfStatus Hook (Issue #4218 + #4211)
 *
 * Real-time PDF status updates using Server-Sent Events with polling fallback.
 * Auto-reconnects SSE with exponential backoff, falls back to polling on connection errors.
 *
 * Features:
 * - SSE connection for real-time updates
 * - Exponential backoff reconnection (1s, 2s, 4s, 8s, 16s, max 30s)
 * - Network detection for slow-2g auto-fallback
 * - Automatic polling fallback (5s interval)
 * - Connection state tracking
 * - Connection metrics (uptime, reconnect count)
 * - Last-Event-ID preservation (TODO: Issue #4211 Task #3)
 * - Callbacks: onStateChange, onComplete, onError
 */

import { useState, useEffect, useCallback, useRef } from 'react';

import { api } from '@/lib/api';
import { mapProcessingStepToPdfState } from '@/types/pdf';
import type { PdfState } from '@/types/pdf';
import type { ProcessingStep } from '@/types/pdf';

// ============================================================================
// Network Information API Types
// ============================================================================

interface NetworkInformation extends EventTarget {
  effectiveType: 'slow-2g' | '2g' | '3g' | '4g';
  downlink: number;
  rtt: number;
  saveData: boolean;
}

declare global {
  interface Navigator {
    connection?: NetworkInformation;
    mozConnection?: NetworkInformation;
    webkitConnection?: NetworkInformation;
  }
}

// ============================================================================
// Types
// ============================================================================

export type ConnectionState =
  | 'connecting'
  | 'connected'
  | 'reconnecting'
  | 'polling'
  | 'failed';

export interface ConnectionMetrics {
  /** Total uptime in milliseconds */
  connectionUptime: number;
  /** Number of reconnection attempts made */
  reconnectionCount: number;
  /** Number of times fallback to polling was triggered */
  fallbackTriggers: number;
  /** Timestamp of last successful connection */
  lastConnectedAt: string | null;
}

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
  /** Max reconnection attempts (default: 5) */
  maxReconnectAttempts?: number;
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
  /** Connection state */
  connectionState: ConnectionState;
  /** True if SSE is connected */
  isConnected: boolean;
  /** True if using polling fallback */
  isPolling: boolean;
  /** Loading state */
  isLoading: boolean;
  /** Error state */
  error: Error | null;
  /** Connection metrics */
  connectionMetrics: ConnectionMetrics;
  /** Manually reconnect */
  reconnect: () => void;
}

// ============================================================================
// Constants
// ============================================================================

const DEFAULT_POLLING_INTERVAL = 5000;
const MAX_RECONNECT_ATTEMPTS = 5;
const MAX_BACKOFF_DELAY = 30000; // 30 seconds
const INITIAL_BACKOFF_DELAY = 1000; // 1 second

// ============================================================================
// Helper Functions
// ============================================================================

function isTerminalState(state: PdfState): boolean {
  return state === 'ready' || state === 'failed';
}

/**
 * Calculate exponential backoff delay for reconnection attempts.
 * Sequence: 1s, 2s, 4s, 8s, 16s, max 30s
 */
function calculateBackoffDelay(attempt: number): number {
  const delay = INITIAL_BACKOFF_DELAY * Math.pow(2, attempt);
  return Math.min(delay, MAX_BACKOFF_DELAY);
}

/**
 * Check if network type requires polling fallback (slow-2g).
 * Uses Network Information API if available.
 */
function shouldUsePollingForNetwork(): boolean {
  if (typeof navigator === 'undefined') return false;
  
  const connection = navigator.connection || navigator.mozConnection || navigator.webkitConnection;
  
  if (!connection) return false;
  
  // Auto-fallback to polling for slow-2g
  return connection.effectiveType === 'slow-2g';
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
    maxReconnectAttempts = MAX_RECONNECT_ATTEMPTS,
    onStateChange,
    onComplete,
    onError,
  } = options;

  // State
  const [status, setStatus] = useState<PdfStatusData | null>(null);
  const [connectionState, setConnectionState] = useState<ConnectionState>('connecting');
  const [isConnected, setIsConnected] = useState(false);
  const [isPolling, setIsPolling] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<Error | null>(null);
  const [connectionMetrics, setConnectionMetrics] = useState<ConnectionMetrics>({
    connectionUptime: 0,
    reconnectionCount: 0,
    fallbackTriggers: 0,
    lastConnectedAt: null,
  });

  // Refs
  const eventSourceRef = useRef<EventSource | null>(null);
  const pollingIntervalRef = useRef<NodeJS.Timeout | null>(null);
  const reconnectAttemptsRef = useRef(0);
  const isMountedRef = useRef(true);
  const previousStateRef = useRef<PdfState | null>(null);
  const lastEventIdRef = useRef<string | null>(null);

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

    setConnectionState('polling');
    setIsPolling(true);
    setIsConnected(false);
    
    // Increment fallback triggers metric
    setConnectionMetrics(prev => ({
      ...prev,
      fallbackTriggers: prev.fallbackTriggers + 1,
    }));

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

    // Set connecting state
    setConnectionState(reconnectAttemptsRef.current > 0 ? 'reconnecting' : 'connecting');

    try {
      // Build URL with Last-Event-ID for stream resume
      let url = `/api/v1/pdfs/${documentId}/status/stream`;
      if (lastEventIdRef.current) {
        url += `?lastEventId=${encodeURIComponent(lastEventIdRef.current)}`;
      }

      const eventSource = new EventSource(url);

      eventSourceRef.current = eventSource;

      eventSource.onopen = () => {
        if (!isMountedRef.current) return;
        setConnectionState('connected');
        setIsConnected(true);
        setIsPolling(false);
        setError(null);
        
        // Save reconnect attempts before resetting
        const wasReconnecting = reconnectAttemptsRef.current > 0;
        reconnectAttemptsRef.current = 0;
        
        // Update metrics
        setConnectionMetrics(prev => ({
          ...prev,
          reconnectionCount: prev.reconnectionCount + (wasReconnecting ? 1 : 0),
          lastConnectedAt: new Date().toISOString(),
        }));
      };

      eventSource.onmessage = (e) => {
        if (!isMountedRef.current) return;

        // Preserve Last-Event-ID for stream resume
        if (e.lastEventId) {
          lastEventIdRef.current = e.lastEventId;
        }

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
        if (reconnectAttemptsRef.current < maxReconnectAttempts) {
          reconnectAttemptsRef.current += 1;
          setConnectionState('reconnecting');
          
          const delay = calculateBackoffDelay(reconnectAttemptsRef.current);
          setTimeout(() => {
            if (isMountedRef.current) {
              startSSE();
            }
          }, delay);
        } else {
          // Fallback to polling
          setConnectionState('failed');
          startPolling();
        }
      };
    } catch (err) {
      // SSE not supported, fallback to polling
      setConnectionState('failed');
      startPolling();
    }
  }, [documentId, startPolling, maxReconnectAttempts]);

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

    // Check network type - auto-fallback to polling for slow-2g
    const usePolling = !enableSSE || shouldUsePollingForNetwork();

    if (usePolling) {
      startPolling();
    } else {
      startSSE();
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
    connectionState,
    isConnected,
    isPolling,
    isLoading,
    error,
    connectionMetrics,
    reconnect,
  };
}