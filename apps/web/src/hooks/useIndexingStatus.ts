/**
 * useIndexingStatus — polls GET /api/v1/knowledge-base/{gameId}/status
 * every 3 seconds until the game is indexed (Completed or Failed).
 */

import { useCallback, useEffect, useRef, useState } from 'react';

import { api } from '@/lib/api';
import type { KnowledgeBaseStatus } from '@/lib/api/schemas/knowledge-base.schemas';

const POLL_INTERVAL_MS = 3000;

export interface UseIndexingStatusOptions {
  /** Enable polling */
  enabled?: boolean;
  onReady?: () => void;
  onError?: (msg: string) => void;
}

export interface UseIndexingStatusReturn {
  status: KnowledgeBaseStatus | null;
  isLoading: boolean;
  isPolling: boolean;
  isReady: boolean;
  isFailed: boolean;
  chunkCount: number | null;
  error: string | null;
  refetch: () => void;
}

function isTerminal(s: KnowledgeBaseStatus): boolean {
  return s.status === 'Completed' || s.status === 'Failed';
}

export function useIndexingStatus(
  gameId: string | null | undefined,
  options: UseIndexingStatusOptions = {}
): UseIndexingStatusReturn {
  const { enabled = true, onReady, onError } = options;
  const [status, setStatus] = useState<KnowledgeBaseStatus | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const isPollingRef = useRef(false);
  const callbacksRef = useRef({ onReady, onError });
  callbacksRef.current = { onReady, onError };

  const fetch = useCallback(async () => {
    if (!gameId) return;
    try {
      const data = await api.knowledgeBase.getEmbeddingStatus(gameId);
      if (!data) return;
      setStatus(data);
      if (data.status === 'Completed') {
        isPollingRef.current = false;
        callbacksRef.current.onReady?.();
      } else if (data.status === 'Failed') {
        isPollingRef.current = false;
        callbacksRef.current.onError?.(data.errorMessage ?? 'Indicizzazione fallita');
      }
    } catch (e) {
      const msg = e instanceof Error ? e.message : 'Errore polling status';
      setError(msg);
    }
  }, [gameId]);

  const startPolling = useCallback(() => {
    if (!gameId || !enabled) return;
    isPollingRef.current = true;
    setIsLoading(true);

    const poll = async () => {
      await fetch();
      setIsLoading(false);
      if (isPollingRef.current) {
        timerRef.current = setTimeout(poll, POLL_INTERVAL_MS);
      }
    };
    poll();
  }, [gameId, enabled, fetch]);

  useEffect(() => {
    if (!gameId || !enabled) return;
    startPolling();
    return () => {
      isPollingRef.current = false;
      if (timerRef.current) clearTimeout(timerRef.current);
    };
  }, [gameId, enabled, startPolling]);

  const isReady = status?.status === 'Completed';
  const isFailed = status?.status === 'Failed';
  const shouldPoll = !!gameId && enabled && !!status && !isTerminal(status);

  return {
    status,
    isLoading,
    isPolling: shouldPoll,
    isReady,
    isFailed,
    chunkCount: status?.processedChunks ?? null,
    error,
    refetch: fetch,
  };
}
