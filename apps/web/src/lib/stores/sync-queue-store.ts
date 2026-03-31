/**
 * SyncQueue Store — offline operation queue with persistence
 *
 * Game Session Flow v2.0 — Task 8
 *
 * Queues API operations that fail due to network issues.
 * Persisted to localStorage via Zustand persist middleware.
 * Processed by useSyncWorker when network is restored.
 */

import { create } from 'zustand';
import { devtools, persist } from 'zustand/middleware';

export type SyncOperationType = 'advanceTurn' | 'advancePhase' | 'recordScore' | 'completeSession';

export interface SyncOperation {
  id: string;
  type: SyncOperationType;
  sessionId: string;
  payload: Record<string, unknown>;
  createdAt: number;
  retries: number;
}

interface SyncQueueState {
  syncQueue: SyncOperation[];
  isSyncing: boolean;

  // Actions
  enqueue: (op: Omit<SyncOperation, 'id' | 'createdAt' | 'retries'>) => void;
  dequeue: (id: string) => void;
  incrementRetries: (id: string) => void;
  setIsSyncing: (value: boolean) => void;
  clearQueue: () => void;
}

export const useSyncQueueStore = create<SyncQueueState>()(
  devtools(
    persist(
      set => ({
        syncQueue: [],
        isSyncing: false,

        enqueue: op =>
          set(
            state => ({
              syncQueue: [
                ...state.syncQueue,
                {
                  ...op,
                  id: `${op.type}-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`,
                  createdAt: Date.now(),
                  retries: 0,
                },
              ],
            }),
            false,
            'enqueue'
          ),

        dequeue: id =>
          set(
            state => ({
              syncQueue: state.syncQueue.filter(op => op.id !== id),
            }),
            false,
            'dequeue'
          ),

        incrementRetries: id =>
          set(
            state => ({
              syncQueue: state.syncQueue.map(op =>
                op.id === id ? { ...op, retries: op.retries + 1 } : op
              ),
            }),
            false,
            'incrementRetries'
          ),

        setIsSyncing: value => set({ isSyncing: value }, false, 'setIsSyncing'),

        clearQueue: () => set({ syncQueue: [] }, false, 'clearQueue'),
      }),
      {
        name: 'meepleai-sync-queue',
        // Persist only the queue, not transient syncing state
        partialize: state => ({ syncQueue: state.syncQueue }),
      }
    ),
    { name: 'sync-queue-store' }
  )
);
