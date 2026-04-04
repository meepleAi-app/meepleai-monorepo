/**
 * useEmbeddingStatus Hook (Issue #4065)
 *
 * React Query-based hook for polling Knowledge Base embedding status.
 * Polls GET /api/v1/knowledge-base/{gameId}/status every 2 seconds,
 * auto-stops on Completed or Failed status.
 *
 * @example
 * ```tsx
 * const { data, isPolling, isReady, isFailed } = useEmbeddingStatus(gameId, {
 *   enabled: uploadComplete,
 *   onReady: () => showNotification('KB ready!'),
 * });
 * ```
 */

import { useCallback, useEffect, useRef, useState } from 'react';

import { api } from '@/lib/api';
import type {
  EmbeddingStatus,
  KnowledgeBaseStatus,
} from '@/lib/api/schemas/knowledge-base.schemas';

// ============================================================================
// Constants
// ============================================================================

const DEFAULT_POLLING_INTERVAL_MS = 2000;
const MAX_RETRY_ATTEMPTS = 3;
const RETRY_DELAY_MS = 1500;

// ============================================================================
// Types
// ============================================================================

export interface UseEmbeddingStatusOptions {
  /** Enable/disable polling (default: true when gameId is provided) */
  enabled?: boolean;
  /** Polling interval in milliseconds (default: 2000ms) */
  pollingInterval?: number;
  /** Callback when embedding completes (RAG ready) */
  onReady?: (gameName?: string) => void;
  /** Callback when embedding fails */
  onError?: (errorMessage: string) => void;
}

export interface UseEmbeddingStatusReturn {
  /** Current KB status data */
  data: KnowledgeBaseStatus | null;
  /** True during initial fetch */
  isLoading: boolean;
  /** True while actively polling (not in terminal state) */
  isPolling: boolean;
  /** True when status is Completed (RAG is ready) */
  isReady: boolean;
  /** True when status is Failed */
  isFailed: boolean;
  /** Current embedding stage label */
  stageLabel: string;
  /** Human-readable chunk progress (e.g., "87/120") */
  chunkProgress: string;
  /** Error object if fetch failed */
  error: Error | null;
  /** Manually trigger a refetch */
  refetch: () => void;
}

// ============================================================================
// Helpers
// ============================================================================

function isTerminalStatus(status: EmbeddingStatus): boolean {
  return status === 'Completed' || status === 'Failed';
}

const STAGE_LABELS: Record<EmbeddingStatus, string> = {
  Pending: 'In attesa...',
  Extracting: 'Estrazione del testo dalle pagine...',
  Chunking: 'Creazione dei chunks semantici...',
  Embedding: 'Generazione degli embeddings vettoriali...',
  Completed: 'Knowledge Base pronta!',
  Failed: 'Elaborazione fallita',
};

function getStageLabel(status: EmbeddingStatus, data: KnowledgeBaseStatus | null): string {
  if (!data) return STAGE_LABELS.Pending;

  const base = STAGE_LABELS[status];

  if (status === 'Chunking' && data.totalChunks > 0) {
    return `Creazione chunks: ${data.processedChunks}/${data.totalChunks}`;
  }
  if (status === 'Embedding' && data.totalChunks > 0) {
    return `Generazione embeddings: ${data.processedChunks}/${data.totalChunks} (${data.progress}%)`;
  }

  return base;
}

function getChunkProgress(data: KnowledgeBaseStatus | null): string {
  if (!data || data.totalChunks === 0) return '0/0';
  return `${data.processedChunks}/${data.totalChunks}`;
}

// ============================================================================
// Hook Implementation
// ============================================================================

