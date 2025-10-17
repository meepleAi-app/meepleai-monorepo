/**
 * PDF-05: Multi-file upload queue hook
 * Manages concurrent PDF uploads with progress tracking, retry, and cancellation
 */

import { useCallback, useEffect, useRef, useState } from 'react';
import { retryWithBackoff, isRetryableError } from '../lib/retryUtils';
import { extractCorrelationId } from '../lib/errorUtils';
import { ApiError } from '../lib/api';

export type UploadStatus = 'pending' | 'uploading' | 'processing' | 'success' | 'failed' | 'cancelled';

export interface UploadQueueItem {
  id: string;
  file: File;
  gameId: string;
  status: UploadStatus;
  progress: number;
  error?: string;
  pdfId?: string;
  correlationId?: string;
  retryCount: number;
}

export interface UploadQueueStats {
  total: number;
  pending: number;
  uploading: number;
  processing: number;
  succeeded: number;
  failed: number;
  cancelled: number;
}

export interface UseUploadQueueOptions {
  concurrencyLimit?: number;
  maxRetries?: number;
  onUploadComplete?: (item: UploadQueueItem) => void;
  onUploadError?: (item: UploadQueueItem, error: string) => void;
  onAllComplete?: (stats: UploadQueueStats) => void;
}

interface UploadOperation {
  id: string;
  abortController: AbortController;
}

