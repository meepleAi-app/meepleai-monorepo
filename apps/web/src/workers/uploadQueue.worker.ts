/**
 * FE-IMP-008: Upload Queue Web Worker
 * Handles PDF upload queue off the main thread with retry/backoff logic
 *
 * Features:
 * - Concurrent upload management with configurable limits
 * - Automatic retry with exponential backoff (reuses retryUtils)
 * - LocalStorage persistence for queue recovery across refreshes
 * - BroadcastChannel sync for multi-tab coordination
 * - Centralized metrics and correlation ID tracking
 *
 * Note on Logging (Issue #1433):
 * - Workers run in a separate thread context without window/document access
 * - console.error is retained for local debugging in development
 * - All errors are also sent to the main thread via postMessage (WORKER_ERROR)
 * - Main thread logs these errors with structured logging
 */

import { ApiError } from '../lib/api';
import { extractCorrelationId } from '../lib/errorUtils';
import { logger } from '../lib/logger';
import { isRetryableError, retryWithBackoff } from '../lib/retryUtils';

// ============================================================================
// Type Definitions
// ============================================================================

export type UploadStatus =
  | 'pending'
  | 'uploading'
  | 'processing'
  | 'success'
  | 'failed'
  | 'cancelled';

export interface UploadQueueItem {
  id: string;
  file: {
    name: string;
    size: number;
    type: string;
    lastModified: number;
  };
  gameId: string;
  language: string;
  status: UploadStatus;
  progress: number;
  error?: string;
  pdfId?: string;
  correlationId?: string;
  retryCount: number;
  createdAt: number;
}

export interface UploadQueueState {
  items: UploadQueueItem[];
  metrics: {
    totalUploads: number;
    successfulUploads: number;
    failedUploads: number;
    cancelledUploads: number;
    totalBytesUploaded: number;
  };
}

export type WorkerRequest =
  | { type: 'ADD_FILES'; payload: { files: FileData[]; gameId: string; language: string } }
  | { type: 'CANCEL_UPLOAD'; payload: { id: string } }
  | { type: 'RETRY_UPLOAD'; payload: { id: string } }
  | { type: 'REMOVE_ITEM'; payload: { id: string } }
  | { type: 'CLEAR_COMPLETED' }
  | { type: 'CLEAR_ALL' }
  | { type: 'START_PROCESSING' }
  | { type: 'GET_STATE' }
  | {
      type: 'RESTORE_STATE';
      payload: { items: UploadQueueItem[]; metrics: UploadQueueState['metrics'] };
    };

export type WorkerResponse =
  | { type: 'STATE_UPDATED'; payload: UploadQueueState }
  | { type: 'UPLOAD_PROGRESS'; payload: { id: string; progress: number } }
  | { type: 'UPLOAD_SUCCESS'; payload: { id: string; pdfId: string } }
  | { type: 'UPLOAD_FAILED'; payload: { id: string; error: string; correlationId?: string } }
  | { type: 'WORKER_READY' }
  | { type: 'WORKER_ERROR'; payload: { message: string } }
  | {
      type: 'PERSIST_REQUEST';
      payload: { items: UploadQueueItem[]; metrics: UploadQueueState['metrics'] };
    }
  | { type: 'CLEAR_COMPLETED_DONE'; payload: { clearedIds: string[] } };

export type BroadcastMessage =
  | { type: 'QUEUE_SYNC'; payload: UploadQueueState; tabId: string }
  | { type: 'UPLOAD_STARTED'; payload: { id: string; tabId: string } }
  | { type: 'UPLOAD_COMPLETED'; payload: { id: string; tabId: string } };

// File data structure (File objects can't be transferred)
export interface FileData {
  name: string;
  size: number;
  type: string;
  lastModified: number;
  arrayBuffer: ArrayBuffer;
}

// ============================================================================
// Worker State
// ============================================================================

// NOTE: localStorage access moved to main thread, but keeping key for reference
const _STORAGE_KEY = 'meepleai-upload-queue';
const BROADCAST_CHANNEL = 'meepleai-upload-queue-sync';
const TAB_ID = `tab-${Date.now()}-${Math.random().toString(36).substring(7)}`;
const API_BASE = self.location.origin.includes('localhost')
  ? 'http://localhost:8080'
  : self.location.origin;

