/**
 * FE-IMP-008: Multi-file upload queue hook (Web Worker + useSyncExternalStore)
 * Manages concurrent PDF uploads with progress tracking, retry, and cancellation
 *
 * BREAKING CHANGES from legacy version:
 * - Now uses Web Worker for off-main-thread processing
 * - Queue persists across page refreshes (localStorage)
 * - Multi-tab synchronization via BroadcastChannel
 * - Uses React 19's useSyncExternalStore for zero-tearing state
 *
 * Backward compatible API with legacy useUploadQueue
 */

import { useSyncExternalStore, useEffect, useCallback } from 'react';
import {
  uploadQueueStore,
  type UploadQueueItem,
  type UploadQueueStats,
  type UploadStatus,
  type UseUploadQueueOptions
} from '../stores/UploadQueueStore';

export type { UploadStatus, UploadQueueItem, UploadQueueStats };

export interface UseUploadQueueReturn {
  queue: UploadQueueItem[];
  addFiles: (files: File[], gameId: string, language: string) => Promise<void>;
  removeFile: (id: string) => void;
  cancelUpload: (id: string) => void;
  retryUpload: (id: string) => void;
  clearCompleted: () => void;
  clearAll: () => void;
  getStats: () => UploadQueueStats;
  startUpload: () => void;
  isWorkerReady: boolean;
  workerError: Error | null;
}

/**
 * Custom hook for managing multi-file PDF uploads with Web Worker concurrency control
 *
 * @param options - Configuration and callback options
 * @returns Upload queue state and control functions
 *
 * @example
 * ```tsx
 * const {
 *   queue,
 *   addFiles,
 *   cancelUpload,
 *   getStats
 * } = useUploadQueue({
 *   onUploadComplete: (item) => console.log('Uploaded:', item.pdfId),
 *   onAllComplete: (stats) => console.log('All done:', stats)
 * });
 *
 * // Add files to queue
 * await addFiles(selectedFiles, 'game-123', 'en');
 * ```
 */
export function useUploadQueue(options: UseUploadQueueOptions = {}): UseUploadQueueReturn {
  // Set options on store (callbacks)
  useEffect(() => {
    uploadQueueStore.setOptions(options);
  }, [options]);

  // Subscribe to worker state using useSyncExternalStore
  const state = useSyncExternalStore(
    uploadQueueStore.subscribe,
    uploadQueueStore.getSnapshot,
    uploadQueueStore.getServerSnapshot
  );

  // Worker status
  const isWorkerReady = uploadQueueStore.isWorkerReady();
  const workerError = uploadQueueStore.getError();

  // Memoized control functions
  const addFiles = useCallback(
    async (files: File[], gameId: string, language: string) => {
      await uploadQueueStore.addFiles(files, gameId, language);
    },
    []
  );

  const removeFile = useCallback((id: string) => {
    uploadQueueStore.removeFile(id);
  }, []);

  const cancelUpload = useCallback((id: string) => {
    uploadQueueStore.cancelUpload(id);
  }, []);

  const retryUpload = useCallback((id: string) => {
    uploadQueueStore.retryUpload(id);
  }, []);

  const clearCompleted = useCallback(() => {
    uploadQueueStore.clearCompleted();
  }, []);

  const clearAll = useCallback(() => {
    uploadQueueStore.clearAll();
  }, []);

  const getStats = useCallback((): UploadQueueStats => {
    return uploadQueueStore.getStats();
  }, []);

  const startUpload = useCallback(() => {
    uploadQueueStore.startProcessing();
  }, []);

  return {
    queue: state.items,
    addFiles,
    removeFile,
    cancelUpload,
    retryUpload,
    clearCompleted,
    clearAll,
    getStats,
    startUpload,
    isWorkerReady,
    workerError
  };
}

/**
 * Hook for accessing upload queue metrics without full state subscription
 * Useful for dashboard/status components that only need stats
 */
export function useUploadQueueStats(): UploadQueueStats {
  const state = useSyncExternalStore(
    uploadQueueStore.subscribe,
    uploadQueueStore.getSnapshot,
    uploadQueueStore.getServerSnapshot
  );

  return uploadQueueStore.getStats();
}

/**
 * Hook for accessing worker status
 * Useful for error boundaries or status indicators
 */
export function useUploadQueueStatus(): {
  isReady: boolean;
  error: Error | null;
  itemCount: number;
} {
  const state = useSyncExternalStore(
    uploadQueueStore.subscribe,
    uploadQueueStore.getSnapshot,
    uploadQueueStore.getServerSnapshot
  );

  return {
    isReady: uploadQueueStore.isWorkerReady(),
    error: uploadQueueStore.getError(),
    itemCount: state.items.length
  };
}