const API_BASE = typeof window !== 'undefined'
  ? (process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080')
  : '';

/**
 * Custom hook for managing multi-file PDF uploads with concurrency control
 */
export function useUploadQueue(options: UseUploadQueueOptions = {}) {
  const {
    concurrencyLimit = 3,
    maxRetries = 3,
    onUploadComplete,
    onUploadError,
    onAllComplete
  } = options;

  const [queue, setQueue] = useState<UploadQueueItem[]>([]);
  const activeUploadsRef = useRef<Map<string, UploadOperation>>(new Map());
  const processingRef = useRef(false);
  const allCompleteNotifiedRef = useRef(false);

  /**
   * Adds files to the upload queue
   */
  const addFiles = useCallback((files: File[], gameId: string) => {
    const newItems: UploadQueueItem[] = files.map(file => ({
      id: `${Date.now()}-${Math.random().toString(36).substring(7)}`,
      file,
      gameId,
      status: 'pending',
      progress: 0,
      retryCount: 0
    }));

    setQueue(prev => [...prev, ...newItems]);
    allCompleteNotifiedRef.current = false;
  }, []);

  /**
   * Removes a file from the queue (only if not uploading)
   */
  const removeFile = useCallback((id: string) => {
    setQueue(prev => {
      const item = prev.find(i => i.id === id);
      if (item && item.status === 'uploading') {
        // Can't remove while uploading, must cancel first
        return prev;
      }
      return prev.filter(i => i.id !== id);
    });
  }, []);

  /**
   * Cancels an ongoing upload
   */
  const cancelUpload = useCallback((id: string) => {
    const operation = activeUploadsRef.current.get(id);
    if (operation) {
      operation.abortController.abort();
      activeUploadsRef.current.delete(id);
    }

    setQueue(prev =>
      prev.map(item =>
        item.id === id
          ? { ...item, status: 'cancelled' as UploadStatus, progress: 0 }
          : item
      )
    );
  }, []);

  /**
   * Retries a failed upload
   */
  const retryUpload = useCallback((id: string) => {
    setQueue(prev =>
      prev.map(item =>
        item.id === id
          ? { ...item, status: 'pending', progress: 0, error: undefined }
          : item
      )
    );
    allCompleteNotifiedRef.current = false;
  }, []);

  /**
   * Clears all completed uploads from the queue
   */
  const clearCompleted = useCallback(() => {
    setQueue(prev =>
      prev.filter(item =>
        item.status !== 'success' && item.status !== 'failed' && item.status !== 'cancelled'
      )
    );
  }, []);

  /**
   * Clears the entire queue (cancels active uploads)
   */
  const clearAll = useCallback(() => {
    // Cancel all active uploads
    activeUploadsRef.current.forEach(operation => {
      operation.abortController.abort();
    });
    activeUploadsRef.current.clear();

    setQueue([]);
    allCompleteNotifiedRef.current = false;
  }, []);

  /**
   * Gets current queue statistics
   */
  const getStats = useCallback((): UploadQueueStats => {
    const stats = queue.reduce(
      (acc, item) => {
        acc.total++;
        if (item.status === 'pending') acc.pending++;
        else if (item.status === 'uploading') acc.uploading++;
        else if (item.status === 'processing') acc.processing++;
        else if (item.status === 'success') acc.succeeded++;
        else if (item.status === 'failed') acc.failed++;
        else if (item.status === 'cancelled') acc.cancelled++;
        return acc;
      },
      {
        total: 0,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 0,
        failed: 0,
        cancelled: 0
      }
    );

    return stats;
  }, [queue]);

  /**
   * Uploads a single file with retry logic
   */
  const uploadFile = useCallback(
    async (item: UploadQueueItem): Promise<void> => {
      const abortController = new AbortController();
      const operation: UploadOperation = { id: item.id, abortController };
      activeUploadsRef.current.set(item.id, operation);

      try {
        // Update status to uploading
        setQueue(prev =>
          prev.map(i =>
            i.id === item.id
              ? { ...i, status: 'uploading', progress: 10 }
              : i
          )
        );

        const formData = new FormData();
        formData.append('file', item.file);
        formData.append('gameId', item.gameId);

        // Use retry logic with exponential backoff
        const response = await retryWithBackoff(
          async () => {
            const res = await fetch(`${API_BASE}/api/v1/ingest/pdf`, {
              method: 'POST',
              body: formData,
              credentials: 'include',
              signal: abortController.signal
            });

            if (!res.ok) {
              const correlationId = extractCorrelationId(res);
              const errorBody = await res.json().catch(() => ({}));
              const errorMessage = errorBody.error ?? res.statusText;

              const apiError = new ApiError(errorMessage, res.status, correlationId, res);
              throw apiError;
            }

            return res;
          },
          {
            maxAttempts: maxRetries,
            shouldRetry: (error) => {
              // Don't retry if aborted
              if (error instanceof DOMException && error.name === 'AbortError') {
                return false;
              }
              return isRetryableError(error);
            },
            onRetry: (error, attempt) => {
              setQueue(prev =>
                prev.map(i =>
                  i.id === item.id
                    ? { ...i, retryCount: attempt }
                    : i
                )
              );
            }
          }
        );

        const data = await response.json() as { documentId: string };

        // Update to processing status
        setQueue(prev =>
          prev.map(i =>
            i.id === item.id
              ? {
                  ...i,
                  status: 'processing',
                  progress: 50,
                  pdfId: data.documentId
                }
              : i
          )
        );

        // Simulate processing completion (in real app, would poll status)
        // For now, mark as success after a short delay
        await new Promise(resolve => setTimeout(resolve, 1000));

        setQueue(prev =>
          prev.map(i =>
            i.id === item.id
              ? { ...i, status: 'success', progress: 100 }
              : i
          )
        );

        const completedItem = { ...item, status: 'success' as UploadStatus, progress: 100, pdfId: data.documentId };
        onUploadComplete?.(completedItem);

      } catch (error: unknown) {
        // Check if it was cancelled
        if (error instanceof DOMException && error.name === 'AbortError') {
          // Already handled by cancelUpload
          return;
        }

        const errorMessage = error instanceof Error ? error.message : 'Upload failed';
        const correlationId = error instanceof ApiError ? error.correlationId : undefined;

        setQueue(prev =>
          prev.map(i =>
            i.id === item.id
              ? {
                  ...i,
                  status: 'failed',
                  progress: 0,
                  error: errorMessage,
                  correlationId
                }
              : i
          )
        );

        const failedItem = { ...item, status: 'failed' as UploadStatus, error: errorMessage, correlationId };
        onUploadError?.(failedItem, errorMessage);

      } finally {
        activeUploadsRef.current.delete(item.id);
      }
    },
    [maxRetries, onUploadComplete, onUploadError]
  );

  /**
   * Processes the next batch of pending uploads
   */
  const processQueue = useCallback(async () => {
    if (processingRef.current) {
      return;
    }

    processingRef.current = true;

    try {
      while (true) {
        const stats = getStats();
        const activeCount = stats.uploading;

        // Check if we can start more uploads
        if (activeCount >= concurrencyLimit) {
          break;
        }

        // Find next pending item
        const nextItem = queue.find(item => item.status === 'pending');
        if (!nextItem) {
          break;
        }

        // Start upload (don't await - run in background)
        void uploadFile(nextItem);

        // Small delay to prevent tight loop
        await new Promise(resolve => setTimeout(resolve, 10));
      }
    } finally {
      processingRef.current = false;
    }
  }, [queue, concurrencyLimit, uploadFile, getStats]);

  /**
   * Effect to process queue when it changes
   */
  useEffect(() => {
    void processQueue();
  }, [processQueue]);

  /**
   * Effect to notify when all uploads complete
   */
  useEffect(() => {
    if (allCompleteNotifiedRef.current) {
      return;
    }

    const stats = getStats();
    const hasItems = stats.total > 0;
    const allDone = stats.pending === 0 && stats.uploading === 0 && stats.processing === 0;

    if (hasItems && allDone && onAllComplete) {
      allCompleteNotifiedRef.current = true;
      onAllComplete(stats);
    }
  }, [queue, getStats, onAllComplete]);

  return {
    queue,
    addFiles,
    removeFile,
    cancelUpload,
    retryUpload,
    clearCompleted,
    clearAll,
    getStats
  };
}