const state: UploadQueueState = {
  items: [],
  metrics: {
    totalUploads: 0,
    successfulUploads: 0,
    failedUploads: 0,
    cancelledUploads: 0,
    totalBytesUploaded: 0,
  },
};

const clearedItemIds = new Set<string>();

const activeUploads = new Map<string, AbortController>();
let broadcastChannel: BroadcastChannel;
const concurrencyLimit = 3;
const maxRetries = 3;
let isProcessing = false;

// ============================================================================
// Persistence Layer (Delegated to Main Thread)
// ============================================================================
// NOTE: Web Workers cannot access localStorage directly!
// Persistence is handled by the main thread (UploadQueueStore) via postMessage

function requestPersistence(): void {
  // Notify main thread to persist current state
  self.postMessage({
    type: 'PERSIST_REQUEST',
    payload: {
      items: state.items,
      metrics: state.metrics,
    },
  } as WorkerResponse);
}

// ============================================================================
// BroadcastChannel Communication
// ============================================================================

function setupBroadcastChannel(): void {
  try {
    broadcastChannel = new BroadcastChannel(BROADCAST_CHANNEL);

    broadcastChannel.onmessage = (event: MessageEvent<BroadcastMessage>) => {
      const message = event.data;

      // Ignore messages from this tab
      if ('tabId' in message.payload && message.payload.tabId === TAB_ID) {
        return;
      }

      switch (message.type) {
        case 'QUEUE_SYNC':
          // Merge state from other tabs (avoid overwriting active uploads)
          mergeQueueState(message.payload);
          break;
        case 'UPLOAD_STARTED':
          // Mark item as being handled by another tab
          markAsHandledByOtherTab(message.payload.id);
          break;
        case 'UPLOAD_COMPLETED':
          // Update item status from other tab
          updateItemFromOtherTab(message.payload.id);
          break;
      }
    };
  } catch (error) {
    logger.error('[UploadWorker] BroadcastChannel not supported:', error);
  }
}

function broadcastQueueUpdate(): void {
  if (broadcastChannel) {
    try {
      broadcastChannel.postMessage({
        type: 'QUEUE_SYNC',
        payload: state,
        tabId: TAB_ID,
      } as BroadcastMessage);
    } catch (error) {
      logger.error('[UploadWorker] Failed to broadcast update:', error);
    }
  }
}

function broadcastUploadStarted(id: string): void {
  if (broadcastChannel) {
    try {
      broadcastChannel.postMessage({
        type: 'UPLOAD_STARTED',
        payload: { id, tabId: TAB_ID },
      } as BroadcastMessage);
    } catch (error) {
      logger.error('[UploadWorker] Failed to broadcast upload started:', error);
    }
  }
}

function mergeQueueState(otherState: UploadQueueState): void {
  // Simple merge: prefer items not being actively uploaded
  const activeIds = new Set(activeUploads.keys());

  otherState.items.forEach(otherItem => {
    if (clearedItemIds.has(otherItem.id)) {
      return;
    }

    const existingIndex = state.items.findIndex(item => item.id === otherItem.id);

    if (existingIndex === -1) {
      // New item from other tab
      state.items.push(otherItem);
    } else if (!activeIds.has(otherItem.id)) {
      // Update if not currently uploading
      // eslint-disable-next-line security/detect-object-injection
      state.items[existingIndex] = otherItem;
    }
  });

  notifyStateUpdate();
}

function markAsHandledByOtherTab(id: string): void {
  // Don't start this upload if another tab is handling it
  const item = state.items.find(i => i.id === id);
  if (item && item.status === 'pending') {
    // Leave it pending but don't process it
    if (process.env.NODE_ENV !== 'production') {
      logger.debug(`[UploadWorker] Item ${id} being handled by another tab`);
    }
  }
}

function updateItemFromOtherTab(id: string): void {
  const item = state.items.find(i => i.id === id);
  if (item) {
    item.status = 'success';
    item.progress = 100;
    notifyStateUpdate();
  }
}

// ============================================================================
// Upload Logic
// ============================================================================

