/**
 * useWizardProgressStream Hook
 *
 * Connects to the admin wizard SSE endpoint for real-time processing progress.
 * Auto-reconnects with exponential backoff, auto-closes on completion.
 */

import { useState, useEffect, useCallback, useRef } from 'react';

import { getApiBase } from '@/lib/api';

// ─── Types ───────────────────────────────────────────────────────────────────

export interface WizardProgressEvent {
  currentStep: string;
  pdfState: string;
  agentExists: boolean;
  overallPercent: number;
  message: string;
  isComplete: boolean;
  errorMessage: string | null;
  priority: string;
  timestamp: string;
}

export type StreamConnectionState = 'connecting' | 'connected' | 'reconnecting' | 'closed' | 'error';

export interface UseWizardProgressStreamReturn {
  progress: WizardProgressEvent | null;
  connectionState: StreamConnectionState;
  isComplete: boolean;
  isFailed: boolean;
  reconnect: () => void;
}

// ─── Constants ───────────────────────────────────────────────────────────────

const MAX_RECONNECT_ATTEMPTS = 5;
const INITIAL_BACKOFF_MS = 1000;
const MAX_BACKOFF_MS = 16000;

// ─── Hook ────────────────────────────────────────────────────────────────────

export function useWizardProgressStream(
  gameId: string | null
): UseWizardProgressStreamReturn {
  const [progress, setProgress] = useState<WizardProgressEvent | null>(null);
  const [connectionState, setConnectionState] = useState<StreamConnectionState>('connecting');

  const eventSourceRef = useRef<EventSource | null>(null);
  const reconnectAttemptsRef = useRef(0);
  const reconnectTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const isMountedRef = useRef(true);

  const isComplete = progress?.isComplete ?? false;
  const isFailed = progress?.pdfState === 'Failed';

  const cleanup = useCallback(() => {
    if (reconnectTimerRef.current) {
      clearTimeout(reconnectTimerRef.current);
      reconnectTimerRef.current = null;
    }
    if (eventSourceRef.current) {
      eventSourceRef.current.close();
      eventSourceRef.current = null;
    }
  }, []);

  const connect = useCallback(() => {
    if (!gameId || !isMountedRef.current) return;

    cleanup();
    setConnectionState('connecting');

    const url = `${getApiBase()}/api/v1/admin/games/wizard/${gameId}/progress/stream`;
    const eventSource = new EventSource(url, { withCredentials: true });
    eventSourceRef.current = eventSource;

    eventSource.addEventListener('progress', (e: MessageEvent) => {
      if (!isMountedRef.current) return;
      try {
        const data = JSON.parse(e.data) as WizardProgressEvent;
        setProgress(data);
        setConnectionState('connected');
        reconnectAttemptsRef.current = 0;
      } catch {
        // Parse error, ignore
      }
    });

    eventSource.addEventListener('complete', (e: MessageEvent) => {
      if (!isMountedRef.current) return;
      try {
        const data = JSON.parse(e.data) as WizardProgressEvent;
        setProgress(data);
        setConnectionState('closed');
        cleanup();
      } catch {
        // Parse error, ignore
      }
    });

    eventSource.addEventListener('error', (e: MessageEvent) => {
      if (!isMountedRef.current) return;
      try {
        const data = JSON.parse(e.data) as WizardProgressEvent;
        setProgress(data);
        setConnectionState('error');
        cleanup();
      } catch {
        // Parse error, ignore
      }
    });

    eventSource.onerror = () => {
      if (!isMountedRef.current) return;

      cleanup();

      if (reconnectAttemptsRef.current < MAX_RECONNECT_ATTEMPTS) {
        reconnectAttemptsRef.current += 1;
        setConnectionState('reconnecting');
        const delay = Math.min(
          INITIAL_BACKOFF_MS * Math.pow(2, reconnectAttemptsRef.current - 1),
          MAX_BACKOFF_MS
        );
        reconnectTimerRef.current = setTimeout(() => {
          if (isMountedRef.current) connect();
        }, delay);
      } else {
        setConnectionState('error');
      }
    };
  }, [gameId, cleanup]);

  const reconnect = useCallback(() => {
    reconnectAttemptsRef.current = 0;
    connect();
  }, [connect]);

  useEffect(() => {
    isMountedRef.current = true;

    if (gameId) {
      connect();
    }

    return () => {
      isMountedRef.current = false;
      cleanup();
    };
  }, [gameId, connect, cleanup]);

  return {
    progress,
    connectionState,
    isComplete,
    isFailed,
    reconnect,
  };
}
