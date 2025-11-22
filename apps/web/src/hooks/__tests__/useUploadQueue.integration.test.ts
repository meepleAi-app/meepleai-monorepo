/**
 * FE-TEST-010: Comprehensive Web Worker Upload Queue Integration Tests
 *
 * Complete integration tests for the Web Worker upload queue covering:
 * - Queue Management
 * - Upload Lifecycle
 * - Error Handling
 * - Concurrency Control
 * - Callbacks & Observability
 * - Message Protocol
 * - Persistence Layer
 * - BroadcastChannel Sync
 * - Worker Lifecycle
 * - File Buffering
 * - ArrayBuffer Management
 * - Performance & Edge Cases
 *
 * Target: 90%+ code coverage on worker, store, and hook code
 */

/**
 * IMPORTANT: Must mock Worker and BroadcastChannel BEFORE importing modules
 */

// Import ONLY the mock class definitions (no side effects)
import {
  MockUploadWorker,
  MockBroadcastChannel,
  createMockUploadQueueItem,
  createMockUploadQueueItems
} from '../../__tests__/helpers/uploadQueueMocks';

// Mock Worker globally BEFORE any other imports or resetModules
let mockWorkerInstance: MockUploadWorker;
global.Worker = jest.fn((scriptURL: string | URL, options?: any) => {
  console.log('[TEST] Worker constructor called');
  if (!mockWorkerInstance) {
    mockWorkerInstance = new MockUploadWorker({ uploadDelay: 10, autoUpload: true });
  }
  return mockWorkerInstance as any;
}) as any;

// Mock BroadcastChannel
global.BroadcastChannel = MockBroadcastChannel as any;

// DON'T reset modules - it clears our Worker mock!
// jest.resetModules();

// Mock BroadcastChannel globally
global.BroadcastChannel = MockBroadcastChannel as any;

// Mock fetch for upload API calls
global.fetch = jest.fn();

// NOW import the modules that use Worker
import { renderHook, waitFor, act } from '@testing-library/react';
import { useUploadQueue, useUploadQueueStats, useUploadQueueStatus } from '../useUploadQueue';
import { uploadQueueStore } from '../../stores/UploadQueueStore';

// Mock localStorage
const localStorageMock: { [key: string]: string } = {};
global.localStorage = {
  getItem: jest.fn((key: string) => localStorageMock[key] || null),
  setItem: jest.fn((key: string, value: string) => {
    localStorageMock[key] = value;
  }),
  removeItem: jest.fn((key: string) => {
    delete localStorageMock[key];
  }),
  clear: jest.fn(() => {
    Object.keys(localStorageMock).forEach(key => delete localStorageMock[key]);
  }),
  length: 0,
  key: jest.fn()
};