async function uploadFile(item: UploadQueueItem, fileData: ArrayBuffer): Promise<void> {
  const abortController = new AbortController();
  activeUploads.set(item.id, abortController);

  // Update status to uploading
  updateItemStatus(item.id, 'uploading', 10);
  broadcastUploadStarted(item.id);

  try {
    // Reconstruct File object from stored data
    const file = new File([fileData], item.file.name, {
      type: item.file.type,
      lastModified: item.file.lastModified,
    });

    const formData = new FormData();
    formData.append('file', file);
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
          updateItemRetryCount(item.id, attempt);
        },
      }
    );

    const data = (await response.json()) as { documentId: string };

    // Update to processing status
    updateItemStatus(item.id, 'processing', 50, undefined, data.documentId);

    // Simulate processing completion (in production, would poll status)
    await new Promise(resolve => setTimeout(resolve, 1000));

    // Mark as success
    updateItemStatus(item.id, 'success', 100, undefined, data.documentId);

    // Update metrics
    state.metrics.successfulUploads++;
    state.metrics.totalBytesUploaded += item.file.size;

    // CRITICAL: Auto-cleanup file ArrayBuffer (prevents memory leak)
    // After successful upload, we no longer need the file data
    fileDataCache.delete(item.id);

    // Notify main thread
    self.postMessage({
      type: 'UPLOAD_SUCCESS',
      payload: { id: item.id, pdfId: data.documentId },
    } as WorkerResponse);
  } catch (error: unknown) {
    // Check if it was cancelled
    if (error instanceof DOMException && error.name === 'AbortError') {
      return; // Already handled by cancelUpload
    }

    const errorMessage = error instanceof Error ? error.message : 'Upload failed';
    const correlationId = error instanceof ApiError ? error.correlationId : undefined;

    updateItemStatus(item.id, 'failed', 0, errorMessage, undefined, correlationId);

    // Update metrics
    state.metrics.failedUploads++;

    // NOTE: Don't delete file data on failure - allow retry
    // File data will be cleaned up when user clears failed items or retries successfully

    // Notify main thread
    self.postMessage({
      type: 'UPLOAD_FAILED',
      payload: { id: item.id, error: errorMessage, correlationId },
    } as WorkerResponse);
  } finally {
    activeUploads.delete(item.id);
  }
}

async function processQueue(): Promise<void> {
  if (isProcessing) {
    return;
  }

  isProcessing = true;

  try {
    while (true) {
      const activeCount = activeUploads.size;

      // Check concurrency limit
      if (activeCount >= concurrencyLimit) {
        break;
      }

      // Find next pending item
      const nextItem = state.items.find(
        item => item.status === 'pending' && !activeUploads.has(item.id)
      );

      if (!nextItem) {
        break;
      }

      // Check if we have file data in storage (for resumed uploads)
      const fileData = fileDataCache.get(nextItem.id);
      if (!fileData) {
        logger.error(`[UploadWorker] No file data for item ${nextItem.id}`);
        updateItemStatus(nextItem.id, 'failed', 0, 'File data not found');
        continue;
      }

      // Start upload (don't await - run in background)
      void uploadFile(nextItem, fileData);

      // Small delay to prevent tight loop
      await new Promise(resolve => setTimeout(resolve, 10));
    }
  } finally {
    isProcessing = false;
  }
}

// ============================================================================
// State Management
// ============================================================================

// Cache for file data (ArrayBuffers)
const fileDataCache = new Map<string, ArrayBuffer>();

function updateItemStatus(
  id: string,
  status: UploadStatus,
  progress: number,
  error?: string,
  pdfId?: string,
  correlationId?: string
): void {
  const item = state.items.find(i => i.id === id);
  if (item) {
    item.status = status;
    item.progress = progress;
    if (error !== undefined) item.error = error;
    if (pdfId !== undefined) item.pdfId = pdfId;
    if (correlationId !== undefined) item.correlationId = correlationId;

    notifyStateUpdate();
    requestPersistence(); // Request main thread to persist
    broadcastQueueUpdate();
  }
}

function updateItemRetryCount(id: string, retryCount: number): void {
  const item = state.items.find(i => i.id === id);
  if (item) {
    item.retryCount = retryCount;
    notifyStateUpdate();
  }
}

function notifyStateUpdate(): void {
  self.postMessage({
    type: 'STATE_UPDATED',
    payload: state,
  } as WorkerResponse);
}

// ============================================================================
// Message Handlers
// ============================================================================

