import { useEffect, useRef, useState, useCallback } from 'react';

import type { BackendTogglesState } from '@/dev-tools/types';

import { devPanelClient, DevPanelClientError } from '../api/devPanelClient';

const BACKOFF_DELAYS_MS = [10_000, 30_000, 60_000, 300_000] as const;

export interface UseBackendTogglesQueryResult {
  toggles: Record<string, boolean>;
  knownServices: string[];
  isLoading: boolean;
  error: DevPanelClientError | null;
  refetch: () => Promise<void>;
}

export function useBackendTogglesQuery(): UseBackendTogglesQueryResult {
  const [state, setState] = useState<BackendTogglesState>({ toggles: {}, knownServices: [] });
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<DevPanelClientError | null>(null);
  const retryCountRef = useRef(0);
  const retryTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const fetchOnce = useCallback(async () => {
    setIsLoading(true);
    try {
      const data = await devPanelClient.getToggles();
      setState(data);
      setError(null);
      retryCountRef.current = 0;
    } catch (err) {
      const e = err instanceof DevPanelClientError ? err : new DevPanelClientError(String(err), 0);
      setError(e);
      const idx = retryCountRef.current;
      if (idx < BACKOFF_DELAYS_MS.length) {
        const delay = BACKOFF_DELAYS_MS[idx];
        retryCountRef.current = idx + 1;
        retryTimerRef.current = setTimeout(() => {
          void fetchOnce();
        }, delay);
      }
    } finally {
      setIsLoading(false);
    }
  }, []);

  const refetch = useCallback(async () => {
    if (retryTimerRef.current) {
      clearTimeout(retryTimerRef.current);
      retryTimerRef.current = null;
    }
    retryCountRef.current = 0;
    await fetchOnce();
  }, [fetchOnce]);

  useEffect(() => {
    void fetchOnce();
    return () => {
      if (retryTimerRef.current) {
        clearTimeout(retryTimerRef.current);
      }
    };
  }, [fetchOnce]);

  return { toggles: state.toggles, knownServices: state.knownServices, isLoading, error, refetch };
}