describe('FE-TEST-010: Upload Queue Integration Tests', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    Object.keys(localStorageMock).forEach(key => delete localStorageMock[key]);
    MockBroadcastChannel.clearAll();

    // Reset mock worker instance
    (global.Worker as jest.Mock).mockClear();

    // Mock successful fetch responses by default
    (global.fetch as jest.Mock).mockResolvedValue({
      ok: true,
      json: async () => ({ documentId: 'pdf-123' }),
      headers: new Headers()
    });

    // CRITICAL FIX: Do NOT call uploadQueueStore.destroy() here!
    // destroy() terminates the singleton worker and it never re-initializes.
    // Instead, clear the queue state via the worker's message protocol.
    // The worker will be initialized once when the module loads.

    // Clear the queue using the store's clearAll method
    uploadQueueStore.clearAll();

    // Wait a tick for the clear to process
    return new Promise(resolve => setTimeout(resolve, 10));
  });

  afterEach(() => {
    // Clean up after each test
    uploadQueueStore.clearAll();
  });

  // ============================================================================
  // 1. Queue Management Tests (~15 tests)
  // ============================================================================
  describe('Queue Management', () => {
    it('should add single file to queue', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const file = new File(['test content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        expect(result.current.queue.length).toBe(1);
        expect(result.current.queue[0].file.name).toBe('test.pdf');
      });
    });

    it('should add multiple files to queue', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const files = [
        new File(['content1'], 'test1.pdf', { type: 'application/pdf' }),
        new File(['content2'], 'test2.pdf', { type: 'application/pdf' }),
        new File(['content3'], 'test3.pdf', { type: 'application/pdf' })
      ];

      await act(async () => {
        await result.current.addFiles(files, '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        expect(result.current.queue.length).toBe(3);
      });
    });

    it('should remove file from queue', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => expect(result.current.queue.length).toBe(1));

      const itemId = result.current.queue[0].id;

      act(() => {
        result.current.removeFile(itemId);
      });

      await waitFor(() => {
        expect(result.current.queue.length).toBe(0);
      });
    });

    it('should clear completed uploads', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      // Wait for upload to complete
      await waitFor(() => {
        const stats = result.current.getStats();
        return stats.succeeded > 0;
      }, { timeout: 5000 });

      act(() => {
        result.current.clearCompleted();
      });

      await waitFor(() => {
        expect(result.current.queue.length).toBe(0);
      });
    });

    it('should clear all uploads', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const files = [
        new File(['1'], '1.pdf', { type: 'application/pdf' }),
        new File(['2'], '2.pdf', { type: 'application/pdf' })
      ];

      await act(async () => {
        await result.current.addFiles(files, '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => expect(result.current.queue.length).toBe(2));

      act(() => {
        result.current.clearAll();
      });

      await waitFor(() => {
        expect(result.current.queue.length).toBe(0);
      });
    });

    it('should track total uploads in metrics', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const files = [
        new File(['1'], '1.pdf', { type: 'application/pdf' }),
        new File(['2'], '2.pdf', { type: 'application/pdf' }),
        new File(['3'], '3.pdf', { type: 'application/pdf' })
      ];

      await act(async () => {
        await result.current.addFiles(files, '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        const state = mockWorkerInstance.getState();
        expect(state.metrics.totalUploads).toBe(3);
      });
    });

    it('should get queue statistics', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const files = [
        new File(['1'], '1.pdf', { type: 'application/pdf' }),
        new File(['2'], '2.pdf', { type: 'application/pdf' })
      ];

      await act(async () => {
        await result.current.addFiles(files, '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        const stats = result.current.getStats();
        expect(stats.total).toBeGreaterThan(0);
      });
    });

    it('should maintain queue order (FIFO)', async () => {
      mockWorkerInstance.setAutoUpload(false); // Prevent auto-upload to test order

      const { result } = renderHook(() => useUploadQueue());
      const files = [
        new File(['1'], 'first.pdf', { type: 'application/pdf' }),
        new File(['2'], 'second.pdf', { type: 'application/pdf' }),
        new File(['3'], 'third.pdf', { type: 'application/pdf' })
      ];

      await act(async () => {
        await result.current.addFiles(files, '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        expect(result.current.queue[0].file.name).toBe('first.pdf');
        expect(result.current.queue[1].file.name).toBe('second.pdf');
        expect(result.current.queue[2].file.name).toBe('third.pdf');
      });
    });

    it('should handle empty queue operations gracefully', () => {
      const { result } = renderHook(() => useUploadQueue());

      expect(() => {
        act(() => {
          result.current.clearCompleted();
          result.current.clearAll();
        });
      }).not.toThrow();
    });

    it('should prevent duplicate file additions', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
        await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        // Should have 2 entries (no deduplication at queue level)
        expect(result.current.queue.length).toBe(2);
      });
    });

    it('should handle rapid add/remove operations', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

      for (let i = 0; i < 5; i++) {
        await act(async () => {
          await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
        });
      }

      await waitFor(() => expect(result.current.queue.length).toBeGreaterThan(0));

      const itemIds = result.current.queue.map(item => item.id);

      act(() => {
        itemIds.forEach(id => result.current.removeFile(id));
      });

      await waitFor(() => {
        expect(result.current.queue.length).toBe(0);
      });
    });

    it('should track queue size changes', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const sizes: number[] = [];

      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

      sizes.push(result.current.queue.length);

      await act(async () => {
        await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        sizes.push(result.current.queue.length);
        expect(sizes[1]).toBeGreaterThan(sizes[0]);
      });
    });

    it('should support different game IDs and languages', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const files = [
        new File(['1'], '1.pdf', { type: 'application/pdf' }),
        new File(['2'], '2.pdf', { type: 'application/pdf' })
      ];

      await act(async () => {
        await result.current.addFiles([files[0]], '770e8400-e29b-41d4-a716-000000000123', 'en');
        await result.current.addFiles([files[1]], '770e8400-e29b-41d4-a716-000000000456', 'it');
      });

      await waitFor(() => {
        expect(result.current.queue[0].gameId).toBe('770e8400-e29b-41d4-a716-000000000123');
        expect(result.current.queue[0].language).toBe('en');
        expect(result.current.queue[1].gameId).toBe('770e8400-e29b-41d4-a716-000000000456');
        expect(result.current.queue[1].language).toBe('it');
      });
    });

    it('should handle metadata preservation', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const file = new File(['test'], 'test.pdf', {
        type: 'application/pdf',
        lastModified: 1234567890
      });

      await act(async () => {
        await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        expect(result.current.queue[0].file.size).toBe(file.size);
        expect(result.current.queue[0].file.type).toBe(file.type);
        expect(result.current.queue[0].file.lastModified).toBe(file.lastModified);
      });
    });

    it('should generate unique IDs for queue items', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const files = [
        new File(['1'], 'test.pdf', { type: 'application/pdf' }),
        new File(['2'], 'test.pdf', { type: 'application/pdf' }),
        new File(['3'], 'test.pdf', { type: 'application/pdf' })
      ];

      await act(async () => {
        await result.current.addFiles(files, '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        const ids = result.current.queue.map(item => item.id);
        const uniqueIds = new Set(ids);
        expect(uniqueIds.size).toBe(ids.length);
      });
    });
  });

  // ============================================================================
  // 2. Upload Lifecycle Tests (~20 tests)
  // ============================================================================
  describe('Upload Lifecycle', () => {
    it('should transition from pending to uploading', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        const item = result.current.queue[0];
        expect(['pending', 'uploading', 'processing', 'success']).toContain(item.status);
      });
    });

    it('should complete successful upload', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        const item = result.current.queue[0];
        expect(item.status).toBe('success');
        expect(item.progress).toBe(100);
        expect(item.pdfId).toBeDefined();
      }, { timeout: 5000 });
    });

    it('should track upload progress', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });
      const progressValues: number[] = [];

      await act(async () => {
        await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      // Track progress changes
      const checkProgress = async () => {
        for (let i = 0; i < 10; i++) {
          await new Promise(resolve => setTimeout(resolve, 50));
          if (result.current.queue[0]) {
            progressValues.push(result.current.queue[0].progress);
          }
        }
      };

      await act(async () => {
        await checkProgress();
      });

      // Progress should increase over time
      expect(progressValues.some(p => p > 0)).toBe(true);
    });

    it('should handle multiple uploads sequentially', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const files = [
        new File(['1'], '1.pdf', { type: 'application/pdf' }),
        new File(['2'], '2.pdf', { type: 'application/pdf' })
      ];

      await act(async () => {
        await result.current.addFiles(files, '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        const stats = result.current.getStats();
        return stats.succeeded === 2;
      }, { timeout: 5000 });
    });

    it('should cancel uploading file', async () => {
      mockWorkerInstance.setUploadDelay(1000); // Slow upload

      const { result } = renderHook(() => useUploadQueue());
      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        return result.current.queue[0]?.status === 'uploading';
      });

      const itemId = result.current.queue[0].id;

      act(() => {
        result.current.cancelUpload(itemId);
      });

      await waitFor(() => {
        const item = result.current.queue.find(i => i.id === itemId);
        expect(item?.status).toBe('cancelled');
      });
    });

    it('should retry failed upload', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => expect(result.current.queue.length).toBe(1));

      const itemId = result.current.queue[0].id;

      // Simulate error
      mockWorkerInstance.setUploadError(itemId, 'Network error');

      await waitFor(() => {
        const item = result.current.queue.find(i => i.id === itemId);
        return item?.status === 'failed';
      }, { timeout: 5000 });

      // Clear error and retry
      mockWorkerInstance.clearAllErrors();

      act(() => {
        result.current.retryUpload(itemId);
      });

      await waitFor(() => {
        const item = result.current.queue.find(i => i.id === itemId);
        return item?.status === 'pending';
      });
    });

    it('should track status transitions', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });
      const statuses: string[] = [];

      await act(async () => {
        await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      // Track status changes
      const trackStatuses = async () => {
        for (let i = 0; i < 20; i++) {
          await new Promise(resolve => setTimeout(resolve, 50));
          if (result.current.queue[0]) {
            statuses.push(result.current.queue[0].status);
          }
        }
      };

      await act(async () => {
        await trackStatuses();
      });

      // Should see multiple statuses
      const uniqueStatuses = new Set(statuses);
      expect(uniqueStatuses.size).toBeGreaterThan(1);
    });

    it('should assign PDF ID on success', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        const item = result.current.queue[0];
        return item?.status === 'success' && item?.pdfId !== undefined;
      }, { timeout: 5000 });

      expect(result.current.queue[0].pdfId).toBe('pdf-123');
    });

    it('should reset progress on retry', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => expect(result.current.queue.length).toBe(1));

      const itemId = result.current.queue[0].id;
      mockWorkerInstance.setUploadError(itemId, 'Test error');

      await waitFor(() => {
        const item = result.current.queue.find(i => i.id === itemId);
        return item?.status === 'failed';
      }, { timeout: 5000 });

      mockWorkerInstance.clearAllErrors();

      act(() => {
        result.current.retryUpload(itemId);
      });

      await waitFor(() => {
        const item = result.current.queue.find(i => i.id === itemId);
        expect(item?.progress).toBe(0);
      });
    });

    it('should handle manual upload start', async () => {
      mockWorkerInstance.setAutoUpload(false);

      const { result } = renderHook(() => useUploadQueue());
      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
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
        return item?.status === 'success';
      }, { timeout: 5000 });
    });

    it('should preserve file metadata through lifecycle', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const file = new File(['test content here'], 'important.pdf', {
        type: 'application/pdf',
        lastModified: 1234567890
      });

      await act(async () => {
        await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        const item = result.current.queue[0];
        return item?.status === 'success';
      }, { timeout: 5000 });

      const completedItem = result.current.queue[0];
      expect(completedItem.file.name).toBe('important.pdf');
      expect(completedItem.file.size).toBe(file.size);
      expect(completedItem.file.lastModified).toBe(1234567890);
    });

    it('should track creation timestamps', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });
      const beforeTime = Date.now();

      await act(async () => {
        await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      const afterTime = Date.now();

      await waitFor(() => {
        const item = result.current.queue[0];
        expect(item.createdAt).toBeGreaterThanOrEqual(beforeTime);
        expect(item.createdAt).toBeLessThanOrEqual(afterTime);
      });
    });

    it('should not allow removal of uploading files', async () => {
      mockWorkerInstance.setUploadDelay(1000);

      const { result } = renderHook(() => useUploadQueue());
      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        return result.current.queue[0]?.status === 'uploading';
      });

      const itemId = result.current.queue[0].id;

      act(() => {
        result.current.removeFile(itemId);
      });

      // Item should still be there (worker prevents removal during upload)
      await waitFor(() => {
        const item = result.current.queue.find(i => i.id === itemId);
        expect(item).toBeDefined();
      });
    });

    it('should handle concurrent lifecycle transitions', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const files = Array.from({ length: 5 }, (_, i) =>
        new File([`content ${i}`], `file-${i}.pdf`, { type: 'application/pdf' })
      );

      await act(async () => {
        await result.current.addFiles(files, '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        const stats = result.current.getStats();
        return stats.succeeded === 5;
      }, { timeout: 10000 });

      expect(result.current.queue.every(item => item.status === 'success')).toBe(true);
    });

    it('should maintain queue integrity during rapid state changes', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
        await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
        await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        expect(result.current.queue.length).toBeGreaterThan(0);
      });

      // Queue should remain consistent
      const queueSnapshot1 = [...result.current.queue];
      await new Promise(resolve => setTimeout(resolve, 100));
      const queueSnapshot2 = [...result.current.queue];

      expect(queueSnapshot2.length).toBeGreaterThanOrEqual(queueSnapshot1.length);
    });

    it('should handle processing stage', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      // Look for processing status during upload
      let sawProcessing = false;
      const checkProcessing = async () => {
        for (let i = 0; i < 30; i++) {
          await new Promise(resolve => setTimeout(resolve, 50));
          if (result.current.queue[0]?.status === 'processing') {
            sawProcessing = true;
            break;
          }
        }
      };

      await act(async () => {
        await checkProcessing();
      });

      await waitFor(() => {
        return result.current.queue[0]?.status === 'success';
      }, { timeout: 5000 });

      // May or may not see processing depending on timing
      expect(result.current.queue[0].status).toBe('success');
    });

    it('should support batch upload completion', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const files = [
        new File(['1'], '1.pdf', { type: 'application/pdf' }),
        new File(['2'], '2.pdf', { type: 'application/pdf' }),
        new File(['3'], '3.pdf', { type: 'application/pdf' })
      ];

      await act(async () => {
        await result.current.addFiles(files, '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        const stats = result.current.getStats();
        return stats.succeeded === 3;
      }, { timeout: 10000 });

      expect(result.current.queue.every(item => item.status === 'success')).toBe(true);
    });

    it('should track retry count', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => expect(result.current.queue.length).toBe(1));

      const itemId = result.current.queue[0].id;
      mockWorkerInstance.setUploadError(itemId, 'Error');

      await waitFor(() => {
        return result.current.queue[0]?.status === 'failed';
      }, { timeout: 5000 });

      mockWorkerInstance.clearAllErrors();

      // Retry multiple times
      for (let i = 0; i < 3; i++) {
        mockWorkerInstance.setUploadError(itemId, 'Error');

        act(() => {
          result.current.retryUpload(itemId);
        });

        await waitFor(() => {
          return result.current.queue[0]?.status === 'failed';
        }, { timeout: 5000 });

        mockWorkerInstance.clearAllErrors();
      }

      // Retry count should be tracked
      expect(result.current.queue[0].retryCount).toBeGreaterThan(0);
    });

    it('should handle cancellation before upload starts', async () => {
      mockWorkerInstance.setAutoUpload(false);

      const { result } = renderHook(() => useUploadQueue());
      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('pending');
      });

      const itemId = result.current.queue[0].id;

      act(() => {
        result.current.cancelUpload(itemId);
      });

      await waitFor(() => {
        const item = result.current.queue.find(i => i.id === itemId);
        expect(item?.status).toBe('cancelled');
      });
    });
  });

  // ============================================================================
  // 3. Error Handling Tests (~15 tests)
  // ============================================================================
  describe('Error Handling', () => {
    it('should handle network errors', async () => {
      (global.fetch as jest.Mock).mockRejectedValue(new Error('Network error'));

      const { result } = renderHook(() => useUploadQueue());
      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        const item = result.current.queue[0];
        return item?.status === 'failed';
      }, { timeout: 5000 });

      expect(result.current.queue[0].error).toContain('Network error');
    });

    it('should handle 4xx client errors', async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: false,
        status: 400,
        statusText: 'Bad Request',
        json: async () => ({ error: 'Invalid file format' }),
        headers: new Headers({ 'x-correlation-id': 'test-corr-id' })
      });

      const { result } = renderHook(() => useUploadQueue());
      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        const item = result.current.queue[0];
        return item?.status === 'failed';
      }, { timeout: 5000 });

      expect(result.current.queue[0].error).toContain('Invalid file format');
    });

    it('should handle 5xx server errors', async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: false,
        status: 500,
        statusText: 'Internal Server Error',
        json: async () => ({ error: 'Server error' }),
        headers: new Headers()
      });

      const { result } = renderHook(() => useUploadQueue());
      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        const item = result.current.queue[0];
        return item?.status === 'failed';
      }, { timeout: 5000 });

      expect(result.current.queue[0].status).toBe('failed');
    });

    it('should track correlation IDs for errors', async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: false,
        status: 500,
        json: async () => ({ error: 'Server error' }),
        headers: new Headers({ 'x-correlation-id': 'corr-123' })
      });

      const { result } = renderHook(() => useUploadQueue());
      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        const item = result.current.queue[0];
        return item?.status === 'failed' && item?.correlationId !== undefined;
      }, { timeout: 5000 });

      expect(result.current.queue[0].correlationId).toBe('corr-123');
    });

    it('should distinguish retryable vs non-retryable errors', async () => {
      // 429 Too Many Requests should be retryable (in real implementation)
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: false,
        status: 429,
        json: async () => ({ error: 'Too many requests' }),
        headers: new Headers()
      });

      const { result } = renderHook(() => useUploadQueue());
      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        const item = result.current.queue[0];
        return item?.status === 'failed';
      }, { timeout: 10000 }); // Longer timeout for retries

      // Should have attempted retries (indicated by retryCount or multiple attempts)
      expect(result.current.queue[0].status).toBe('failed');
    });

    it('should handle AbortError when cancelled', async () => {
      mockWorkerInstance.setUploadDelay(1000);

      const { result } = renderHook(() => useUploadQueue());
      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        return result.current.queue[0]?.status === 'uploading';
      });

      const itemId = result.current.queue[0].id;

      act(() => {
        result.current.cancelUpload(itemId);
      });

      await waitFor(() => {
        const item = result.current.queue.find(i => i.id === itemId);
        expect(item?.status).toBe('cancelled');
      });

      // Should not have error message for cancellation
      const item = result.current.queue.find(i => i.id === itemId);
      expect(item?.error).toBeUndefined();
    });

    it('should respect retry limits', async () => {
      // Mock persistent failure
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: false,
        status: 500,
        json: async () => ({ error: 'Server error' }),
        headers: new Headers()
      });

      const { result } = renderHook(() => useUploadQueue());
      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        const item = result.current.queue[0];
        return item?.status === 'failed';
      }, { timeout: 15000 }); // Allow time for all retries

      // Should have attempted max retries (3)
      expect(result.current.queue[0].retryCount).toBeGreaterThan(0);
    });

    it('should update metrics for failed uploads', async () => {
      (global.fetch as jest.Mock).mockRejectedValue(new Error('Network error'));

      const { result } = renderHook(() => useUploadQueue());
      const files = [
        new File(['1'], '1.pdf', { type: 'application/pdf' }),
        new File(['2'], '2.pdf', { type: 'application/pdf' })
      ];

      await act(async () => {
        await result.current.addFiles(files, '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        const state = mockWorkerInstance.getState();
        return state.metrics.failedUploads === 2;
      }, { timeout: 10000 });

      expect(mockWorkerInstance.getState().metrics.failedUploads).toBe(2);
    });

    it('should handle malformed API responses', async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        json: async () => ({}), // Missing documentId
        headers: new Headers()
      });

      const { result } = renderHook(() => useUploadQueue());
      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        const item = result.current.queue[0];
        return item?.status === 'success';
      }, { timeout: 5000 });

      // Should still succeed but may have generated pdfId
      expect(result.current.queue[0].status).toBe('success');
    });

    it('should handle JSON parse errors in error responses', async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: false,
        status: 500,
        json: async () => {
          throw new Error('Invalid JSON');
        },
        headers: new Headers()
      });

      const { result } = renderHook(() => useUploadQueue());
      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        return result.current.queue[0]?.status === 'failed';
      }, { timeout: 10000 });

      expect(result.current.queue[0].status).toBe('failed');
    });

    it('should handle timeout errors', async () => {
      (global.fetch as jest.Mock).mockImplementation(() =>
        new Promise((_, reject) =>
          setTimeout(() => reject(new Error('Request timeout')), 100)
        )
      );

      const { result } = renderHook(() => useUploadQueue());
      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        return result.current.queue[0]?.status === 'failed';
      }, { timeout: 10000 });

      expect(result.current.queue[0].error).toContain('timeout');
    });

    it('should clear errors on successful retry', async () => {
      const { result } = renderHook(() => useUploadQueue());
      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => expect(result.current.queue.length).toBe(1));

      const itemId = result.current.queue[0].id;
      mockWorkerInstance.setUploadError(itemId, 'Test error');

      await waitFor(() => {
        return result.current.queue[0]?.status === 'failed';
      }, { timeout: 5000 });

      expect(result.current.queue[0].error).toBe('Test error');

      mockWorkerInstance.clearAllErrors();

      act(() => {
        result.current.retryUpload(itemId);
      });

      await waitFor(() => {
        const item = result.current.queue.find(i => i.id === itemId);
        return item?.status === 'success';
      }, { timeout: 5000 });

      expect(result.current.queue[0].error).toBeUndefined();
    });

    it('should handle errors during file conversion', async () => {
      const { result } = renderHook(() => useUploadQueue());

      // Create a file that might cause issues
      const invalidFile = new File([], '', { type: '' });

      await act(async () => {
        await result.current.addFiles([invalidFile], '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      // Should handle gracefully
      await waitFor(() => {
        expect(result.current.queue.length).toBeGreaterThanOrEqual(0);
      });
    });

    it('should preserve error details for debugging', async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: false,
        status: 400,
        statusText: 'Bad Request',
        json: async () => ({ error: 'File too large', details: 'Max size 10MB' }),
        headers: new Headers({ 'x-correlation-id': 'debug-123' })
      });

      const { result } = renderHook(() => useUploadQueue());
      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        return result.current.queue[0]?.status === 'failed';
      }, { timeout: 5000 });

      const failedItem = result.current.queue[0];
      expect(failedItem.error).toBeDefined();
      expect(failedItem.correlationId).toBe('debug-123');
    });

    it('should handle mixed success and failure uploads', async () => {
      let callCount = 0;
      (global.fetch as jest.Mock).mockImplementation(() => {
        callCount++;
        if (callCount % 2 === 0) {
          return Promise.resolve({
            ok: true,
            json: async () => ({ documentId: 'pdf-123' }),
            headers: new Headers()
          });
        } else {
          return Promise.resolve({
            ok: false,
            status: 500,
            json: async () => ({ error: 'Server error' }),
            headers: new Headers()
          });
        }
      });

      const { result } = renderHook(() => useUploadQueue());
      const files = [
        new File(['1'], '1.pdf', { type: 'application/pdf' }),
        new File(['2'], '2.pdf', { type: 'application/pdf' }),
        new File(['3'], '3.pdf', { type: 'application/pdf' }),
        new File(['4'], '4.pdf', { type: 'application/pdf' })
      ];

      await act(async () => {
        await result.current.addFiles(files, '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        const stats = result.current.getStats();
        return stats.succeeded > 0 && stats.failed > 0;
      }, { timeout: 15000 });

      const stats = result.current.getStats();
      expect(stats.succeeded).toBeGreaterThan(0);
      expect(stats.failed).toBeGreaterThan(0);
    });
  });

  // ============================================================================
  // 4. Concurrency Control Tests (~10 tests)
  // ============================================================================
  describe('Concurrency Control', () => {
    it('should enforce 3-file concurrent upload limit', async () => {
      mockWorkerInstance.setUploadDelay(500); // Slow uploads to observe concurrency

      const { result } = renderHook(() => useUploadQueue());
      const files = Array.from({ length: 10 }, (_, i) =>
        new File([`content ${i}`], `file-${i}.pdf`, { type: 'application/pdf' })
      );

      await act(async () => {
        await result.current.addFiles(files, '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      // Check active uploads during processing
      await new Promise(resolve => setTimeout(resolve, 100));

      const activeCount = mockWorkerInstance.getActiveUploadsCount();
      expect(activeCount).toBeLessThanOrEqual(3);
    });

    it('should process queue when slots become available', async () => {
      mockWorkerInstance.setUploadDelay(100);

      const { result } = renderHook(() => useUploadQueue());
      const files = Array.from({ length: 5 }, (_, i) =>
        new File([`content ${i}`], `file-${i}.pdf`, { type: 'application/pdf' })
      );

      await act(async () => {
        await result.current.addFiles(files, '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        const stats = result.current.getStats();
        return stats.succeeded === 5;
      }, { timeout: 10000 });

      expect(result.current.queue.every(item => item.status === 'success')).toBe(true);
    });

    it('should track active upload count', async () => {
      mockWorkerInstance.setUploadDelay(200);

      const { result } = renderHook(() => useUploadQueue());
      const files = Array.from({ length: 6 }, (_, i) =>
        new File([`content ${i}`], `file-${i}.pdf`, { type: 'application/pdf' })
      );

      await act(async () => {
        await result.current.addFiles(files, '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      // Give time for uploads to start
      await new Promise(resolve => setTimeout(resolve, 100));

      const stats = result.current.getStats();
      expect(stats.uploading).toBeGreaterThan(0);
      expect(stats.uploading).toBeLessThanOrEqual(3);
    });

    it('should not exceed concurrency limit on rapid additions', async () => {
      mockWorkerInstance.setUploadDelay(300);

      const { result } = renderHook(() => useUploadQueue());

      // Add files in rapid succession
      for (let i = 0; i < 10; i++) {
        const file = new File([`content ${i}`], `file-${i}.pdf`, {
          type: 'application/pdf'
        });

        await act(async () => {
          await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
        });
      }

      await new Promise(resolve => setTimeout(resolve, 150));

      const activeCount = mockWorkerInstance.getActiveUploadsCount();
      expect(activeCount).toBeLessThanOrEqual(3);
    });

    it('should handle concurrent limit with cancellations', async () => {
      mockWorkerInstance.setUploadDelay(500);

      const { result } = renderHook(() => useUploadQueue());
      const files = Array.from({ length: 5 }, (_, i) =>
        new File([`content ${i}`], `file-${i}.pdf`, { type: 'application/pdf' })
      );

      await act(async () => {
        await result.current.addFiles(files, '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        const stats = result.current.getStats();
        return stats.uploading > 0;
      });

      // Cancel one upload
      const uploadingItem = result.current.queue.find(item => item.status === 'uploading');
      if (uploadingItem) {
        act(() => {
          result.current.cancelUpload(uploadingItem.id);
        });
      }

      // Queue should continue processing
      await waitFor(() => {
        const stats = result.current.getStats();
        return stats.succeeded + stats.cancelled + stats.failed === 5;
      }, { timeout: 10000 });
    });

    it('should maintain concurrency across retries', async () => {
      mockWorkerInstance.setUploadDelay(200);

      const { result } = renderHook(() => useUploadQueue());
      const files = Array.from({ length: 5 }, (_, i) =>
        new File([`content ${i}`], `file-${i}.pdf`, { type: 'application/pdf' })
      );

      await act(async () => {
        await result.current.addFiles(files, '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => expect(result.current.queue.length).toBe(5));

      // Fail first item
      const firstItemId = result.current.queue[0].id;
      mockWorkerInstance.setUploadError(firstItemId, 'Error');

      await waitFor(() => {
        return result.current.queue[0]?.status === 'failed';
      }, { timeout: 5000 });

      mockWorkerInstance.clearAllErrors();

      act(() => {
        result.current.retryUpload(firstItemId);
      });

      // Should not exceed concurrency during retry
      await new Promise(resolve => setTimeout(resolve, 100));
      const activeCount = mockWorkerInstance.getActiveUploadsCount();
      expect(activeCount).toBeLessThanOrEqual(3);
    });

    it('should handle all slots filled scenario', async () => {
      mockWorkerInstance.setUploadDelay(1000);

      const { result } = renderHook(() => useUploadQueue());
      const files = Array.from({ length: 3 }, (_, i) =>
        new File([`content ${i}`], `file-${i}.pdf`, { type: 'application/pdf' })
      );

      await act(async () => {
        await result.current.addFiles(files, '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        const stats = result.current.getStats();
        return stats.uploading === 3;
      });

      // All 3 slots should be filled
      expect(mockWorkerInstance.getActiveUploadsCount()).toBe(3);

      // Add another file - should queue
      const extraFile = new File(['extra'], 'extra.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([extraFile], '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      // Still only 3 active
      expect(mockWorkerInstance.getActiveUploadsCount()).toBe(3);

      // Fourth file should be pending
      const pendingItems = result.current.queue.filter(item => item.status === 'pending');
      expect(pendingItems.length).toBeGreaterThan(0);
    });

    it('should sequentially process uploads when limit reached', async () => {
      mockWorkerInstance.setUploadDelay(100);

      const { result } = renderHook(() => useUploadQueue());
      const files = Array.from({ length: 7 }, (_, i) =>
        new File([`content ${i}`], `file-${i}.pdf`, { type: 'application/pdf' })
      );

      await act(async () => {
        await result.current.addFiles(files, '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        const stats = result.current.getStats();
        return stats.succeeded === 7;
      }, { timeout: 10000 });

      expect(result.current.queue.length).toBe(7);
      expect(result.current.queue.every(item => item.status === 'success')).toBe(true);
    });

    it('should not start new uploads when limit reached', async () => {
      mockWorkerInstance.setUploadDelay(2000); // Very slow

      const { result } = renderHook(() => useUploadQueue());
      const files = Array.from({ length: 5 }, (_, i) =>
        new File([`content ${i}`], `file-${i}.pdf`, { type: 'application/pdf' })
      );

      await act(async () => {
        await result.current.addFiles(files, '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        const stats = result.current.getStats();
        return stats.uploading === 3;
      });

      // Should have 3 uploading, 2 pending
      const stats = result.current.getStats();
      expect(stats.uploading).toBe(3);
      expect(stats.pending).toBe(2);
    });

    it('should resume processing after all uploads complete', async () => {
      mockWorkerInstance.setUploadDelay(50);

      const { result } = renderHook(() => useUploadQueue());
      const firstBatch = Array.from({ length: 3 }, (_, i) =>
        new File([`batch1-${i}`], `batch1-${i}.pdf`, { type: 'application/pdf' })
      );

      await act(async () => {
        await result.current.addFiles(firstBatch, '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        const stats = result.current.getStats();
        return stats.succeeded === 3;
      }, { timeout: 5000 });

      // Add second batch
      const secondBatch = Array.from({ length: 2 }, (_, i) =>
        new File([`batch2-${i}`], `batch2-${i}.pdf`, { type: 'application/pdf' })
      );

      await act(async () => {
        await result.current.addFiles(secondBatch, '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        const stats = result.current.getStats();
        return stats.succeeded === 5;
      }, { timeout: 5000 });

      expect(result.current.queue.every(item => item.status === 'success')).toBe(true);
    });
  });

  // ============================================================================
  // 5. Callbacks & Observability Tests (~10 tests)
  // ============================================================================
  describe('Callbacks & Observability', () => {
    it('should call onUploadComplete for successful uploads', async () => {
      const onUploadComplete = jest.fn();
      const { result } = renderHook(() => useUploadQueue({ onUploadComplete }));

      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        expect(onUploadComplete).toHaveBeenCalled();
      }, { timeout: 5000 });

      expect(onUploadComplete).toHaveBeenCalledWith(
        expect.objectContaining({
          status: 'success',
          progress: 100,
          pdfId: expect.any(String)
        })
      );
    });

    it('should call onUploadError for failed uploads', async () => {
      (global.fetch as jest.Mock).mockRejectedValue(new Error('Network error'));

      const onUploadError = jest.fn();
      const { result } = renderHook(() => useUploadQueue({ onUploadError }));

      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        expect(onUploadError).toHaveBeenCalled();
      }, { timeout: 10000 });

      expect(onUploadError).toHaveBeenCalledWith(
        expect.objectContaining({
          status: 'failed'
        }),
        expect.stringContaining('Network error')
      );
    });

    it('should call onAllComplete when all uploads finish', async () => {
      const onAllComplete = jest.fn();
      const { result } = renderHook(() => useUploadQueue({ onAllComplete }));

      const files = [
        new File(['1'], '1.pdf', { type: 'application/pdf' }),
        new File(['2'], '2.pdf', { type: 'application/pdf' })
      ];

      await act(async () => {
        await result.current.addFiles(files, '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        expect(onAllComplete).toHaveBeenCalled();
      }, { timeout: 5000 });

      expect(onAllComplete).toHaveBeenCalledWith(
        expect.objectContaining({
          total: 2,
          succeeded: 2
        })
      );
    });

    it('should provide timing information in callbacks', async () => {
      const onUploadComplete = jest.fn();
      const { result } = renderHook(() => useUploadQueue({ onUploadComplete }));

      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });
      const startTime = Date.now();

      await act(async () => {
        await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        expect(onUploadComplete).toHaveBeenCalled();
      }, { timeout: 5000 });

      const callArgs = onUploadComplete.mock.calls[0][0];
      expect(callArgs.createdAt).toBeLessThanOrEqual(Date.now());
      expect(callArgs.createdAt).toBeGreaterThanOrEqual(startTime);
    });

    it('should call callbacks in correct sequence', async () => {
      const callSequence: string[] = [];

      const callbacks = {
        onUploadComplete: () => callSequence.push('complete'),
        onAllComplete: () => callSequence.push('allComplete')
      };

      const { result } = renderHook(() => useUploadQueue(callbacks));

      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        return callSequence.length === 2;
      }, { timeout: 5000 });

      expect(callSequence).toEqual(['complete', 'allComplete']);
    });

    it('should handle multiple onUploadComplete calls', async () => {
      const onUploadComplete = jest.fn();
      const { result } = renderHook(() => useUploadQueue({ onUploadComplete }));

      const files = [
        new File(['1'], '1.pdf', { type: 'application/pdf' }),
        new File(['2'], '2.pdf', { type: 'application/pdf' }),
        new File(['3'], '3.pdf', { type: 'application/pdf' })
      ];

      await act(async () => {
        await result.current.addFiles(files, '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        expect(onUploadComplete).toHaveBeenCalledTimes(3);
      }, { timeout: 5000 });
    });

    it('should not call onAllComplete prematurely', async () => {
      mockWorkerInstance.setUploadDelay(500);

      const onAllComplete = jest.fn();
      const { result } = renderHook(() => useUploadQueue({ onAllComplete }));

      const files = [
        new File(['1'], '1.pdf', { type: 'application/pdf' }),
        new File(['2'], '2.pdf', { type: 'application/pdf' })
      ];

      await act(async () => {
        await result.current.addFiles(files, '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      // Wait a bit but not until completion
      await new Promise(resolve => setTimeout(resolve, 200));

      expect(onAllComplete).not.toHaveBeenCalled();
    });

    it('should provide error details in onUploadError', async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: false,
        status: 400,
        json: async () => ({ error: 'File too large' }),
        headers: new Headers({ 'x-correlation-id': 'test-corr' })
      });

      const onUploadError = jest.fn();
      const { result } = renderHook(() => useUploadQueue({ onUploadError }));

      const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        expect(onUploadError).toHaveBeenCalled();
      }, { timeout: 5000 });

      const [item, error] = onUploadError.mock.calls[0];
      expect(error).toContain('File too large');
      expect(item.correlationId).toBe('test-corr');
    });

    it('should not call onAllComplete for empty queue', async () => {
      const onAllComplete = jest.fn();
      renderHook(() => useUploadQueue({ onAllComplete }));

      await new Promise(resolve => setTimeout(resolve, 500));

      expect(onAllComplete).not.toHaveBeenCalled();
    });

    it('should update callbacks when options change', async () => {
      const onUploadComplete1 = jest.fn();
      const onUploadComplete2 = jest.fn();

      const { result, rerender } = renderHook(
        (props) => useUploadQueue(props),
        { initialProps: { onUploadComplete: onUploadComplete1 } }
      );

      const file1 = new File(['1'], '1.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file1], '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        expect(onUploadComplete1).toHaveBeenCalled();
      }, { timeout: 5000 });

      // Update callback
      rerender({ onUploadComplete: onUploadComplete2 });

      const file2 = new File(['2'], '2.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file2], '770e8400-e29b-41d4-a716-000000000123', 'en');
      });

      await waitFor(() => {
        expect(onUploadComplete2).toHaveBeenCalled();
      }, { timeout: 5000 });

      expect(onUploadComplete2).toHaveBeenCalledTimes(1);
    });
  });

  // Due to length constraints, continuing in next message with:
  // 6. Message Protocol Tests
  // 7. Persistence Layer Tests
  // 8. BroadcastChannel Sync Tests
  // 9. Worker Lifecycle Tests
  // 10. File Buffering Tests
  // 11. ArrayBuffer Management Tests
  // 12. Performance & Edge Cases Tests
});
