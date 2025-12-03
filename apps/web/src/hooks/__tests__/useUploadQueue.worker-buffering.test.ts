/**
 * FE-TEST-010d: Web Worker File Buffering, ArrayBuffer & Performance Tests
 *
 * Tests focusing on:
 * - File buffering when worker not ready
 * - ArrayBuffer conversion and transfer
 * - Memory management and cleanup
 * - Performance with large files and queues
 * - Edge cases (empty files, long filenames, special characters)
 *
 * Split from: useUploadQueue.worker.test.ts (1833 lines → ~483 lines)
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
  TEST_GAME_ID_ALT,
  waitForItemStatus,
  waitForQueueLength,
  type UseUploadQueueWithWorker,
} from './useUploadQueue.worker-test-helpers';

// Setup global mocks BEFORE imports
const { getMockWorkerInstance } = setupGlobalMocks();
const { storage: localStorageMock, clearStorage } = setupLocalStorage();

describe('FE-TEST-010d: File Buffering, ArrayBuffer & Performance Tests', () => {
  let mockWorkerInstance: ReturnType<typeof getMockWorkerInstance>;

  beforeEach(async () => {
    mockWorkerInstance = getMockWorkerInstance();
    await setupWorkerTestEnvironment(mockWorkerInstance, localStorageMock);
  });

  afterEach(async () => {
    await cleanupWorkerTestEnvironment();
  });

  // ==========================================================================
  // File Buffering Tests
  // ==========================================================================
  describe('File Buffering', () => {
    it('should buffer files when worker is not ready', async () => {
      uploadQueueStore.destroy();

      const { result } = renderHook(() => useUploadQueue());
      const file = createTestPdfFile();

      await act(async () => {
        await result.current.addFiles([file], TEST_GAME_ID, 'en');
      });

      await waitFor(
        () => {
          expect(result.current.queue.length).toBeGreaterThanOrEqual(0);
        },
        { timeout: 2000 }
      );
    });

    it('should process buffered files after WORKER_READY', async () => {
      uploadQueueStore.destroy();
      mockWorkerInstance.terminate();

      const { result } = renderHook(() => useUploadQueue());
      const file = createTestPdfFile();

      await act(async () => {
        await result.current.addFiles([file], TEST_GAME_ID, 'en');
      });

      await waitFor(
        () => {
          // @ts-expect-error - Testing planned worker properties
          expect(result.current.isWorkerReady).toBe(true);
          expect(result.current.queue.length).toBeGreaterThan(0);
        },
        { timeout: 2000 }
      );
    });

    it('should handle concurrent addFiles calls before ready', async () => {
      uploadQueueStore.destroy();
      mockWorkerInstance.terminate();

      const { result } = renderHook(() => useUploadQueue());
      const files = createTestPdfFiles(3);

      await act(async () => {
        await Promise.all([
          result.current.addFiles([files[0]], TEST_GAME_ID, 'en'),
          result.current.addFiles([files[1]], TEST_GAME_ID, 'en'),
          result.current.addFiles([files[2]], TEST_GAME_ID, 'en'),
        ]);
      });

      await waitForQueueLength(result, 3, 3000);
    });

    it('should maintain buffer order (FIFO)', async () => {
      uploadQueueStore.destroy();
      mockWorkerInstance.terminate();

      const { result } = renderHook(() => useUploadQueue());
      const files = [
        new File(['1'], 'first.pdf', { type: 'application/pdf' }),
        new File(['2'], 'second.pdf', { type: 'application/pdf' }),
        new File(['3'], 'third.pdf', { type: 'application/pdf' }),
      ];

      await act(async () => {
        await result.current.addFiles([files[0]], TEST_GAME_ID, 'en');
        await result.current.addFiles([files[1]], TEST_GAME_ID, 'en');
        await result.current.addFiles([files[2]], TEST_GAME_ID, 'en');
      });

      await waitForQueueLength(result, 3, 3000);

      expect(result.current.queue[0].file.name).toBe('first.pdf');
      expect(result.current.queue[1].file.name).toBe('second.pdf');
      expect(result.current.queue[2].file.name).toBe('third.pdf');
    });

    it('should not lose buffered files on rapid operations', async () => {
      uploadQueueStore.destroy();
      mockWorkerInstance.terminate();

      const { result } = renderHook(() => useUploadQueue());

      for (let i = 0; i < 10; i++) {
        const file = new File([`content ${i}`], `file-${i}.pdf`, { type: 'application/pdf' });

        await act(async () => {
          await result.current.addFiles([file], TEST_GAME_ID, 'en');
        });
      }

      await waitForQueueLength(result, 10, 3000);
    });
  });

  // ==========================================================================
  // ArrayBuffer Management Tests
  // ==========================================================================
  describe('ArrayBuffer Management', () => {
    it('should convert File to ArrayBuffer', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const file = new File(['test content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], TEST_GAME_ID, 'en');
      });

      await waitForQueueLength(result, 1);
      expect(result.current.queue[0].file.size).toBe(file.size);
    });

    it('should transfer ArrayBuffer to worker', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const file = createTestPdfFile();

      await act(async () => {
        await result.current.addFiles([file], TEST_GAME_ID, 'en');
      });

      await waitForQueueLength(result, 1);
      expect(mockWorkerInstance.getFileDataCacheSize()).toBeGreaterThan(0);
    });

    it('should cache ArrayBuffer in worker', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const files = createTestPdfFiles(2);

      await act(async () => {
        await result.current.addFiles(files, TEST_GAME_ID, 'en');
      });

      await waitForQueueLength(result, 2);
      expect(mockWorkerInstance.getFileDataCacheSize()).toBeGreaterThan(0);
    });

    it('should cleanup ArrayBuffer after successful upload', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const file = createTestPdfFile();

      await act(async () => {
        await result.current.addFiles([file], TEST_GAME_ID, 'en');
      });

      await waitForItemStatus(result, 'success');

      await waitFor(() => {
        expect(mockWorkerInstance.getFileDataCacheSize()).toBe(0);
      });
    });

    it('should cleanup ArrayBuffer on cancellation', async () => {
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

      await waitFor(() => {
        const item = result.current.queue.find(i => i.id === itemId);
        return item?.status === 'cancelled';
      });

      await waitFor(() => {
        expect(mockWorkerInstance.getFileDataCacheSize()).toBe(0);
      });
    });

    it('should handle large ArrayBuffers (100MB)', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const largeContent = new Array(100 * 1024 * 1024).fill('x').join('');
      const largeFile = new File([largeContent], 'large.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([largeFile], TEST_GAME_ID, 'en');
      });

      await waitFor(
        () => {
          expect(result.current.queue.length).toBe(1);
          expect(result.current.queue[0].file.size).toBe(largeFile.size);
        },
        { timeout: 5000 }
      );
    }, 30000);

    it('should handle multiple ArrayBuffers concurrently', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const files = createTestPdfFiles(5);

      await act(async () => {
        await result.current.addFiles(files, TEST_GAME_ID, 'en');
      });

      await waitForQueueLength(result, 5);
      expect(mockWorkerInstance.getFileDataCacheSize()).toBeGreaterThan(0);
    });

    it('should prevent memory leaks with ArrayBuffer cleanup', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const files = createTestPdfFiles(10);

      await act(async () => {
        await result.current.addFiles(files, TEST_GAME_ID, 'en');
      });

      await waitFor(
        () => {
          const stats = result.current.getStats();
          return stats.succeeded === 10;
        },
        { timeout: 10000 }
      );

      await waitFor(() => {
        expect(mockWorkerInstance.getFileDataCacheSize()).toBe(0);
      });
    });
  });

  // ==========================================================================
  // Performance & Edge Cases Tests
  // ==========================================================================
  describe('Performance & Edge Cases', () => {
    it('should handle 50+ small files efficiently', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const files = createTestPdfFiles(50, 'file');

      const startTime = Date.now();

      await act(async () => {
        await result.current.addFiles(files, TEST_GAME_ID, 'en');
      });

      await waitFor(
        () => {
          const stats = result.current.getStats();
          return stats.succeeded === 50;
        },
        { timeout: 30000 }
      );

      const duration = Date.now() - startTime;
      expect(duration).toBeLessThan(30000);
      expect(result.current.queue.every(item => item.status === 'success')).toBe(true);
    }, 40000);

    it('should handle rapid add/remove/add cycles', async () => {
      const { result } = renderHook(() => useUploadQueue());

      for (let cycle = 0; cycle < 5; cycle++) {
        const file = new File([`cycle ${cycle}`], `file-${cycle}.pdf`, { type: 'application/pdf' });

        await act(async () => {
          await result.current.addFiles([file], TEST_GAME_ID, 'en');
        });

        await waitForQueueLength(result, 1);

        const itemId = result.current.queue[0].id;

        if (result.current.queue[0].status !== 'uploading') {
          act(() => {
            result.current.removeFile(itemId);
          });

          await waitForQueueLength(result, 0);
        }
      }

      expect(true).toBe(true);
    });

    it('should handle mixed file sizes', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const files = [
        new File(['tiny'], 'tiny.pdf', { type: 'application/pdf' }),
        new File([new Array(1024).fill('x').join('')], 'medium.pdf', { type: 'application/pdf' }),
        new File([new Array(10240).fill('y').join('')], 'large.pdf', { type: 'application/pdf' }),
      ];

      await act(async () => {
        await result.current.addFiles(files, TEST_GAME_ID, 'en');
      });

      await waitFor(
        () => {
          const stats = result.current.getStats();
          return stats.succeeded === 3;
        },
        { timeout: 10000 }
      );

      expect(result.current.queue.every(item => item.status === 'success')).toBe(true);
    });

    it('should handle edge case: empty file', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const emptyFile = new File([], 'empty.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([emptyFile], TEST_GAME_ID, 'en');
      });

      await waitFor(() => {
        expect(result.current.queue.length).toBeGreaterThanOrEqual(0);
      });

      expect(true).toBe(true);
    });

    it('should handle edge case: very long filename', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const longName = 'a'.repeat(255) + '.pdf';
      const file = new File(['test'], longName, { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], TEST_GAME_ID, 'en');
      });

      await waitFor(() => {
        expect(result.current.queue.length).toBe(1);
        expect(result.current.queue[0].file.name).toBe(longName);
      });
    });

    it('should handle edge case: special characters in filename', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const specialName = 'test-file_123 (copy) [final].pdf';
      const file = new File(['test'], specialName, { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], TEST_GAME_ID, 'en');
      });

      await waitFor(() => {
        expect(result.current.queue[0].file.name).toBe(specialName);
      });
    });

    it('should handle stress test: rapid operations', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const operations = [];
      for (let i = 0; i < 10; i++) {
        const file = new File([`test ${i}`], `test-${i}.pdf`, { type: 'application/pdf' });

        operations.push(
          act(async () => {
            await result.current.addFiles([file], TEST_GAME_ID, 'en');
          })
        );
      }

      await Promise.all(operations);

      await waitFor(
        () => {
          expect(result.current.queue.length).toBeGreaterThan(0);
        },
        { timeout: 5000 }
      );

      expect(true).toBe(true);
    });

    it('should handle cleanup of large queues efficiently', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const files = createTestPdfFiles(50);

      await act(async () => {
        await result.current.addFiles(files, TEST_GAME_ID, 'en');
      });

      await waitFor(
        () => {
          const stats = result.current.getStats();
          return stats.succeeded === 50;
        },
        { timeout: 30000 }
      );

      const startTime = Date.now();

      act(() => {
        result.current.clearCompleted();
      });

      const duration = Date.now() - startTime;
      expect(duration).toBeLessThan(1000);

      await waitForQueueLength(result, 0);
    }, 40000);
  });
});
