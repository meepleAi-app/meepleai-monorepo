'use client';
/**
 * useResolvePlayRecord — Opzione C polling hook.
 * Issue #2503: after POST /complete, polls play-records history to find the
 * auto-created record for a given gameId. Backoff: 1s, 1.5s, 2s, 2.5s, 3s.
 * Timeout: ~15s. Returns status + resolved playRecordId.
 */
import { useCallback, useEffect, useRef, useState } from 'react';

import { api } from '@/lib/api';

export type ResolvePlayRecordStatus = 'idle' | 'resolving' | 'resolved' | 'timeout';

export interface UseResolvePlayRecordResult {
  status: ResolvePlayRecordStatus;
  playRecordId: string | null;
  start: (gameId: string) => void;
}

const BACKOFF_MS = [1000, 1500, 2000, 2500, 3000];
const TIMEOUT_MS = 15000;

export function useResolvePlayRecord(): UseResolvePlayRecordResult {
  const [status, setStatus] = useState<ResolvePlayRecordStatus>('idle');
  const [playRecordId, setPlayRecordId] = useState<string | null>(null);

  const gameIdRef = useRef<string | null>(null);
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const timeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const attemptRef = useRef(0);
  const cancelledRef = useRef(false);

  const cleanup = useCallback(() => {
    if (timerRef.current) clearTimeout(timerRef.current);
    if (timeoutRef.current) clearTimeout(timeoutRef.current);
    cancelledRef.current = true;
  }, []);

  useEffect(() => () => cleanup(), [cleanup]);

  const poll = useCallback(async () => {
    if (cancelledRef.current) return;
    const gameId = gameIdRef.current;
    if (!gameId) return;

    try {
      const response = await api.playRecords.getHistory({ gameId, pageSize: 1 });
      if (cancelledRef.current) return;
      if (response.records.length > 0) {
        setPlayRecordId(response.records[0].id);
        setStatus('resolved');
        cleanup();
        return;
      }
    } catch {
      // network error — retry on next backoff tick
    }

    if (cancelledRef.current) return;
    const delay = BACKOFF_MS[Math.min(attemptRef.current, BACKOFF_MS.length - 1)];
    attemptRef.current += 1;
    timerRef.current = setTimeout(() => void poll(), delay);
  }, [cleanup]);

  const start = useCallback(
    (gameId: string) => {
      cleanup();
      cancelledRef.current = false;
      attemptRef.current = 0;
      gameIdRef.current = gameId;
      setPlayRecordId(null);
      setStatus('resolving');

      timeoutRef.current = setTimeout(() => {
        if (!cancelledRef.current) {
          cancelledRef.current = true;
          setStatus('timeout');
        }
      }, TIMEOUT_MS);

      void poll();
    },
    [cleanup, poll]
  );

  return { status, playRecordId, start };
}
