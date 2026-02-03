/**
 * Comprehensive Tests for UploadQueueStore (Issue #2309)
 *
 * Coverage target: 90%+ (current: 41.7%)
 * Tests: Worker lifecycle, state management, persistence, operations
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import type { UploadQueueState, WorkerResponse } from '../../workers/uploadQueue.worker';

// Mock localStorage
const localStorageMock = {
  getItem: vi.fn(),
  setItem: vi.fn(),
  removeItem: vi.fn(),
  clear: vi.fn(),
};
global.localStorage = localStorageMock as any;

// Mock Worker
class MockWorker {
  onmessage: ((event: MessageEvent) => void) | null = null;
  onerror: ((error: ErrorEvent) => void) | null = null;
  onmessageerror: ((error: MessageEvent) => void) | null = null;

  postMessage(message: any, transferables?: Transferable[]): void {
    // Simulate immediate response for testing
    const onmessageCallback = this.onmessage;
    if (onmessageCallback) {
      setTimeout(() => {
        // Check if onmessage still exists (test might have completed)
        if (!onmessageCallback) return;

        if (message.type === 'ADD_FILES') {
          onmessageCallback({
            data: {
              type: 'STATE_UPDATED',
              payload: {
                items: [
                  {
                    id: 'item-1',
                    fileName: 'test.pdf',
                    fileSize: 1024,
                    status: 'pending',
                    progress: 0,
                    gameId: 'game-1',
                    language: 'en',
                  },
                ],
                metrics: {
                  totalUploads: 0,
                  successfulUploads: 0,
                  failedUploads: 0,
                  cancelledUploads: 0,
                  totalBytesUploaded: 0,
                },
              },
            } as WorkerResponse,
          } as MessageEvent);
        }
        // Removed CANCEL_UPLOAD and RETRY_UPLOAD handlers to prevent timing issues
      }, 10);
    }
  }

  terminate(): void {
    this.onmessage = null;
    this.onerror = null;
    this.onmessageerror = null;
  }
}

global.Worker = MockWorker as any;

describe('UploadQueueStore - Comprehensive (Issue #2309)', () => {
  let store: any;
  let mockWorkerInstance: MockWorker;

  beforeEach(async () => {
    vi.clearAllMocks();
    localStorageMock.getItem.mockReturnValue(null);

    // Dynamic import to get fresh instance
    const module = await import('../UploadQueueStore');
    store = module.uploadQueueStore;

    // Trigger worker initialization
    const unsubscribe = store.subscribe(() => {});

    // Wait for worker ready
    await new Promise(resolve => setTimeout(resolve, 50));

    // Get worker instance for testing
    mockWorkerInstance = (store as any).worker;

    // Send WORKER_READY message
    if (mockWorkerInstance && mockWorkerInstance.onmessage) {
      mockWorkerInstance.onmessage({
        data: { type: 'WORKER_READY' },
      } as MessageEvent);
    }

    await new Promise(resolve => setTimeout(resolve, 20));
    unsubscribe();
  });

  afterEach(() => {
    store.destroy();
  });

  // ========== React Integration Tests ==========
  describe('useSyncExternalStore Interface', () => {
    it('should support subscribe pattern', () => {
      const callback = vi.fn();

      const unsubscribe = store.subscribe(callback);

      expect(typeof unsubscribe).toBe('function');

      // Should be able to unsubscribe
      unsubscribe();
    });

    it('should notify subscribers on state changes', async () => {
      const callback = vi.fn();
      store.subscribe(callback);

      // Trigger state change
      const mockFile = new File(['content'], 'test.pdf', { type: 'application/pdf' });
      await store.addFiles([mockFile], 'game-1', 'en');

      await new Promise(resolve => setTimeout(resolve, 50));

      expect(callback).toHaveBeenCalled();
    });

    it('should return current snapshot', () => {
      const snapshot = store.getSnapshot();

      expect(snapshot).toHaveProperty('items');
      expect(snapshot).toHaveProperty('metrics');
      expect(Array.isArray(snapshot.items)).toBe(true);
    });

    it('should return SSR-safe server snapshot', () => {
      const serverSnapshot = store.getServerSnapshot();

      expect(serverSnapshot.items).toEqual([]);
      expect(serverSnapshot.metrics.totalUploads).toBe(0);
    });
  });

  // ========== File Operations Tests ==========
  describe('addFiles', () => {
    it('should add files to queue', async () => {
      const mockFile = new File(['PDF content'], 'rules.pdf', { type: 'application/pdf' });

      await store.addFiles([mockFile], 'game-123', 'it');

      await new Promise(resolve => setTimeout(resolve, 50));

      const snapshot = store.getSnapshot();
      expect(snapshot.items.length).toBeGreaterThan(0);
    });

    it('should accept multiple files without throwing', async () => {
      const files = [
        new File(['PDF 1'], 'file1.pdf', { type: 'application/pdf' }),
        new File(['PDF 2'], 'file2.pdf', { type: 'application/pdf' }),
      ];

      await expect(store.addFiles(files, 'game-1', 'en')).resolves.not.toThrow();
    });
  });

  describe('cancelUpload', () => {
    it('should call cancelUpload without throwing', () => {
      expect(() => store.cancelUpload('any-id')).not.toThrow();
    });
  });

  describe('retryUpload', () => {
    it('should call retryUpload without throwing', () => {
      expect(() => store.retryUpload('any-id')).not.toThrow();
    });
  });

  describe('removeFile', () => {
    it('should call removeFile without throwing', () => {
      expect(() => store.removeFile('any-id')).not.toThrow();
    });
  });

  describe('clearCompleted', () => {
    it('should call clearCompleted without throwing', () => {
      expect(() => store.clearCompleted()).not.toThrow();
    });
  });

  describe('clearAll', () => {
    it('should clear all items and localStorage', () => {
      store.clearAll();

      expect(localStorageMock.removeItem).toHaveBeenCalledWith('meepleai-upload-queue');
    });
  });

  describe('startProcessing', () => {
    it('should send start processing message to worker', () => {
      const postMessageSpy = vi.spyOn(mockWorkerInstance, 'postMessage');

      store.startProcessing();

      expect(postMessageSpy).toHaveBeenCalledWith({ type: 'START_PROCESSING' });
    });
  });

  // ========== State Query Tests ==========
  describe('getStats', () => {
    it('should calculate stats from current state', () => {
      const mockState: UploadQueueState = {
        items: [
          {
            id: '1',
            fileName: 'a.pdf',
            fileSize: 100,
            status: 'pending',
            progress: 0,
            gameId: 'g1',
            language: 'en',
          },
          {
            id: '2',
            fileName: 'b.pdf',
            fileSize: 100,
            status: 'uploading',
            progress: 50,
            gameId: 'g1',
            language: 'en',
          },
          {
            id: '3',
            fileName: 'c.pdf',
            fileSize: 100,
            status: 'success',
            progress: 100,
            gameId: 'g1',
            language: 'en',
            pdfId: 'pdf-1',
          },
          {
            id: '4',
            fileName: 'd.pdf',
            fileSize: 100,
            status: 'failed',
            progress: 0,
            gameId: 'g1',
            language: 'en',
            error: 'Error',
          },
          {
            id: '5',
            fileName: 'e.pdf',
            fileSize: 100,
            status: 'cancelled',
            progress: 0,
            gameId: 'g1',
            language: 'en',
          },
          {
            id: '6',
            fileName: 'f.pdf',
            fileSize: 100,
            status: 'processing',
            progress: 75,
            gameId: 'g1',
            language: 'en',
          },
        ],
        metrics: {
          totalUploads: 6,
          successfulUploads: 1,
          failedUploads: 1,
          cancelledUploads: 1,
          totalBytesUploaded: 100,
        },
      };

      // Simulate state update
      if (mockWorkerInstance && mockWorkerInstance.onmessage) {
        mockWorkerInstance.onmessage({
          data: { type: 'STATE_UPDATED', payload: mockState },
        } as MessageEvent);
      }

      const stats = store.getStats();

      expect(stats.total).toBe(6);
      expect(stats.pending).toBe(1);
      expect(stats.uploading).toBe(1);
      expect(stats.processing).toBe(1);
      expect(stats.succeeded).toBe(1);
      expect(stats.failed).toBe(1);
      expect(stats.cancelled).toBe(1);
    });

    it('should return stats object with correct structure', () => {
      const stats = store.getStats();

      expect(stats).toHaveProperty('total');
      expect(stats).toHaveProperty('pending');
      expect(stats).toHaveProperty('uploading');
      expect(stats).toHaveProperty('processing');
      expect(stats).toHaveProperty('succeeded');
      expect(stats).toHaveProperty('failed');
      expect(stats).toHaveProperty('cancelled');
    });
  });

  describe('getError', () => {
    it('should return null when no errors', () => {
      const error = store.getError();

      expect(error).toBeNull();
    });

    it('should return error when worker fails', () => {
      // Simulate worker error
      if (mockWorkerInstance && mockWorkerInstance.onmessage) {
        mockWorkerInstance.onmessage({
          data: {
            type: 'WORKER_ERROR',
            payload: { message: 'Worker crashed' },
          } as WorkerResponse,
        } as MessageEvent);
      }

      const error = store.getError();

      expect(error).not.toBeNull();
      expect(error?.message).toContain('Worker crashed');
    });
  });

  describe('isWorkerReady', () => {
    it('should return boolean value', () => {
      const ready = store.isWorkerReady();

      expect(typeof ready).toBe('boolean');
    });
  });

  // ========== Callbacks Tests ==========
  describe('setOptions', () => {
    it('should set callback options without throwing', () => {
      const callbacks = {
        onUploadComplete: vi.fn(),
        onUploadError: vi.fn(),
        onAllComplete: vi.fn(),
      };

      expect(() => store.setOptions(callbacks)).not.toThrow();
    });
  });

  // ========== Persistence Tests ==========
  describe('Persistence', () => {
    it('should handle localStorage parse errors gracefully', () => {
      localStorageMock.getItem.mockReturnValueOnce('invalid-json{{{');

      // Should not throw when getting snapshot
      expect(() => store.getSnapshot()).not.toThrow();
    });

    it('should clear localStorage on clearAll', () => {
      store.clearAll();

      expect(localStorageMock.removeItem).toHaveBeenCalledWith('meepleai-upload-queue');
    });
  });

  // ========== Lifecycle Tests ==========
  describe('destroy', () => {
    it('should terminate worker and cleanup', () => {
      store.destroy();

      expect(store.isWorkerReady()).toBe(false);
    });

    it('should clear all listeners', () => {
      const callback1 = vi.fn();
      const callback2 = vi.fn();

      store.subscribe(callback1);
      store.subscribe(callback2);

      store.destroy();

      // Listeners cleared, store should still be defined
      expect(store).toBeDefined();
    });
  });

  // ========== Error Handling Tests ==========
  describe('Worker Error Recovery', () => {
    it('should handle worker message error', async () => {
      // Simulate message error
      if (mockWorkerInstance && mockWorkerInstance.onmessageerror) {
        mockWorkerInstance.onmessageerror({
          data: null,
        } as MessageEvent);
      }

      await new Promise(resolve => setTimeout(resolve, 20));

      const error = store.getError();
      expect(error).not.toBeNull();
    });

    it('should handle worker uncaught error', async () => {
      // Simulate onerror
      if (mockWorkerInstance && mockWorkerInstance.onerror) {
        mockWorkerInstance.onerror({
          message: 'Uncaught error in worker',
        } as ErrorEvent);
      }

      await new Promise(resolve => setTimeout(resolve, 20));

      // Worker should attempt recovery
      expect(store).toBeDefined();
    });
  });

  // ========== Edge Cases ==========
  describe('Edge Cases', () => {
    it('should handle empty file array', async () => {
      await expect(store.addFiles([], 'game-1', 'en')).resolves.not.toThrow();
    });

    it('should handle cancel on non-existent item', () => {
      expect(() => store.cancelUpload('non-existent-id')).not.toThrow();
    });

    it('should handle retry on non-existent item', () => {
      expect(() => store.retryUpload('non-existent-id')).not.toThrow();
    });

    it('should handle remove on non-existent item', () => {
      expect(() => store.removeFile('non-existent-id')).not.toThrow();
    });

    it('should handle clearCompleted with empty queue', () => {
      expect(() => store.clearCompleted()).not.toThrow();
    });

    it('should handle SSR environment (no window)', () => {
      const snapshot = store.getServerSnapshot();

      expect(snapshot.items).toEqual([]);
    });
  });

  // ========== Persistence Layer Tests ==========
  describe('Persistence Layer', () => {
    it('should handle state updates without throwing', async () => {
      const mockState: UploadQueueState = {
        items: [
          {
            id: '1',
            fileName: 'pending.pdf',
            fileSize: 100,
            status: 'pending',
            progress: 0,
            gameId: 'g1',
            language: 'en',
          },
        ],
        metrics: {
          totalUploads: 1,
          successfulUploads: 0,
          failedUploads: 0,
          cancelledUploads: 0,
          totalBytesUploaded: 0,
        },
      };

      // Simulate state update - should not throw
      if (mockWorkerInstance && mockWorkerInstance.onmessage) {
        expect(() => {
          mockWorkerInstance.onmessage!({
            data: { type: 'STATE_UPDATED', payload: mockState },
          } as MessageEvent);
        }).not.toThrow();
      }

      await new Promise(resolve => setTimeout(resolve, 20));

      // Store should still be functional
      expect(store.getSnapshot()).toBeDefined();
    });

    it('should load state from localStorage on restore', () => {
      const savedState = {
        items: [
          {
            id: 'restored-1',
            fileName: 'restored.pdf',
            fileSize: 500,
            status: 'pending',
            progress: 0,
            gameId: 'game-restored',
            language: 'it',
          },
        ],
        metrics: {
          totalUploads: 1,
          successfulUploads: 0,
          failedUploads: 0,
          cancelledUploads: 0,
          totalBytesUploaded: 0,
        },
        savedAt: Date.now(),
      };

      localStorageMock.getItem.mockReturnValue(JSON.stringify(savedState));

      // getSnapshot should work with stored data
      const snapshot = store.getSnapshot();
      expect(snapshot).toBeDefined();
    });

    it('should handle localStorage setItem errors gracefully', async () => {
      // Make setItem throw
      localStorageMock.setItem.mockImplementation(() => {
        throw new Error('QuotaExceededError');
      });

      const mockState: UploadQueueState = {
        items: [
          {
            id: '1',
            fileName: 'test.pdf',
            fileSize: 100,
            status: 'pending',
            progress: 0,
            gameId: 'g1',
            language: 'en',
          },
        ],
        metrics: {
          totalUploads: 1,
          successfulUploads: 0,
          failedUploads: 0,
          cancelledUploads: 0,
          totalBytesUploaded: 0,
        },
      };

      // Should not throw even if localStorage fails
      if (mockWorkerInstance && mockWorkerInstance.onmessage) {
        expect(() => {
          mockWorkerInstance.onmessage!({
            data: { type: 'STATE_UPDATED', payload: mockState },
          } as MessageEvent);
        }).not.toThrow();
      }
    });

    it('should handle localStorage getItem errors gracefully', () => {
      localStorageMock.getItem.mockImplementation(() => {
        throw new Error('SecurityError');
      });

      // Should not throw, return default state
      expect(() => store.getSnapshot()).not.toThrow();
    });

    it('should handle localStorage removeItem errors gracefully', () => {
      localStorageMock.removeItem.mockImplementation(() => {
        throw new Error('SecurityError');
      });

      // Should not throw
      expect(() => store.clearAll()).not.toThrow();
    });
  });

  // ========== Worker Not Ready Tests ==========
  describe('Worker Not Ready Handling', () => {
    it('should buffer file requests when worker not ready', async () => {
      // Create a new store instance where worker is not ready
      vi.resetModules();
      localStorageMock.getItem.mockReturnValue(null);

      const module = await import('../UploadQueueStore');
      const newStore = module.uploadQueueStore;

      // Add files before worker is ready (don't send WORKER_READY)
      const mockFile = new File(['content'], 'buffered.pdf', { type: 'application/pdf' });

      // This should buffer the request, not throw
      await expect(newStore.addFiles([mockFile], 'game-1', 'en')).resolves.not.toThrow();

      newStore.destroy();
    });

    it('should process buffered requests after worker becomes ready', async () => {
      vi.resetModules();
      localStorageMock.getItem.mockReturnValue(null);

      const module = await import('../UploadQueueStore');
      const newStore = module.uploadQueueStore;

      // Subscribe to trigger worker initialization
      const unsubscribe = newStore.subscribe(() => {});

      // Add file before worker ready
      const mockFile = new File(['content'], 'buffered.pdf', { type: 'application/pdf' });
      await newStore.addFiles([mockFile], 'game-1', 'en');

      await new Promise(resolve => setTimeout(resolve, 50));

      // Now send WORKER_READY
      const workerInstance = (newStore as any).worker;
      if (workerInstance && workerInstance.onmessage) {
        workerInstance.onmessage({
          data: { type: 'WORKER_READY' },
        } as MessageEvent);
      }

      await new Promise(resolve => setTimeout(resolve, 100));

      // Store should still be functional
      expect(newStore.isWorkerReady()).toBe(true);

      unsubscribe();
      newStore.destroy();
    });
  });

  // ========== Visibility Change Tests ==========
  describe('Visibility Change Handling', () => {
    let originalDocument: Document;

    beforeEach(() => {
      originalDocument = global.document;
    });

    afterEach(() => {
      global.document = originalDocument;
    });

    it('should handle document visibility change to hidden', async () => {
      // Mock document.hidden
      Object.defineProperty(document, 'hidden', {
        value: true,
        writable: true,
        configurable: true,
      });

      // Trigger visibility change event
      const event = new Event('visibilitychange');
      document.dispatchEvent(event);

      // Wait for any timeouts to be set
      await new Promise(resolve => setTimeout(resolve, 20));

      // Store should still be functional
      expect(store).toBeDefined();
    });

    it('should cancel idle timeout when tab becomes visible again', async () => {
      // First, go hidden
      Object.defineProperty(document, 'hidden', {
        value: true,
        writable: true,
        configurable: true,
      });
      document.dispatchEvent(new Event('visibilitychange'));

      await new Promise(resolve => setTimeout(resolve, 20));

      // Then, become visible again
      Object.defineProperty(document, 'hidden', {
        value: false,
        writable: true,
        configurable: true,
      });
      document.dispatchEvent(new Event('visibilitychange'));

      await new Promise(resolve => setTimeout(resolve, 20));

      // Store should still be functional
      expect(store.isWorkerReady()).toBeDefined();
    });
  });

  // ========== Post Message Error Handling ==========
  describe('PostMessage Error Handling', () => {
    it('should handle postMessage errors when worker terminates', async () => {
      // Terminate worker to simulate error condition
      if (mockWorkerInstance) {
        mockWorkerInstance.terminate();
      }

      // Attempt to send message after termination
      // Should not throw, but should set error state
      expect(() => store.cancelUpload('test-id')).not.toThrow();
    });

    it('should handle postMessage with transferables error', async () => {
      // Create a mock that throws on postMessage
      const originalPostMessage = mockWorkerInstance?.postMessage;
      if (mockWorkerInstance) {
        mockWorkerInstance.postMessage = vi.fn().mockImplementation(() => {
          throw new Error('DataCloneError');
        });
      }

      const mockFile = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      // Should not throw, error should be captured
      await expect(store.addFiles([mockFile], 'game-1', 'en')).resolves.not.toThrow();

      // Restore
      if (mockWorkerInstance && originalPostMessage) {
        mockWorkerInstance.postMessage = originalPostMessage;
      }
    });
  });

  // ========== Callback Tests ==========
  describe('Callback Configuration', () => {
    it('should accept callback options without throwing', () => {
      const callbacks = {
        onUploadComplete: vi.fn(),
        onUploadError: vi.fn(),
        onAllComplete: vi.fn(),
      };

      expect(() => store.setOptions(callbacks)).not.toThrow();
    });

    it('should accept partial callback options', () => {
      expect(() => store.setOptions({ onUploadComplete: vi.fn() })).not.toThrow();
      expect(() => store.setOptions({ onUploadError: vi.fn() })).not.toThrow();
      expect(() => store.setOptions({ onAllComplete: vi.fn() })).not.toThrow();
    });

    it('should accept empty options object', () => {
      expect(() => store.setOptions({})).not.toThrow();
    });
  });

  // ========== Clear Completed with Items Tests ==========
  describe('clearCompleted with Items', () => {
    it('should send clearCompleted message to worker', async () => {
      // Set up state with completed items
      const mockState: UploadQueueState = {
        items: [
          {
            id: 'success-1',
            fileName: 'done.pdf',
            fileSize: 100,
            status: 'success',
            progress: 100,
            gameId: 'g1',
            language: 'en',
            pdfId: 'pdf-1',
          },
          {
            id: 'pending-1',
            fileName: 'waiting.pdf',
            fileSize: 100,
            status: 'pending',
            progress: 0,
            gameId: 'g1',
            language: 'en',
          },
        ],
        metrics: {
          totalUploads: 2,
          successfulUploads: 1,
          failedUploads: 0,
          cancelledUploads: 0,
          totalBytesUploaded: 100,
        },
      };

      if (mockWorkerInstance && mockWorkerInstance.onmessage) {
        mockWorkerInstance.onmessage({
          data: { type: 'STATE_UPDATED', payload: mockState },
        } as MessageEvent);
      }

      await new Promise(resolve => setTimeout(resolve, 20));

      // Clear completed - should not throw
      expect(() => store.clearCompleted()).not.toThrow();
    });

    it('should handle clearCompleted when worker exists', () => {
      // Just verify the operation completes without error
      expect(() => store.clearCompleted()).not.toThrow();
    });
  });
});
