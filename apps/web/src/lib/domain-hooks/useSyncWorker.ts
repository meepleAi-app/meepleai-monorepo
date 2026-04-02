/**
 * useSyncWorker — processes queued offline operations when online
 *
 * Game Session Flow v2.0 — Task 9
 *
 * Watches navigator.onLine and the SyncQueue store.
 * When back online, retries operations in order.
 * Operations that exceed MAX_RETRIES are discarded.
 */

'use client';

import { useEffect, useRef } from 'react';

import { api } from '@/lib/api';
import { useSyncQueueStore } from '@/lib/stores/sync-queue-store';

const MAX_RETRIES = 3;
const RETRY_DELAY_MS = 1500;

export function useSyncWorker() {
  const syncQueue = useSyncQueueStore(s => s.syncQueue);
  const isSyncing = useSyncQueueStore(s => s.isSyncing);
  const dequeue = useSyncQueueStore(s => s.dequeue);
  const incrementRetries = useSyncQueueStore(s => s.incrementRetries);
  const setIsSyncing = useSyncQueueStore(s => s.setIsSyncing);

  const processingRef = useRef(false);

  useEffect(() => {
    async function processQueue() {
      if (processingRef.current || isSyncing || !navigator.onLine) return;
      if (syncQueue.length === 0) return;

      processingRef.current = true;
      setIsSyncing(true);

      for (const op of syncQueue) {
        if (!navigator.onLine) break;

        incrementRetries(op.id);

        if (op.retries >= MAX_RETRIES) {
          dequeue(op.id);
          continue;
        }

        try {
          switch (op.type) {
            case 'advanceTurn':
              await api.liveSessions.advanceTurn(op.sessionId);
              break;
            case 'advancePhase':
              await api.liveSessions.advancePhase(op.sessionId);
              break;
            case 'recordScore':
              await api.liveSessions.recordScore(
                op.sessionId,
                op.payload as Parameters<typeof api.liveSessions.recordScore>[1]
              );
              break;
            case 'completeSession':
              await api.liveSessions.completeSession(op.sessionId);
              break;
          }
          dequeue(op.id);
        } catch {
          // Will retry on next cycle
          await new Promise(r => setTimeout(r, RETRY_DELAY_MS));
        }
      }

      setIsSyncing(false);
      processingRef.current = false;
    }

    const handleOnline = () => {
      void processQueue();
    };

    window.addEventListener('online', handleOnline);

    if (navigator.onLine && syncQueue.length > 0) {
      void processQueue();
    }

    return () => {
      window.removeEventListener('online', handleOnline);
    };
  }, [syncQueue, isSyncing, dequeue, incrementRetries, setIsSyncing]);
}