function handleAddFiles(files: FileData[], gameId: string, language: string): void {
  const newItems: UploadQueueItem[] = files.map(fileData => {
    const id = `${Date.now()}-${Math.random().toString(36).substring(7)}`;

    // Store file data in cache
    fileDataCache.set(id, fileData.arrayBuffer);

    return {
      id,
      file: {
        name: fileData.name,
        size: fileData.size,
        type: fileData.type,
        lastModified: fileData.lastModified,
      },
      gameId,
      language,
      status: 'pending',
      progress: 0,
      retryCount: 0,
      createdAt: Date.now(),
    };
  });

  state.items.push(...newItems);
  state.metrics.totalUploads += newItems.length;

  notifyStateUpdate();
  requestPersistence(); // Request main thread to persist
  broadcastQueueUpdate();

  // Auto-start processing
  void processQueue();
}

function handleCancelUpload(id: string): void {
  const controller = activeUploads.get(id);
  if (controller) {
    controller.abort();
    activeUploads.delete(id);
  }

  updateItemStatus(id, 'cancelled', 0);
  state.metrics.cancelledUploads++;

  // Cleanup file data for cancelled uploads (free memory)
  fileDataCache.delete(id);
}

function handleRetryUpload(id: string): void {
  const item = state.items.find(i => i.id === id);
  if (item) {
    item.status = 'pending';
    item.progress = 0;
    item.error = undefined;
    item.retryCount = 0;

    notifyStateUpdate();
    requestPersistence(); // Request main thread to persist
    void processQueue();
  }
}

function handleRemoveItem(id: string): void {
  // Only allow removal if not uploading
  const item = state.items.find(i => i.id === id);
  if (item && item.status !== 'uploading') {
    state.items = state.items.filter(i => i.id !== id);
    fileDataCache.delete(id);

    notifyStateUpdate();
    requestPersistence(); // Request main thread to persist
  }
}

function handleClearCompleted(): void {
  const completedIds = state.items
    .filter(
      item => item.status === 'success' || item.status === 'failed' || item.status === 'cancelled'
    )
    .map(item => item.id);

  clearedItemIds.clear();
  completedIds.forEach(id => clearedItemIds.add(id));

  state.items = state.items.filter(item => !completedIds.includes(item.id));
  completedIds.forEach(id => fileDataCache.delete(id));

  notifyStateUpdate();
  self.postMessage({
    type: 'CLEAR_COMPLETED_DONE',
    payload: { clearedIds: completedIds },
  });
  requestPersistence(); // Request main thread to persist
  broadcastQueueUpdate();
}

function handleClearAll(): void {
  // Cancel all active uploads
  activeUploads.forEach(controller => controller.abort());
  activeUploads.clear();

  state.items = [];
  fileDataCache.clear();

  notifyStateUpdate();
  requestPersistence(); // Request main thread to clear persistence
  broadcastQueueUpdate();
}

// ============================================================================
// Worker Initialization
// ============================================================================

self.onmessage = (event: MessageEvent<WorkerRequest>) => {
  const message = event.data;

  try {
    switch (message.type) {
      case 'ADD_FILES':
        handleAddFiles(message.payload.files, message.payload.gameId, message.payload.language);
        break;
      case 'CANCEL_UPLOAD':
        handleCancelUpload(message.payload.id);
        break;
      case 'RETRY_UPLOAD':
        handleRetryUpload(message.payload.id);
        break;
      case 'REMOVE_ITEM':
        handleRemoveItem(message.payload.id);
        break;
      case 'CLEAR_COMPLETED':
        handleClearCompleted();
        break;
      case 'CLEAR_ALL':
        handleClearAll();
        break;
      case 'START_PROCESSING':
        void processQueue();
        break;
      case 'GET_STATE':
        notifyStateUpdate();
        break;
      case 'RESTORE_STATE':
        // Restore state from main thread's localStorage
        state.items = message.payload.items;
        state.metrics = message.payload.metrics;
        if (process.env.NODE_ENV !== 'production') {
          logger.debug(`[UploadWorker] Restored ${state.items.length} items from main thread`);
        }
        notifyStateUpdate();
        void processQueue(); // Auto-start processing restored items
        break;
    }
  } catch (error) {
    logger.error('[UploadWorker] Error handling message:', error);
    self.postMessage({
      type: 'WORKER_ERROR',
      payload: { message: error instanceof Error ? error.message : 'Unknown error' },
    } as WorkerResponse);
  }
};

// Initialize worker
setupBroadcastChannel();

self.postMessage({
  type: 'WORKER_READY',
} as WorkerResponse);

if (process.env.NODE_ENV !== 'production') {
  logger.debug('[UploadWorker] Initialized, awaiting state restoration from main thread');
}
