/**
 * FE-IMP-008: Upload Queue Store
 * Bridges Web Worker with React useSyncExternalStore
 *
 * Features:
 * - Subscribe/getSnapshot pattern for React integration
 * - Worker lifecycle management with error recovery
 * - File ArrayBuffer transfer to worker
 * - SSR-safe with getServerSnapshot
 */

import type {
  WorkerRequest,
  WorkerResponse,
  UploadQueueState,
  FileData
} from '../workers/uploadQueue.worker';
import type { UploadQueueItem as WorkerUploadQueueItem } from '../workers/uploadQueue.worker';

// Re-export types for public API
export type {
  UploadStatus,
  UploadQueueState
} from '../workers/uploadQueue.worker';

// Public-facing UploadQueueItem type
export type UploadQueueItem = WorkerUploadQueueItem;

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
  onUploadComplete?: (item: UploadQueueItem) => void;
  onUploadError?: (item: UploadQueueItem, error: string) => void;
  onAllComplete?: (stats: UploadQueueStats) => void;
}

// Persistence keys
const STORAGE_KEY = 'meepleai-upload-queue';

class UploadQueueStore {
  private worker: Worker | null = null;
  private state: UploadQueueState = {
    items: [],
    metrics: {
      totalUploads: 0,
      successfulUploads: 0,
      failedUploads: 0,
      cancelledUploads: 0,
      totalBytesUploaded: 0
    }
  };
  private listeners = new Set<() => void>();
  private isReady = false;
  private isInitializing = false;
  private workerError: Error | null = null;
  private restartCount = 0;
  private readonly MAX_RESTARTS = 3;

  // Callbacks
  private options: UseUploadQueueOptions = {};
  private lastCompletedIds = new Set<string>();

  // Buffer for files added before worker is ready
  private pendingFileRequests: Array<{
    files: File[];
    gameId: string;
    language: string;
  }> = [];

