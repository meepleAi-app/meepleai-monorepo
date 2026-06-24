'use client';
/**
 * useResolvePlayRecord — Opzione C polling hook.
 * Issue #2503: after POST /complete, polls play-records history to find the
 * auto-created record for a given gameId. Backoff: 1s, 1.5s, 2s, 2.5s, 3s.
 * Timeout: ~15s. Returns status + resolved playRecordId.
 *
 * **Stale-record guard (code-review CRITICAL 2)**: `PlayRecordSummary` carries
 * no `createdAt`, so the most-recent record for a game the user has played
 * before would resolve instantly to the WRONG (pre-existing) record. To avoid
 * that, `start()` takes the id of the most-recent record captured BEFORE the
 * POST /complete (`previousRecordId`); the poll resolves only when it observes
 * a record whose id differs from that baseline.
 *
 * **Instance guard (code-review IMPORTANT 3)**: each `start()` bumps an instance
 * counter captured by the poll loop, so an in-flight response from a superseded
 * poll cannot write state for the current run.
 */
import { useCallback, useEffect, useRef, useState } from 'react';

import { api } from '@/lib/api';

export type ResolvePlayRecordStatus = 'idle' | 'resolving' | 'resolved' | 'timeout';

export interface UseResolvePlayRecordResult {
  status: ResolvePlayRecordStatus;
  playRecordId: string | null;
  /**
   * Begin polling for the play-record auto-created for `gameId`.
   * @param gameId            game whose history to poll
   * @param previousRecordId  id of the most-recent record captured BEFORE the
   *                          POST /complete (or null if none); resolution skips
   *                          any record matching this id.
   */
  start: (gameId: string, previousRecordId?: string | null) => void;
}

const BACKOFF_MS = [1000, 1500, 2000, 2500, 3000];
const TIMEOUT_MS = 15000;

export function useResolvePlayRecord(): UseResolvePlayRecordResult {
  const [status, setStatus] = useState<ResolvePlayRecordStatus>('idle');
  const [playRecordId, setPlayRecordId] = useState<string | null>(null);

  const gameIdRef = useRef<string | null>(null);
  const previousIdRef = useRef<string | null>(null);
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const timeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const attemptRef = useRef(0);
  const cancelledRef = useRef(false);
  const instanceRef = useRef(0);

  const cleanup = useCallback(() => {
    if (timerRef.current) clearTimeout(timerRef.current);
    if (timeoutRef.current) clearTimeout(timeoutRef.current);
    cancelledRef.current = true;
  }, []);

  useEffect(() => () => cleanup(), [cleanup]);

  const poll = useCallback(
    async (instance: number) => {
      if (cancelledRef.current || instanceRef.current !== instance) return;
      const gameId = gameIdRef.current;
      if (!gameId) return;

      try {
        const response = await api.playRecords.getHistory({ gameId, pageSize: 1 });
        if (cancelledRef.current || instanceRef.current !== instance) return;
        const record = response.records[0];
        // Resolve only on a record distinct from the pre-complete baseline.
        if (record && record.id !== previousIdRef.current) {
          setPlayRecordId(record.id);
          setStatus('resolved');
          cleanup();
          return;
        }
      } catch {
        // network error — retry on next backoff tick
      }

      if (cancelledRef.current || instanceRef.current !== instance) return;
      const delay = BACKOFF_MS[Math.min(attemptRef.current, BACKOFF_MS.length - 1)];
      attemptRef.current += 1;
      timerRef.current = setTimeout(() => void poll(instance), delay);
    },
    [cleanup]
  );

  const start = useCallback(
    (gameId: string, previousRecordId: string | null = null) => {
      cleanup();
      const instance = instanceRef.current + 1;
      instanceRef.current = instance;
      cancelledRef.current = false;
      attemptRef.current = 0;
      gameIdRef.current = gameId;
      previousIdRef.current = previousRecordId;
      setPlayRecordId(null);
      setStatus('resolving');

      timeoutRef.current = setTimeout(() => {
        if (!cancelledRef.current && instanceRef.current === instance) {
          cancelledRef.current = true;
          setStatus('timeout');
        }
      }, TIMEOUT_MS);

      void poll(instance);
    },
    [cleanup, poll]
  );

  return { status, playRecordId, start };
}
