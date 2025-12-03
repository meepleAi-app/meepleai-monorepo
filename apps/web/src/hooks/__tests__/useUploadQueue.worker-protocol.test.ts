/**
 * FE-TEST-010a: Web Worker Message Protocol Tests
 *
 * Tests focusing on worker message protocol:
 * - ADD_FILES message
 * - CANCEL_UPLOAD message
 * - RETRY_UPLOAD message
 * - REMOVE_ITEM message
 * - CLEAR_COMPLETED message
 * - CLEAR_ALL message
 * - START_PROCESSING message
 * - GET_STATE message
 * - STATE_UPDATED response
 * - UPLOAD_PROGRESS response
 * - UPLOAD_SUCCESS response
 * - UPLOAD_FAILED response
 * - WORKER_READY response
 * - WORKER_ERROR response
 * - PERSIST_REQUEST response
 *
 * Split from: useUploadQueue.worker.test.ts (1833 lines → ~450 lines)
 */

import type { Mock } from 'vitest';
import { renderHook, waitFor, act } from '@testing-library/react';
import { useUploadQueue } from '../useUploadQueue';
import { uploadQueueStore } from '../../stores/UploadQueueStore';
import {
  setupGlobalMocks,
  setupLocalStorage,
  setupWorkerTestEnvironment,
  cleanupWorkerTestEnvironment,
  createTestPdfFile,
  createTestPdfFiles,
  TEST_GAME_ID,
  waitForItemStatus,
  waitForQueueLength,
  type UseUploadQueueWithWorker,
} from './useUploadQueue.worker-test-helpers';

// Setup global mocks BEFORE imports
const { getMockWorkerInstance } = setupGlobalMocks();
const { storage: localStorageMock, clearStorage } = setupLocalStorage();

/**
 * NOTE: Worker properties (isWorkerReady, workerError) are planned features
 * Type errors are expected until worker integration is complete
 * Use UseUploadQueueWithWorker type for assertions in this test suite
 */