  // Idle cleanup timeout
  private idleTimeout: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    // Defer worker creation to avoid SSR issues
    if (typeof window !== 'undefined') {
      this.initializeWorker();

      // Auto-cleanup on page unload (prevents memory leak in SPA navigation)
      window.addEventListener('beforeunload', () => {
        this.destroy();
      });

      // Cleanup on visibility change (tab hidden for >5 min with empty queue)
      this.setupIdleCleanup();
    }
  }

  private initializeWorker(): void {
    if (this.isInitializing || this.worker) {
      return;
    }

    this.isInitializing = true;

    try {
      this.worker = new Worker(
        new URL('../workers/uploadQueue.worker.ts', import.meta.url),
        { type: 'module', name: 'upload-queue-worker' }
      );

      this.setupWorkerListeners();
      this.isReady = false; // Will be set to true on WORKER_READY message

    } catch (error) {
      console.error('[UploadQueueStore] Failed to initialize worker:', error);
      this.workerError = error instanceof Error ? error : new Error('Worker initialization failed');
      this.isReady = false;
    } finally {
      this.isInitializing = false;
    }
  }

  private setupWorkerListeners(): void {
    if (!this.worker) return;

    this.worker.onmessage = (event: MessageEvent<WorkerResponse>) => {
      const response = event.data;

      switch (response.type) {
        case 'WORKER_READY':
          // eslint-disable-next-line no-console
          console.log('[UploadQueueStore] Worker ready');
          this.isReady = true;
          this.workerError = null;
          this.restartCount = 0;

          // Restore state from localStorage before notifying listeners
          this.restoreStateToWorker();

          this.notifyListeners();

          // Process any buffered file requests
          this.processPendingFileRequests();
          break;

        case 'STATE_UPDATED':
          this.state = response.payload;
          this.notifyListeners();
          this.checkForCompletions();
          break;

        case 'UPLOAD_PROGRESS':
          // Progress updates are handled within STATE_UPDATED
          break;

        case 'UPLOAD_SUCCESS':
          this.handleUploadSuccess(response.payload.id, response.payload.pdfId);
          break;

        case 'UPLOAD_FAILED':
          this.handleUploadFailed(
            response.payload.id,
            response.payload.error,
            response.payload.correlationId
          );
          break;

        case 'WORKER_ERROR':
          console.error('[UploadQueueStore] Worker error:', response.payload.message);
          this.workerError = new Error(response.payload.message);
          this.notifyListeners();
          break;

        case 'PERSIST_REQUEST':
          // Worker requests persistence - save to localStorage
          this.saveToLocalStorage(response.payload.items, response.payload.metrics);
          break;
      }
    };

    this.worker.onerror = (error) => {
      console.error('[UploadQueueStore] Worker uncaught error:', error);
      this.handleWorkerCrash();
    };

    this.worker.onmessageerror = (error) => {
      console.error('[UploadQueueStore] Worker message deserialization error:', error);
      this.workerError = new Error('Worker message deserialization failed');
      this.notifyListeners();
    };
  }

  private handleWorkerCrash(): void {
    if (this.restartCount >= this.MAX_RESTARTS) {
      console.error('[UploadQueueStore] Max restart attempts reached');
      this.workerError = new Error(
        `Worker crashed ${this.MAX_RESTARTS} times. Please reload the page.`
      );
      this.isReady = false;
      this.notifyListeners();
      return;
    }

    this.restartCount++;
    console.warn(`[UploadQueueStore] Restarting worker (attempt ${this.restartCount})`);

    // CRITICAL FIX: Preserve current state before terminating
    const stateToRestore = { ...this.state };

    // Persist current state to localStorage before crash
    this.saveToLocalStorage(stateToRestore.items, stateToRestore.metrics);

    this.worker?.terminate();
    this.worker = null;
    this.isReady = false;

    // Reinitialize after a delay
    setTimeout(() => {
      this.initializeWorker();
      // State will be restored via restoreStateToWorker() on WORKER_READY
    }, 1000 * this.restartCount); // Exponential backoff
  }

  private handleUploadSuccess(id: string, pdfId: string): void {
    const item = this.state.items.find(i => i.id === id);
    if (item && this.options.onUploadComplete) {
      this.options.onUploadComplete({ ...item, pdfId, status: 'success', progress: 100 });
    }
  }

  private handleUploadFailed(id: string, error: string, correlationId?: string): void {
    const item = this.state.items.find(i => i.id === id);
    if (item && this.options.onUploadError) {
      this.options.onUploadError(
        { ...item, error, correlationId, status: 'failed', progress: 0 },
        error
      );
    }
  }

  private checkForCompletions(): void {
    const currentCompletedIds = new Set(
      this.state.items
        .filter(item => item.status === 'success')
        .map(item => item.id)
    );

    // Check if all uploads are complete
    const stats = this.getStats();
    const allDone = stats.pending === 0 && stats.uploading === 0 && stats.processing === 0;

    if (allDone && stats.total > 0 && this.options.onAllComplete) {
      // Only notify once per batch
      const hasNewCompletions = Array.from(currentCompletedIds).some(
        id => !this.lastCompletedIds.has(id)
      );

      if (hasNewCompletions) {
        this.options.onAllComplete(stats);
        this.lastCompletedIds = currentCompletedIds;
      }
    }
  }

  // ============================================================================
  // useSyncExternalStore Interface
  // ============================================================================

  subscribe = (callback: () => void): (() => void) => {
    this.listeners.add(callback);
    return () => this.listeners.delete(callback);
  };

  getSnapshot = (): UploadQueueState => {
    return this.state;
  };

  getServerSnapshot = (): UploadQueueState => {
    // SSR-safe: return empty state
    return {
      items: [],
      metrics: {
        totalUploads: 0,
        successfulUploads: 0,
        failedUploads: 0,
        cancelledUploads: 0,
        totalBytesUploaded: 0
      }
    };
  };

  private notifyListeners(): void {
    this.listeners.forEach(callback => callback());
  }

  private async processPendingFileRequests(): Promise<void> {
    if (this.pendingFileRequests.length === 0) {
      return;
    }

    // eslint-disable-next-line no-console
    console.log(`[UploadQueueStore] Processing ${this.pendingFileRequests.length} buffered file requests`);

    // Process all buffered requests
    const requests = [...this.pendingFileRequests];
    this.pendingFileRequests = [];

    for (const request of requests) {
      try {
        await this.addFiles(request.files, request.gameId, request.language);
      } catch (error) {
        console.error('[UploadQueueStore] Failed to process buffered request:', error);
      }
    }
  }

  // ============================================================================
  // Persistence Layer (Main Thread)
  // ============================================================================
  // Workers cannot access localStorage - main thread handles persistence

  private saveToLocalStorage(items: WorkerUploadQueueItem[], metrics: UploadQueueState['metrics']): void {
    try {
      const serializable = {
        items: items.filter(
          item => item.status === 'pending' || item.status === 'failed'
        ),
        metrics,
        savedAt: Date.now()
      };

      localStorage.setItem(STORAGE_KEY, JSON.stringify(serializable));
    } catch (error) {
      console.error('[UploadQueueStore] Failed to save to localStorage:', error);
    }
  }

  private loadFromLocalStorage(): { items: WorkerUploadQueueItem[]; metrics: UploadQueueState['metrics'] } | null {
    try {
      const saved = localStorage.getItem(STORAGE_KEY);
      if (saved) {
        const parsed = JSON.parse(saved);
        return {
          items: parsed.items || [],
          metrics: parsed.metrics || {
            totalUploads: 0,
            successfulUploads: 0,
            failedUploads: 0,
            cancelledUploads: 0,
            totalBytesUploaded: 0
          }
        };
      }
    } catch (error) {
      console.error('[UploadQueueStore] Failed to load from localStorage:', error);
    }
    return null;
  }

  private restoreStateToWorker(): void {
    const saved = this.loadFromLocalStorage();
    if (saved && saved.items.length > 0) {
      // eslint-disable-next-line no-console
      console.log(`[UploadQueueStore] Restoring ${saved.items.length} items to worker`);

      this.postMessage({
        type: 'RESTORE_STATE',
        payload: {
          items: saved.items,
          metrics: saved.metrics
        }
      });
    }
  }

  private clearLocalStorage(): void {
    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch (error) {
      console.error('[UploadQueueStore] Failed to clear localStorage:', error);
    }
  }

  // ============================================================================
  // Public API
  // ============================================================================

  setOptions(options: UseUploadQueueOptions): void {
    this.options = options;
  }

  async addFiles(files: File[], gameId: string, language: string): Promise<void> {
    if (!this.isReady || !this.worker) {
      console.warn('[UploadQueueStore] Worker not ready, buffering files until worker initializes');

      // Buffer the request instead of dropping it
      this.pendingFileRequests.push({ files, gameId, language });
      return;
    }

    // Convert Files to transferable FileData with ArrayBuffers
    const fileDataPromises = Array.from(files).map(async (file): Promise<FileData> => {
      const arrayBuffer = await file.arrayBuffer();
      return {
        name: file.name,
        size: file.size,
        type: file.type,
        lastModified: file.lastModified,
        arrayBuffer
      };
    });

    const filesData = await Promise.all(fileDataPromises);

    // Transfer ArrayBuffers to worker (zero-copy)
    const transferables = filesData.map(fd => fd.arrayBuffer);

    this.postMessage(
      {
        type: 'ADD_FILES',
        payload: { files: filesData, gameId, language }
      },
      transferables
    );
  }

  cancelUpload(id: string): void {
    this.postMessage({ type: 'CANCEL_UPLOAD', payload: { id } });
  }

  retryUpload(id: string): void {
    this.postMessage({ type: 'RETRY_UPLOAD', payload: { id } });
  }

  removeFile(id: string): void {
    this.postMessage({ type: 'REMOVE_ITEM', payload: { id } });
  }

  clearCompleted(): void {
    this.postMessage({ type: 'CLEAR_COMPLETED' });
    this.lastCompletedIds.clear();
  }

  clearAll(): void {
    this.postMessage({ type: 'CLEAR_ALL' });
    this.lastCompletedIds.clear();
    this.clearLocalStorage(); // Clear persisted state
  }

  startProcessing(): void {
    this.postMessage({ type: 'START_PROCESSING' });
  }

  getStats(): UploadQueueStats {
    return this.state.items.reduce(
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
  }

  getError(): Error | null {
    return this.workerError;
  }

  isWorkerReady(): boolean {
    return this.isReady;
  }

  private postMessage(message: WorkerRequest, transferables?: Transferable[]): void {
    if (!this.worker) {
      console.error('[UploadQueueStore] Worker not initialized');
      return;
    }

    if (!this.isReady) {
      console.warn('[UploadQueueStore] Worker not ready, message may be delayed');
    }

    try {
      if (transferables) {
        this.worker.postMessage(message, transferables);
      } else {
        this.worker.postMessage(message);
      }
    } catch (error) {
      console.error('[UploadQueueStore] Failed to post message to worker:', error);
      this.workerError = error instanceof Error ? error : new Error('Failed to communicate with worker');
      this.notifyListeners();
    }
  }

  // Setup idle cleanup for inactive tabs
  private setupIdleCleanup(): void {
    if (typeof document === 'undefined') return;

    document.addEventListener('visibilitychange', () => {
      if (document.hidden) {
        // Tab hidden - set idle timeout (5 minutes)
        this.idleTimeout = setTimeout(() => {
          const stats = this.getStats();
          // Only cleanup if queue is empty and no active uploads
          if (stats.total === 0 || (stats.pending === 0 && stats.uploading === 0)) {
            // eslint-disable-next-line no-console
            console.log('[UploadQueueStore] Idle cleanup - terminating worker');
            this.destroy();
          }
        }, 5 * 60 * 1000); // 5 minutes
      } else {
        // Tab visible again - cancel idle timeout
        if (this.idleTimeout) {
          clearTimeout(this.idleTimeout);
          this.idleTimeout = null;
        }

        // Reinitialize worker if it was destroyed
        if (!this.worker && !this.workerError) {
          // eslint-disable-next-line no-console
          console.log('[UploadQueueStore] Reinitializing worker after idle cleanup');
          this.initializeWorker();
        }
      }
    });
  }

  // Cleanup method (called on page unload or idle timeout)
  destroy(): void {
    if (this.idleTimeout) {
      clearTimeout(this.idleTimeout);
      this.idleTimeout = null;
    }

    this.worker?.terminate();
    this.worker = null;
    this.listeners.clear();
    this.isReady = false;

    // eslint-disable-next-line no-console
    console.log('[UploadQueueStore] Worker destroyed and cleaned up');
  }
}

// Singleton instance
export const uploadQueueStore = new UploadQueueStore();
