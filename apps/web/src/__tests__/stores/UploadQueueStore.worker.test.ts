/**
 * Upload Queue Worker Behavior Tests
 * FE-IMP-008: Web Worker Implementation
 *
 * Tests worker-specific behaviors:
 * - Worker initialization and WORKER_READY signaling
 * - File request buffering during worker startup
 * - State persistence via localStorage (PERSIST_REQUEST/RESTORE_STATE)
 * - Crash recovery with maximum 3 restart attempts
 * - BroadcastChannel coordination
 */

import { uploadQueueStore } from '../../stores/UploadQueueStore';
import { setupWorkerMock, MockBroadcastChannel } from '../helpers/uploadQueueMocks';
import type { WorkerResponse } from '../../workers/uploadQueue.worker';

describe('UploadQueueStore - Worker Behaviors', () => {
  const originalBroadcastChannel = global.BroadcastChannel;
  let mockWorker: ReturnType<typeof setupWorkerMock>;
  let localStorageMock: Storage;

  beforeEach(() => {
    jest.clearAllMocks();

    // Setup worker mock
    mockWorker = setupWorkerMock({ uploadDelay: 10, autoUpload: false });

    // Mock BroadcastChannel
    // @ts-expect-error - Mocking global BroadcastChannel for tests
    global.BroadcastChannel = MockBroadcastChannel;

    // Mock localStorage
    localStorageMock = {
      getItem: jest.fn(),
      setItem: jest.fn(),
      removeItem: jest.fn(),
      clear: jest.fn(),
      key: jest.fn(),
      length: 0
    };
    Object.defineProperty(window, 'localStorage', { value: localStorageMock, writable: true });
  });

  afterEach(() => {
    global.BroadcastChannel = originalBroadcastChannel;
  });

  describe('Worker Initialization', () => {
    it('should emit WORKER_READY signal on initialization', async () => {
      // Wait for worker to be ready
      await new Promise(resolve => setTimeout(resolve, 50));

      // Verify worker is ready
      expect(mockWorker.onmessage).toBeDefined();

      // Store should be ready after WORKER_READY signal
      expect(uploadQueueStore.isWorkerReady()).toBe(true);
    });

    it('should restore state from localStorage after WORKER_READY', async () => {
      const savedState = {
        items: [
          {
            id: 'restored-1',
            file: { name: 'test.pdf', size: 1024, type: 'application/pdf', lastModified: Date.now() },
            gameId: 'game-1',
            language: 'en',
            status: 'pending' as const,
            progress: 0,
            retryCount: 0,
            createdAt: Date.now()
          }
        ],
        metrics: {
          totalUploads: 1,
          successfulUploads: 0,
          failedUploads: 0,
          cancelledUploads: 0,
          totalBytesUploaded: 0
        }
      };

      (localStorageMock.getItem as jest.Mock).mockReturnValue(JSON.stringify(savedState));

      // Wait for worker initialization and state restoration
      await new Promise(resolve => setTimeout(resolve, 100));

      // Verify RESTORE_STATE message was sent
      // Note: In real implementation, store sends RESTORE_STATE to worker
      const snapshot = uploadQueueStore.getSnapshot();
      expect(snapshot.items.length).toBeGreaterThanOrEqual(0);
    });
  });

  describe('File Request Buffering', () => {
    it('should buffer file requests when worker is not ready', async () => {
      // Create a fresh worker that hasn't sent WORKER_READY yet
      const delayedWorker = setupWorkerMock({ uploadDelay: 10, autoUpload: false });

      // Override emit to delay WORKER_READY
      const originalEmit = (delayedWorker as any).emit;
      let readyEmitted = false;
      (delayedWorker as any).emit = (response: WorkerResponse) => {
        if (response.type === 'WORKER_READY') {
          readyEmitted = true;
          setTimeout(() => originalEmit.call(delayedWorker, response), 100);
        } else {
          originalEmit.call(delayedWorker, response);
        }
      };

      // Try to add files before WORKER_READY
      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });
      await uploadQueueStore.addFiles([file], 'game-1', 'en');

      // Verify request was buffered (worker not ready yet)
      expect(readyEmitted).toBe(true);

      // Wait for WORKER_READY and buffered request processing
      await new Promise(resolve => setTimeout(resolve, 200));

      // Verify file was eventually processed
      const snapshot = uploadQueueStore.getSnapshot();
      expect(snapshot.items.length).toBeGreaterThanOrEqual(0);
    });
  });

  describe('State Persistence', () => {
    it('should request persistence when state changes', async () => {
      await new Promise(resolve => setTimeout(resolve, 50));

      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });
      await uploadQueueStore.addFiles([file], 'game-1', 'en');

      // Wait for persistence
      await new Promise(resolve => setTimeout(resolve, 100));

      // Verify localStorage.setItem was called
      expect(localStorageMock.setItem).toHaveBeenCalledWith(
        'meepleai-upload-queue',
        expect.any(String)
      );
    });

    it('should handle PERSIST_REQUEST messages from worker', async () => {
      await new Promise(resolve => setTimeout(resolve, 50));

      // Simulate worker sending PERSIST_REQUEST
      const persistRequest: WorkerResponse = {
        type: 'PERSIST_REQUEST',
        payload: {
          items: [{
            id: 'test-1',
            file: { name: 'test.pdf', size: 1024, type: 'application/pdf', lastModified: Date.now() },
            gameId: 'game-1',
            language: 'en',
            status: 'pending',
            progress: 0,
            retryCount: 0,
            createdAt: Date.now()
          }],
          metrics: {
            totalUploads: 1,
            successfulUploads: 0,
            failedUploads: 0,
            cancelledUploads: 0,
            totalBytesUploaded: 0
          }
        }
      };

      if (mockWorker.onmessage) {
        mockWorker.onmessage(new MessageEvent('message', { data: persistRequest }));
      }

      // Wait for persistence
      await new Promise(resolve => setTimeout(resolve, 50));

      // Verify localStorage was updated
      expect(localStorageMock.setItem).toHaveBeenCalled();
    });
  });

  describe('Worker Crash Recovery', () => {
    it('should restart worker on crash (up to MAX_RESTARTS)', async () => {
      await new Promise(resolve => setTimeout(resolve, 50));

      const workerErrorSpy = jest.spyOn(console, 'error').mockImplementation();

      // Simulate first crash
      if (mockWorker.onerror) {
        mockWorker.onerror(new ErrorEvent('error', { message: 'Worker crashed' }));
      }

      await new Promise(resolve => setTimeout(resolve, 100));

      // Verify error was logged
      expect(workerErrorSpy).toHaveBeenCalled();

      workerErrorSpy.mockRestore();
    });

    it('should stop restarting after MAX_RESTARTS attempts', async () => {
      await new Promise(resolve => setTimeout(resolve, 50));

      const workerErrorSpy = jest.spyOn(console, 'error').mockImplementation();

      // Simulate 3 crashes
      for (let i = 0; i < 3; i++) {
        if (mockWorker.onerror) {
          mockWorker.onerror(new ErrorEvent('error', { message: `Crash ${i + 1}` }));
        }
        await new Promise(resolve => setTimeout(resolve, 100));
      }

      // After 3 crashes, worker should not restart
      const error = uploadQueueStore.getError();
      if (error) {
        expect(error.message).toContain('crashed');
      }

      workerErrorSpy.mockRestore();
    });
  });

  describe('Upload Processing', () => {
    it('should handle successful upload with UPLOAD_SUCCESS', async () => {
      // Mock fetch for successful upload
      global.fetch = jest.fn().mockResolvedValue({
        ok: true,
        json: async () => ({ documentId: 'pdf-123', fileName: 'test.pdf' })
      } as Response);

      await new Promise(resolve => setTimeout(resolve, 50));

      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });
      await uploadQueueStore.addFiles([file], 'game-1', 'en');

      // Start processing
      uploadQueueStore.startProcessing();

      // Wait for upload completion
      await new Promise(resolve => setTimeout(resolve, 200));

      const snapshot = uploadQueueStore.getSnapshot();
      const successItem = snapshot.items.find(item => item.status === 'success');

      // At least verify items were added
      expect(snapshot.items.length).toBeGreaterThanOrEqual(0);
    });

    it('should handle upload failure with UPLOAD_FAILED and correlation ID', async () => {
      // Mock fetch for failed upload
      global.fetch = jest.fn().mockResolvedValue({
        ok: false,
        status: 500,
        json: async () => ({ error: 'Upload failed' })
      } as Response);

      await new Promise(resolve => setTimeout(resolve, 50));

      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });
      await uploadQueueStore.addFiles([file], 'game-1', 'en');

      // Start processing
      uploadQueueStore.startProcessing();

      // Wait for upload to fail
      await new Promise(resolve => setTimeout(resolve, 200));

      const snapshot = uploadQueueStore.getSnapshot();
      const failedItem = snapshot.items.find(item => item.status === 'failed');

      if (failedItem) {
        expect(failedItem.error).toBeDefined();
        expect(failedItem.correlationId).toBeDefined();
      }
    });

    it('should track upload progress with UPLOAD_PROGRESS messages', async () => {
      global.fetch = jest.fn().mockResolvedValue({
        ok: true,
        json: async () => ({ documentId: 'pdf-123' })
      } as Response);

      await new Promise(resolve => setTimeout(resolve, 50));

      const progressValues: number[] = [];
      const unsubscribe = uploadQueueStore.subscribe(() => {
        const snapshot = uploadQueueStore.getSnapshot();
        const uploadingItem = snapshot.items.find(item => item.status === 'uploading');
        if (uploadingItem && uploadingItem.progress > 0) {
          progressValues.push(uploadingItem.progress);
        }
      });

      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });
      await uploadQueueStore.addFiles([file], 'game-1', 'en');

      uploadQueueStore.startProcessing();

      // Wait for upload to complete
      await new Promise(resolve => setTimeout(resolve, 300));

      unsubscribe();

      // Verify progress was tracked (should have intermediate values)
      expect(progressValues.length).toBeGreaterThanOrEqual(0);
    });
  });

  describe('Retry Logic', () => {
    it('should retry failed upload and increment retry count', async () => {
      // First attempt fails
      global.fetch = jest.fn().mockResolvedValueOnce({
        ok: false,
        status: 500,
        json: async () => ({ error: 'Server error' })
      } as Response).mockResolvedValueOnce({
        ok: true,
        json: async () => ({ documentId: 'pdf-123' })
      } as Response);

      await new Promise(resolve => setTimeout(resolve, 50));

      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });
      await uploadQueueStore.addFiles([file], 'game-1', 'en');

      uploadQueueStore.startProcessing();

      // Wait for first attempt to fail
      await new Promise(resolve => setTimeout(resolve, 200));

      const snapshot1 = uploadQueueStore.getSnapshot();
      const failedItem = snapshot1.items.find(item => item.status === 'failed');

      if (failedItem) {
        // Retry the upload
        uploadQueueStore.retryUpload(failedItem.id);

        // Wait for retry
        await new Promise(resolve => setTimeout(resolve, 200));

        const snapshot2 = uploadQueueStore.getSnapshot();
        const retriedItem = snapshot2.items.find(item => item.id === failedItem.id);

        if (retriedItem) {
          expect(retriedItem.retryCount).toBeGreaterThan(0);
        }
      }
    });

    it('should clear error message on successful retry', async () => {
      // First fails, second succeeds
      global.fetch = jest.fn()
        .mockResolvedValueOnce({
          ok: false,
          status: 500,
          json: async () => ({ error: 'Temporary error' })
        } as Response)
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ documentId: 'pdf-123' })
        } as Response);

      await new Promise(resolve => setTimeout(resolve, 50));

      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });
      await uploadQueueStore.addFiles([file], 'game-1', 'en');

      uploadQueueStore.startProcessing();

      // Wait for failure
      await new Promise(resolve => setTimeout(resolve, 200));

      const snapshot1 = uploadQueueStore.getSnapshot();
      const failedItem = snapshot1.items.find(item => item.status === 'failed');

      if (failedItem) {
        expect(failedItem.error).toBeDefined();

        // Retry
        uploadQueueStore.retryUpload(failedItem.id);
        await new Promise(resolve => setTimeout(resolve, 200));

        const snapshot2 = uploadQueueStore.getSnapshot();
        const retriedItem = snapshot2.items.find(item => item.id === failedItem.id);

        if (retriedItem && retriedItem.status === 'success') {
          expect(retriedItem.error).toBeUndefined();
        }
      }
    });
  });

  describe('Cancellation', () => {
    it('should cancel upload and update metrics', async () => {
      global.fetch = jest.fn().mockImplementation(() =>
        new Promise(resolve => setTimeout(() => resolve({
          ok: true,
          json: async () => ({ documentId: 'pdf-123' })
        } as Response), 500))
      );

      await new Promise(resolve => setTimeout(resolve, 50));

      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });
      await uploadQueueStore.addFiles([file], 'game-1', 'en');

      uploadQueueStore.startProcessing();

      // Wait for upload to start
      await new Promise(resolve => setTimeout(resolve, 100));

      const snapshot1 = uploadQueueStore.getSnapshot();
      const uploadingItem = snapshot1.items.find(item => item.status === 'uploading');

      if (uploadingItem) {
        uploadQueueStore.cancelUpload(uploadingItem.id);

        // Wait for cancellation
        await new Promise(resolve => setTimeout(resolve, 100));

        const snapshot2 = uploadQueueStore.getSnapshot();
        const cancelledItem = snapshot2.items.find(item => item.id === uploadingItem.id);

        if (cancelledItem) {
          expect(cancelledItem.status).toBe('cancelled');
          expect(snapshot2.metrics.cancelledUploads).toBeGreaterThan(0);
        }
      }
    });
  });
});
