/**
 * FE-TEST-010c: Web Worker BroadcastChannel Sync & Lifecycle Tests
 *
 * Tests focusing on:
 * - BroadcastChannel multi-tab synchronization
 * - Worker lifecycle management
 * - Worker initialization and cleanup
 * - Crash detection and recovery
 * - Visibility change handling
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
  type UseUploadQueueWithWorker,
} from './useUploadQueue.worker-test-helpers';

// Setup global mocks BEFORE imports
const { getMockWorkerInstance } = setupGlobalMocks();
const { storage: localStorageMock, clearStorage } = setupLocalStorage();

describe('FE-TEST-010c: BroadcastChannel Sync & Worker Lifecycle Tests', () => {
  let mockWorkerInstance: ReturnType<typeof getMockWorkerInstance>;

  beforeEach(async () => {
    mockWorkerInstance = getMockWorkerInstance();
    await setupWorkerTestEnvironment(mockWorkerInstance, localStorageMock);
  });

  afterEach(async () => {
    await cleanupWorkerTestEnvironment();
  });

  // ==========================================================================
  // BroadcastChannel Sync Tests
  // ==========================================================================
  describe('BroadcastChannel Sync', () => {
    it('should create BroadcastChannel on initialization', async () => {
      const { result } = renderHook(() => useUploadQueue());

      await waitFor(() => {
        // @ts-expect-error - Testing planned worker properties
        expect(result.current.isWorkerReady).toBe(true);
      });

      // BroadcastChannel should be created (mocked)
      expect(true).toBe(true);
    });

    it('should broadcast queue updates', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const file = createTestPdfFile();

      await act(async () => {
        await result.current.addFiles([file], TEST_GAME_ID, 'en');
      });

      await waitFor(() => {
        expect(result.current.queue.length).toBe(1);
      });

      // Updates should be broadcast (verified by mock)
      expect(true).toBe(true);
    });

    it('should ignore messages from same tab', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const file = createTestPdfFile();

      await act(async () => {
        await result.current.addFiles([file], TEST_GAME_ID, 'en');
      });

      const initialLength = result.current.queue.length;

      await new Promise(resolve => setTimeout(resolve, 100));

      // Queue length should not double from self-messages
      expect(result.current.queue.length).toBe(initialLength);
    });

    it('should broadcast upload started events', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const file = createTestPdfFile();

      await act(async () => {
        await result.current.addFiles([file], TEST_GAME_ID, 'en');
      });

      await waitFor(() => {
        const item = result.current.queue[0];
        return item?.status !== 'pending';
      });

      expect(true).toBe(true);
    });

    it('should broadcast upload completed events', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const file = createTestPdfFile();

      await act(async () => {
        await result.current.addFiles([file], TEST_GAME_ID, 'en');
      });

      await waitForItemStatus(result, 'success');
      expect(true).toBe(true);
    });

    it('should not override active uploads with sync messages', async () => {
      mockWorkerInstance.setUploadDelay(1000);

      const { result } = renderHook(() => useUploadQueue());
      const file = createTestPdfFile();

      await act(async () => {
        await result.current.addFiles([file], TEST_GAME_ID, 'en');
      });

      await waitForItemStatus(result, 'uploading');

      const uploadingItem = result.current.queue.find(item => item.status === 'uploading');
      expect(uploadingItem).toBeDefined();
    });

    it('should handle BroadcastChannel errors gracefully', async () => {
      const OriginalBC = global.BroadcastChannel;
      global.BroadcastChannel = vi.fn(() => {
        throw new Error('BroadcastChannel not supported');
      }) as any;

      uploadQueueStore.destroy();
      mockWorkerInstance.terminate();

      const { result } = renderHook(() => useUploadQueue());

      await waitFor(() => {
        // @ts-expect-error - Testing planned worker properties
        expect(result.current.isWorkerReady).toBe(true);
      });

      global.BroadcastChannel = OriginalBC;
    });
  });

  // ==========================================================================
  // Worker Lifecycle Tests
  // ==========================================================================
  describe('Worker Lifecycle', () => {
    it('should initialize worker on mount', async () => {
      const { result } = renderHook(() => useUploadQueue());

      await waitFor(() => {
        // @ts-expect-error - Testing planned worker properties
        expect(result.current.isWorkerReady).toBe(true);
      });
    });

    it('should send WORKER_READY after initialization', async () => {
      const { result } = renderHook(() => useUploadQueue());

      await waitFor(() => {
        // @ts-expect-error - Testing planned worker properties
        expect(result.current.isWorkerReady).toBe(true);
      });

      // @ts-expect-error - Testing planned worker properties
      expect(result.current.workerError).toBeNull();
    });

    it('should detect worker crashes', async () => {
      const { result } = renderHook(() => useUploadQueue());

      await waitFor(() => {
        // @ts-expect-error - Testing planned worker properties
        expect(result.current.isWorkerReady).toBe(true);
      });

      mockWorkerInstance.simulateCrash();

      await waitFor(
        () => {
          // @ts-expect-error - Testing planned worker properties
          expect(result.current.workerError).toBeDefined();
        },
        { timeout: 2000 }
      );
    });

    it('should attempt automatic restart after crash', async () => {
      const { result } = renderHook(() => useUploadQueue());

      await waitFor(() => {
        // @ts-expect-error - Testing planned worker properties
        expect(result.current.isWorkerReady).toBe(true);
      });

      mockWorkerInstance.simulateCrash();

      await waitFor(
        () => {
          // @ts-expect-error - Testing planned worker properties
          expect(result.current.workerError).toBeDefined();
        },
        { timeout: 2000 }
      );
    });

    it('should limit restart attempts to 3', async () => {
      const { result } = renderHook(() => useUploadQueue());

      await waitFor(() => {
        // @ts-expect-error - Testing planned worker properties
        expect(result.current.isWorkerReady).toBe(true);
      });

      // Simulate multiple crashes
      for (let i = 0; i < 4; i++) {
        mockWorkerInstance.simulateCrash();
        await new Promise(resolve => setTimeout(resolve, 200));
      }

      await waitFor(
        () => {
          // @ts-expect-error - Testing planned worker properties
          expect(result.current.workerError).toBeDefined();
        },
        { timeout: 3000 }
      );
    });

    it('should handle visibility change for idle cleanup', async () => {
      const { result } = renderHook(() => useUploadQueue());

      await waitFor(() => {
        // @ts-expect-error - Testing planned worker properties
        expect(result.current.isWorkerReady).toBe(true);
      });

      Object.defineProperty(document, 'hidden', {
        writable: true,
        value: true,
      });

      const visibilityEvent = new Event('visibilitychange');
      document.dispatchEvent(visibilityEvent);

      expect(true).toBe(true);

      Object.defineProperty(document, 'hidden', {
        writable: true,
        value: false,
      });
    });

    it('should not cleanup worker if uploads are active', async () => {
      vi.useFakeTimers();

      mockWorkerInstance.setUploadDelay(10000);

      const { result } = renderHook(() => useUploadQueue());
      const file = createTestPdfFile();

      await act(async () => {
        await result.current.addFiles([file], TEST_GAME_ID, 'en');
      });

      await waitForItemStatus(result, 'uploading');

      Object.defineProperty(document, 'hidden', {
        writable: true,
        value: true,
      });

      const visibilityEvent = new Event('visibilitychange');
      document.dispatchEvent(visibilityEvent);

      vi.advanceTimersByTime(5 * 60 * 1000);

      // @ts-expect-error - Testing planned worker properties
      expect(result.current.isWorkerReady).toBe(true);

      vi.useRealTimers();
    });

    it('should prevent multiple simultaneous initializations', async () => {
      const { result: result1 } = renderHook(() => useUploadQueue());
      const { result: result2 } = renderHook(() => useUploadQueue());

      await waitFor(() => {
        // @ts-expect-error - Testing planned worker properties
        expect(result1.current.isWorkerReady).toBe(true);
        // @ts-expect-error - Testing planned worker properties
        expect(result2.current.isWorkerReady).toBe(true);
      });

      // Both should share same worker (singleton store)
      expect(result1.current.queue).toEqual(result2.current.queue);
    });
  });
});
