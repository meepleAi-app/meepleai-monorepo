/**
 * PDF-05: Multi-file upload queue hook
 * Manages concurrent PDF uploads with progress tracking, retry, and cancellation
 */

import { useCallback, useEffect, useRef, useState } from 'react';
import { retryWithBackoff, isRetryableError } from '../lib/retryUtils';
import { extractCorrelationId } from '../lib/errorUtils';
import { ApiError } from '../lib/api';

export type UploadStatus =
  | 'pending'
  | 'uploading'
  | 'processing'
  | 'success'
  | 'failed'
  | 'cancelled';

export interface UploadQueueItem {
  id: string;
  file: File;
  gameId: string;
  language: string; // Document language for OCR/parsing
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
  autoUpload?: boolean; // Auto-start uploads when files added (default: true for production, false for testing)
  onUploadComplete?: (item: UploadQueueItem) => void;
  onUploadError?: (item: UploadQueueItem, error: string) => void;
  onAllComplete?: (stats: UploadQueueStats) => void;
  // Test observability hooks - fire synchronously before async operations
  onUploadStart?: (item: UploadQueueItem) => void; // Called immediately before upload begins
  onUploadSuccess?: (item: UploadQueueItem) => void; // Called immediately after successful upload
  onQueueAdd?: (items: UploadQueueItem[]) => void; // Called immediately after files added to queue
  onRetry?: (item: UploadQueueItem, attempt: number, error: Error) => void; // Called on each retry attempt
}

interface UploadOperation {
  id: string;
  abortController: AbortController;
}

const API_BASE =
  typeof window !== 'undefined' ? process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:5080' : '';

/**
 * Processing step enum matching backend ProcessingStep
 */
export type ProcessingStep =
  | 'Uploading'
  | 'Extracting'
  | 'Chunking'
  | 'Embedding'
  | 'Indexing'
  | 'Completed'
  | 'Failed';

/**
 * Processing progress response from backend
 */
export interface ProcessingProgress {
  currentStep: ProcessingStep;
  percentComplete: number;
  elapsedTime: string; // TimeSpan as ISO 8601 duration
  estimatedTimeRemaining?: string;
  pagesProcessed: number;
  totalPages: number;
  startedAt: string;
  completedAt?: string;
  errorMessage?: string;
}

/**
 * Polling configuration constants
 */
const POLL_INTERVAL_MS = 1000; // Poll every second
const MAX_POLL_ATTEMPTS = 300; // Max 5 minutes of polling (300 * 1s)

/**
 * Polls the processing status endpoint until completion or failure
 *
 * @param documentId - The PDF document ID to poll
 * @param signal - AbortSignal for cancellation
 * @param onProgress - Callback for progress updates
 * @returns Final ProcessingProgress when complete or failed
 * @throws Error if polling times out or encounters an unrecoverable error
 */
async function pollProcessingStatus(
  documentId: string,
  signal: AbortSignal,
  onProgress?: (progress: ProcessingProgress) => void
): Promise<ProcessingProgress> {
  let attempts = 0;

  while (attempts < MAX_POLL_ATTEMPTS) {
    // Check if cancelled
    if (signal.aborted) {
      throw new DOMException('Polling cancelled', 'AbortError');
    }

    try {
      const response = await fetch(`${API_BASE}/api/v1/pdfs/${documentId}/progress`, {
        method: 'GET',
        credentials: 'include',
        signal,
      });

      if (!response.ok) {
        // If 404, the document may not exist yet or was deleted
        if (response.status === 404) {
          // Wait and retry - document might not be ready yet
          await new Promise(resolve => setTimeout(resolve, POLL_INTERVAL_MS));
          attempts++;
          continue;
        }
        throw new Error(`Failed to get progress: ${response.status}`);
      }

      const progress = (await response.json()) as ProcessingProgress;

      // Notify progress callback
      onProgress?.(progress);

      // Check for terminal states
      if (progress.currentStep === 'Completed' || progress.currentStep === 'Failed') {
        return progress;
      }

      // Wait before next poll
      await new Promise(resolve => setTimeout(resolve, POLL_INTERVAL_MS));
      attempts++;
    } catch (error) {
      // Re-throw abort errors
      if (error instanceof DOMException && error.name === 'AbortError') {
        throw error;
      }

      // For network errors, wait and retry
      console.warn(`Polling attempt ${attempts + 1} failed:`, error);
      await new Promise(resolve => setTimeout(resolve, POLL_INTERVAL_MS));
      attempts++;
    }
  }

  // Timeout - return a failed status
  return {
    currentStep: 'Failed',
    percentComplete: 0,
    elapsedTime: '',
    pagesProcessed: 0,
    totalPages: 0,
    startedAt: new Date().toISOString(),
    errorMessage: 'Processing timed out after 5 minutes',
  };
}