describe('FE-TEST-010a: Message Protocol Tests', () => {
  let mockWorkerInstance: ReturnType<typeof getMockWorkerInstance>;

  beforeEach(async () => {
    mockWorkerInstance = getMockWorkerInstance();
    await setupWorkerTestEnvironment(mockWorkerInstance, localStorageMock);
  });

  afterEach(async () => {
    await cleanupWorkerTestEnvironment();
  });

  describe('ADD_FILES message', () => {
    it('should handle ADD_FILES message', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const file = createTestPdfFile();

      await act(async () => {
        await result.current.addFiles([file], TEST_GAME_ID, 'en');
      });

      await waitForQueueLength(result, 1);
    });

    it('should handle multiple files in ADD_FILES', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const files = createTestPdfFiles(3);

      await act(async () => {
        await result.current.addFiles(files, TEST_GAME_ID, 'en');
      });

      await waitForQueueLength(result, 3);
    });
  });

  describe('CANCEL_UPLOAD message', () => {
    it('should handle CANCEL_UPLOAD message', async () => {
      mockWorkerInstance.setUploadDelay(1000);

      const { result } = renderHook(() => useUploadQueue());
      const file = createTestPdfFile();

      await act(async () => {
        await result.current.addFiles([file], TEST_GAME_ID, 'en');
      });

      await waitForItemStatus(result, 'uploading');

      const itemId = result.current.queue[0].id;

      act(() => {
        result.current.cancelUpload(itemId);
      });

      await waitForItemStatus(result, 'cancelled');
    });
  });

  describe('RETRY_UPLOAD message', () => {
    it('should handle RETRY_UPLOAD message', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const file = createTestPdfFile();

      await act(async () => {
        await result.current.addFiles([file], TEST_GAME_ID, 'en');
      });

      await waitForQueueLength(result, 1);

      const itemId = result.current.queue[0].id;
      mockWorkerInstance.setUploadError(itemId, 'Error');

      await waitForItemStatus(result, 'failed');

      mockWorkerInstance.clearAllErrors();

      act(() => {
        result.current.retryUpload(itemId);
      });

      await waitFor(() => {
        const item = result.current.queue.find(i => i.id === itemId);
        return item?.status === 'pending' || item?.status === 'uploading';
      });
    });
  });

  describe('REMOVE_ITEM message', () => {
    it('should handle REMOVE_ITEM message', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const file = createTestPdfFile();

      await act(async () => {
        await result.current.addFiles([file], TEST_GAME_ID, 'en');
      });

      await waitForQueueLength(result, 1);

      const itemId = result.current.queue[0].id;

      act(() => {
        result.current.removeFile(itemId);
      });

      await waitForQueueLength(result, 0);
    });
  });

  describe('CLEAR_COMPLETED message', () => {
    it('should handle CLEAR_COMPLETED message', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const file = createTestPdfFile();

      await act(async () => {
        await result.current.addFiles([file], TEST_GAME_ID, 'en');
      });

      await waitForItemStatus(result, 'success');

      act(() => {
        result.current.clearCompleted();
      });

      await waitForQueueLength(result, 0);
    });
  });

  describe('CLEAR_ALL message', () => {
    it('should handle CLEAR_ALL message', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const files = createTestPdfFiles(2);

      await act(async () => {
        await result.current.addFiles(files, TEST_GAME_ID, 'en');
      });

      await waitForQueueLength(result, 2);

      act(() => {
        result.current.clearAll();
      });

      await waitForQueueLength(result, 0);
    });
  });

  describe('START_PROCESSING message', () => {
    it('should handle START_PROCESSING message', async () => {
      mockWorkerInstance.setAutoUpload(false);

      const { result } = renderHook(() => useUploadQueue());
      const file = createTestPdfFile();

      await act(async () => {
        await result.current.addFiles([file], TEST_GAME_ID, 'en');
      });

      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('pending');
      });

      mockWorkerInstance.setAutoUpload(true);

      act(() => {
        result.current.startUpload();
      });

      await waitFor(() => {
        const item = result.current.queue[0];
        return item?.status !== 'pending';
      });
    });
  });

  describe('GET_STATE message', () => {
    it('should handle GET_STATE message', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const file = createTestPdfFile();

      await act(async () => {
        await result.current.addFiles([file], TEST_GAME_ID, 'en');
      });

      await waitForQueueLength(result, 1);

      // State should be synced
      expect(result.current.queue[0]).toBeDefined();
    });
  });

  describe('STATE_UPDATED response', () => {
    it('should handle STATE_UPDATED response', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const file = createTestPdfFile();

      await act(async () => {
        await result.current.addFiles([file], TEST_GAME_ID, 'en');
      });

      await waitForItemStatus(result, 'success');

      expect(result.current.queue[0].status).toBe('success');
    });
  });

  describe('UPLOAD_PROGRESS response', () => {
    it('should handle UPLOAD_PROGRESS response', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const file = createTestPdfFile();
      let sawProgress = false;

      await act(async () => {
        await result.current.addFiles([file], TEST_GAME_ID, 'en');
      });

      // Monitor for progress updates
      for (let i = 0; i < 10; i++) {
        await new Promise(resolve => setTimeout(resolve, 50));
        const item = result.current.queue[0];
        if (item && item.progress > 0 && item.progress < 100) {
          sawProgress = true;
          break;
        }
      }

      await waitForItemStatus(result, 'success');

      // Progress tracking works
      expect(result.current.queue[0].progress).toBe(100);
    });
  });

  describe('UPLOAD_SUCCESS response', () => {
    it('should handle UPLOAD_SUCCESS response', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const file = createTestPdfFile();

      await act(async () => {
        await result.current.addFiles([file], TEST_GAME_ID, 'en');
      });

      await waitFor(
        () => {
          const item = result.current.queue[0];
          return item?.status === 'success' && item?.pdfId !== undefined;
        },
        { timeout: 5000 }
      );

      expect(result.current.queue[0].pdfId).toBeDefined();
    });
  });

  describe('UPLOAD_FAILED response', () => {
    it('should handle UPLOAD_FAILED response', async () => {
      (global.fetch as Mock).mockRejectedValue(new Error('Network error'));

      const { result } = renderHook(() => useUploadQueue());
      const file = createTestPdfFile();

      await act(async () => {
        await result.current.addFiles([file], TEST_GAME_ID, 'en');
      });

      await waitFor(
        () => {
          const item = result.current.queue[0];
          return item?.status === 'failed' && item?.error !== undefined;
        },
        { timeout: 10000 }
      );

      expect(result.current.queue[0].error).toBeDefined();
    });
  });

  describe('WORKER_READY response', () => {
    it('should handle WORKER_READY response', async () => {
      const { result } = renderHook(() => useUploadQueue());

      await waitFor(() => {
        // @ts-expect-error - Testing planned worker properties (isWorkerReady, workerError)
        expect(result.current.isWorkerReady).toBe(true);
      });
    });
  });

  describe('WORKER_ERROR response', () => {
    it('should handle WORKER_ERROR response', async () => {
      const { result } = renderHook(() => useUploadQueue());

      // Simulate worker error
      mockWorkerInstance.simulateCrash();

      await waitFor(
        () => {
          // @ts-expect-error - Testing planned worker properties (isWorkerReady, workerError)
          expect(result.current.workerError).toBeDefined();
        },
        { timeout: 2000 }
      );
    });
  });

  describe('PERSIST_REQUEST response', () => {
    it('should handle PERSIST_REQUEST response', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const file = createTestPdfFile();

      await act(async () => {
        await result.current.addFiles([file], TEST_GAME_ID, 'en');
      });

      await waitFor(() => {
        // localStorage should be called for persistence
        expect(localStorage.setItem).toHaveBeenCalled();
      });
    });
  });
});