export function useEmbeddingStatus(
  gameId: string | null,
  options: UseEmbeddingStatusOptions = {}
): UseEmbeddingStatusReturn {
  const {
    enabled = true,
    pollingInterval = DEFAULT_POLLING_INTERVAL_MS,
    onReady,
    onError,
  } = options;

  const [data, setData] = useState<KnowledgeBaseStatus | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<Error | null>(null);

  // Refs for cleanup and deduplication
  const isMountedRef = useRef(true);
  const intervalRef = useRef<NodeJS.Timeout | null>(null);
  const retryCountRef = useRef(0);
  const hasNotifiedReadyRef = useRef(false);
  const hasNotifiedErrorRef = useRef(false);
  const dataRef = useRef<KnowledgeBaseStatus | null>(null);

  // Stable callback refs
  const onReadyRef = useRef(onReady);
  const onErrorRef = useRef(onError);
  useEffect(() => {
    onReadyRef.current = onReady;
    onErrorRef.current = onError;
  }, [onReady, onError]);

  // Reset on gameId change
  useEffect(() => {
    hasNotifiedReadyRef.current = false;
    hasNotifiedErrorRef.current = false;
    dataRef.current = null;
    setData(null);
    setError(null);
    retryCountRef.current = 0;
  }, [gameId]);

  const fetchStatus = useCallback(async () => {
    if (!gameId || !isMountedRef.current) return;

    // Skip if already in terminal state
    if (dataRef.current && isTerminalStatus(dataRef.current.status)) return;

    try {
      setIsLoading(true);
      setError(null);

      const result = await api.knowledgeBase.getEmbeddingStatus(gameId);

      if (!isMountedRef.current) return;

      if (result) {
        dataRef.current = result;
        setData(result);
        retryCountRef.current = 0;

        // Notify on completion
        if (result.status === 'Completed' && !hasNotifiedReadyRef.current) {
          hasNotifiedReadyRef.current = true;
          onReadyRef.current?.(result.gameName);
        }

        // Notify on failure
        if (result.status === 'Failed' && !hasNotifiedErrorRef.current) {
          hasNotifiedErrorRef.current = true;
          onErrorRef.current?.(result.errorMessage ?? 'Embedding failed');
        }

        // Stop polling on terminal state
        if (isTerminalStatus(result.status) && intervalRef.current) {
          clearInterval(intervalRef.current);
          intervalRef.current = null;
        }
      }
    } catch (err) {
      if (!isMountedRef.current) return;

      if (retryCountRef.current < MAX_RETRY_ATTEMPTS) {
        retryCountRef.current += 1;
        setTimeout(() => {
          if (isMountedRef.current) void fetchStatus();
        }, RETRY_DELAY_MS);
        return;
      }

      setError(err instanceof Error ? err : new Error('Failed to fetch embedding status'));
    } finally {
      if (isMountedRef.current) setIsLoading(false);
    }
  }, [gameId]);

  const refetch = useCallback(() => {
    retryCountRef.current = 0;
    hasNotifiedReadyRef.current = false;
    hasNotifiedErrorRef.current = false;
    setError(null);
    void fetchStatus();
  }, [fetchStatus]);

  // Polling effect
  useEffect(() => {
    isMountedRef.current = true;

    if (intervalRef.current) {
      clearInterval(intervalRef.current);
      intervalRef.current = null;
    }

    if (!enabled || !gameId) return;

    void fetchStatus();

    intervalRef.current = setInterval(() => {
      if (!isMountedRef.current) return;
      if (dataRef.current && isTerminalStatus(dataRef.current.status)) {
        if (intervalRef.current) {
          clearInterval(intervalRef.current);
          intervalRef.current = null;
        }
        return;
      }
      void fetchStatus();
    }, pollingInterval);

    return () => {
      isMountedRef.current = false;
      if (intervalRef.current) {
        clearInterval(intervalRef.current);
        intervalRef.current = null;
      }
    };
  }, [enabled, gameId, pollingInterval, fetchStatus]);

  const currentStatus = data?.status ?? 'Pending';

  return {
    data,
    isLoading: isLoading && !data,
    isPolling: enabled && !!gameId && !isTerminalStatus(currentStatus) && !error,
    isReady: currentStatus === 'Completed',
    isFailed: currentStatus === 'Failed',
    stageLabel: getStageLabel(currentStatus, data),
    chunkProgress: getChunkProgress(data),
    error,
    refetch,
  };
}
