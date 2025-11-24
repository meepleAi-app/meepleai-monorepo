/**
 * FE-TEST-010b: Web Worker Persistence Layer Tests
 *
 * Tests focusing on localStorage persistence:
 * - Save queue state to localStorage
 * - Restore queue state from localStorage
 * - Persist only pending and failed items
 * - Clear localStorage on clearAll
 * - Handle corrupted localStorage data
 * - Update localStorage on state changes
 * - Preserve metrics in localStorage
 * - Handle empty localStorage
 * - Persist failed items for retry
 * - Include timestamp in persisted data
 *
 * Split from: useUploadQueue.worker.test.ts (1833 lines → ~450 lines)
 */

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

describe.skip('FE-TEST-010b: Persistence Layer Tests', () => {
  let mockWorkerInstance: ReturnType<typeof getMockWorkerInstance>;

  beforeEach(async () => {
    mockWorkerInstance = getMockWorkerInstance();
    await setupWorkerTestEnvironment(mockWorkerInstance, localStorageMock);
  });

  afterEach(async () => {
    await cleanupWorkerTestEnvironment();
  });

  it('should save queue state to localStorage', async () => {
    const { result } = renderHook(() => useUploadQueue());
    const file = createTestPdfFile();

    await act(async () => {
      await result.current.addFiles([file], TEST_GAME_ID, 'en');
    });

    await waitFor(() => {
      expect(localStorage.setItem).toHaveBeenCalledWith('meepleai-upload-queue', expect.any(String));
    });
  });

  it('should restore queue state from localStorage', async () => {
    // Prepare saved state
    const savedState = {
      items: [
        {
          id: 'restored-1',
          file: {
            name: 'restored.pdf',
            size: 1000,
            type: 'application/pdf',
            lastModified: Date.now(),
          },
          gameId: TEST_GAME_ID,
          language: 'en',
          status: 'pending',
          progress: 0,
          retryCount: 0,
          createdAt: Date.now(),
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

    localStorageMock['meepleai-upload-queue'] = JSON.stringify(savedState);

    // Create new worker to trigger restoration
    uploadQueueStore.destroy();
    mockWorkerInstance.terminate();

    const { result } = renderHook(() => useUploadQueue());

    await waitFor(
      () => {
        expect(result.current.queue.length).toBe(1);
        expect(result.current.queue[0].file.name).toBe('restored.pdf');
      },
      { timeout: 2000 }
    );
  });

  it('should only persist pending and failed items', async () => {
    const { result } = renderHook(() => useUploadQueue());
    const files = createTestPdfFiles(2);

    await act(async () => {
      await result.current.addFiles(files, TEST_GAME_ID, 'en');
    });

    await waitFor(
      () => {
        const stats = result.current.getStats();
        return stats.succeeded === 2;
      },
      { timeout: 5000 }
    );

    // Check what was saved
    await waitFor(() => {
      const lastCall = (localStorage.setItem as Mock).mock.calls.slice(-1)[0];
      if (lastCall) {
        const savedData = JSON.parse(lastCall[1]);
        // Successful items should not be persisted
        expect(savedData.items.length).toBe(0);
      }
    });
  });

  it('should clear localStorage on clearAll', async () => {
    const { result } = renderHook(() => useUploadQueue());
    const file = createTestPdfFile();

    await act(async () => {
      await result.current.addFiles([file], TEST_GAME_ID, 'en');
    });

    await waitForQueueLength(result, 1);

    act(() => {
      result.current.clearAll();
    });

    await waitFor(() => {
      expect(localStorage.removeItem).toHaveBeenCalledWith('meepleai-upload-queue');
    });
  });

  it('should handle corrupted localStorage data gracefully', async () => {
    localStorageMock['meepleai-upload-queue'] = 'invalid json {{{';

    uploadQueueStore.destroy();
    mockWorkerInstance.terminate();

    const { result } = renderHook(() => useUploadQueue());

    // Should not crash
    await waitFor(() => {
      // @ts-expect-error - Testing planned worker properties
      expect(result.current.isWorkerReady).toBe(true);
    });

    expect(result.current.queue.length).toBe(0);
  });

  it('should update localStorage on state changes', async () => {
    const { result } = renderHook(() => useUploadQueue());
    const file = createTestPdfFile();

    const setItemCallsBefore = (localStorage.setItem as Mock).mock.calls.length;

    await act(async () => {
      await result.current.addFiles([file], TEST_GAME_ID, 'en');
    });

    await waitFor(() => {
      const setItemCallsAfter = (localStorage.setItem as Mock).mock.calls.length;
      expect(setItemCallsAfter).toBeGreaterThan(setItemCallsBefore);
    });
  });

  it('should preserve metrics in localStorage', async () => {
    const { result } = renderHook(() => useUploadQueue());
    const files = createTestPdfFiles(2);

    await act(async () => {
      await result.current.addFiles(files, TEST_GAME_ID, 'en');
    });

    await waitFor(() => {
      const lastCall = (localStorage.setItem as Mock).mock.calls.slice(-1)[0];
      if (lastCall) {
        const savedData = JSON.parse(lastCall[1]);
        expect(savedData.metrics).toBeDefined();
        expect(savedData.metrics.totalUploads).toBeGreaterThan(0);
      }
    });
  });

  it('should handle empty localStorage gracefully', async () => {
    delete localStorageMock['meepleai-upload-queue'];

    uploadQueueStore.destroy();
    mockWorkerInstance.terminate();

    const { result } = renderHook(() => useUploadQueue());

    await waitFor(() => {
      // @ts-expect-error - Testing planned worker properties
      expect(result.current.isWorkerReady).toBe(true);
    });

    expect(result.current.queue.length).toBe(0);
  });

  it('should persist failed items for retry', async () => {
    (global.fetch as Mock).mockRejectedValue(new Error('Network error'));

    const { result } = renderHook(() => useUploadQueue());
    const file = createTestPdfFile();

    await act(async () => {
      await result.current.addFiles([file], TEST_GAME_ID, 'en');
    });

    await waitForItemStatus(result, 'failed', { timeout: 10000 });

    await waitFor(() => {
      const lastCall = (localStorage.setItem as Mock).mock.calls.slice(-1)[0];
      if (lastCall) {
        const savedData = JSON.parse(lastCall[1]);
        // Failed items should be persisted
        const failedItems = savedData.items.filter((item: any) => item.status === 'failed');
        expect(failedItems.length).toBeGreaterThan(0);
      }
    });
  });

  it('should include timestamp in persisted data', async () => {
    const { result } = renderHook(() => useUploadQueue());
    const file = createTestPdfFile();

    await act(async () => {
      await result.current.addFiles([file], TEST_GAME_ID, 'en');
    });

    await waitFor(() => {
      const lastCall = (localStorage.setItem as Mock).mock.calls.slice(-1)[0];
      if (lastCall) {
        const savedData = JSON.parse(lastCall[1]);
        expect(savedData.savedAt).toBeDefined();
        expect(typeof savedData.savedAt).toBe('number');
      }
    });
  });
});