/**
 * Custom hook for managing multi-file PDF uploads with concurrency control
 */
export function useUploadQueue(options: UseUploadQueueOptions = {}) {
  const {
    concurrencyLimit = 3,
    maxRetries = 3,
    autoUpload = true, // Default to true for production behavior
    onUploadComplete,
    onUploadError,
    onAllComplete,
    onUploadStart,
    onUploadSuccess,
    onQueueAdd,
    onRetry,
  } = options;

  const [queue, setQueue] = useState<UploadQueueItem[]>([]);
  const activeUploadsRef = useRef<Map<string, UploadOperation>>(new Map());
  const processingRef = useRef(false);
  const allCompleteNotifiedRef = useRef(false);

  /**
   * Adds files to the upload queue
   */
  const addFiles = useCallback(
    (files: File[], gameId: string, language: string) => {
      const newItems: UploadQueueItem[] = files.map(file => ({
        id: `${Date.now()}-${Math.random().toString(36).substring(7)}`,
        file,
        gameId,
        language,
        status: 'pending',
        progress: 0,
        retryCount: 0,
      }));

      setQueue(prev => [...prev, ...newItems]);
      allCompleteNotifiedRef.current = false;

      // Test observability: notify immediately after queue update (synchronous)
      onQueueAdd?.(newItems);
    },
    [onQueueAdd]
  );

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
        item.id === id ? { ...item, status: 'cancelled' as UploadStatus, progress: 0 } : item
      )
    );
  }, []);

  /**
   * Retries a failed upload
   */
  const retryUpload = useCallback((id: string) => {
    setQueue(prev =>
      prev.map(item =>
        item.id === id ? { ...item, status: 'pending', progress: 0, error: undefined } : item
      )
    );
    allCompleteNotifiedRef.current = false;
  }, []);

  /**
   * Clears all completed uploads from the queue
   */
  const clearCompleted = useCallback(() => {
    setQueue(prev =>
      prev.filter(
        item => item.status !== 'success' && item.status !== 'failed' && item.status !== 'cancelled'
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
        cancelled: 0,
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

      // Test observability: notify immediately before upload starts (synchronous)
      onUploadStart?.(item);

      try {
        // Update status to uploading
        setQueue(prev =>
          prev.map(i => (i.id === item.id ? { ...i, status: 'uploading', progress: 10 } : i))
        );

        const formData = new FormData();
        formData.append('file', item.file);
        formData.append('gameId', item.gameId);
        formData.append('language', item.language);

        // Use retry logic with exponential backoff
        const response = await retryWithBackoff(
          async () => {
            const res = await fetch(`${API_BASE}/api/v1/ingest/pdf`, {
              method: 'POST',
              body: formData,
              credentials: 'include',
              signal: abortController.signal,
            });

            if (!res.ok) {
              const correlationId = extractCorrelationId(res);
              const errorBody = await res.json().catch(() => ({}));
              const errorMessage = errorBody.error ?? res.statusText;

              const apiError = new ApiError({
                message: errorMessage,
                statusCode: res.status,
                correlationId,
                response: res,
              });
              throw apiError;
            }

            return res;
          },
          {
            maxAttempts: maxRetries,
            shouldRetry: error => {
              // Don't retry if aborted
              if (error instanceof DOMException && error.name === 'AbortError') {
                return false;
              }
              return isRetryableError(error);
            },
            onRetry: (error, attempt) => {
              // Test observability: notify immediately on retry (synchronous)
              if (onRetry) {
                const currentItem = queue.find(i => i.id === item.id) || item;
                const errorObj = error instanceof Error ? error : new Error(String(error));
                onRetry(currentItem, attempt, errorObj);
              }

              setQueue(prev =>
                prev.map(i => (i.id === item.id ? { ...i, retryCount: attempt } : i))
              );
            },
          }
        );

        const data = (await response.json()) as { documentId: string };

        // Update to processing status
        setQueue(prev =>
          prev.map(i =>
            i.id === item.id
              ? {
                  ...i,
                  status: 'processing',
                  progress: 50,
                  pdfId: data.documentId,
                }
              : i
          )
        );

        // Poll processing status until complete or failed
        const pollResult = await pollProcessingStatus(
          data.documentId,
          abortController.signal,
          progress => {
            // Update progress during polling
            setQueue(prev =>
              prev.map(i =>
                i.id === item.id
                  ? {
                      ...i,
                      status: 'processing',
                      progress: Math.max(
                        50,
                        Math.min(99, 50 + Math.floor(progress.percentComplete / 2))
                      ),
                    }
                  : i
              )
            );
          }
        );

        if (pollResult.currentStep === 'Failed') {
          // Processing failed on the backend
          const errorMessage = pollResult.errorMessage || 'Processing failed';
          const failedItem = {
            ...item,
            status: 'failed' as UploadStatus,
            error: errorMessage,
            pdfId: data.documentId,
          };
          onUploadError?.(failedItem, errorMessage);

          setQueue(prev =>
            prev.map(i =>
              i.id === item.id
                ? {
                    ...i,
                    status: 'failed',
                    progress: 0,
                    error: errorMessage,
                    pdfId: data.documentId,
                  }
                : i
            )
          );
          return;
        }

        setQueue(prev =>
          prev.map(i => (i.id === item.id ? { ...i, status: 'success', progress: 100 } : i))
        );

        const completedItem = {
          ...item,
          status: 'success' as UploadStatus,
          progress: 100,
          pdfId: data.documentId,
        };

        // Test observability: notify immediately after successful upload (synchronous)
        onUploadSuccess?.(completedItem);

        onUploadComplete?.(completedItem);
      } catch (error: unknown) {
        // Check if it was cancelled
        if (error instanceof DOMException && error.name === 'AbortError') {
          // Already handled by cancelUpload
          return;
        }

        const errorMessage = error instanceof Error ? error.message : 'Upload failed';
        const correlationId = error instanceof ApiError ? error.correlationId : undefined;

        const failedItem = {
          ...item,
          status: 'failed' as UploadStatus,
          error: errorMessage,
          correlationId,
        };

        // Test observability: notify immediately about error (synchronous, before state update)
        onUploadError?.(failedItem, errorMessage);

        setQueue(prev =>
          prev.map(i =>
            i.id === item.id
              ? {
                  ...i,
                  status: 'failed',
                  progress: 0,
                  error: errorMessage,
                  correlationId,
                }
              : i
          )
        );
      } finally {
        activeUploadsRef.current.delete(item.id);
      }
    },
    [maxRetries, onUploadComplete, onUploadError, onUploadStart, onUploadSuccess, onRetry, queue]
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
        // Use activeUploadsRef instead of queue state for accurate count
        // queue state updates asynchronously, but ref is synchronous!
        const activeCount = activeUploadsRef.current.size;
        const stats = getStats();

        // Check if we can start more uploads
        if (activeCount >= concurrencyLimit) {
          break;
        }

        // Find next pending item that's not already being uploaded
        const nextItem = queue.find(
          item => item.status === 'pending' && !activeUploadsRef.current.has(item.id)
        );
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
   * Effect to process queue when it changes (only if autoUpload is enabled)
   */
  useEffect(() => {
    if (autoUpload) {
      void processQueue();
    }
  }, [processQueue, autoUpload]);

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
    getStats,
    startUpload: processQueue, // Expose manual upload trigger for testing
  };
}
