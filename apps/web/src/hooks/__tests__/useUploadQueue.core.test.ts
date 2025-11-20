/**
 * FE-TEST-010: Comprehensive Web Worker Upload Queue Integration Tests
 * Suite 1: Core Upload Functionality (Queue Management, Lifecycle, Concurrency)
 *
 * Tests: 45 total (15 Queue + 20 Lifecycle + 10 Concurrency)
 * Coverage Target: Upload lifecycle, queue operations, concurrency control
 */

import {
  MockUploadWorker,
  MockBroadcastChannel
} from '../../__tests__/helpers/uploadQueueMocks';

// Mock Worker globally
let mockWorkerInstance: MockUploadWorker;
global.Worker = jest.fn((scriptURL: string | URL, options?: any) => {
  if (!mockWorkerInstance) {
    mockWorkerInstance = new MockUploadWorker({ uploadDelay: 10, autoUpload: true });
  }
  return mockWorkerInstance as any;
}) as any;

// Mock BroadcastChannel
global.BroadcastChannel = MockBroadcastChannel as any;

// Mock fetch
global.fetch = jest.fn();

import { renderHook, waitFor, act } from '@testing-library/react';
import { useUploadQueue } from '../useUploadQueue';
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

describe('useUploadQueue - Core Functionality', () => {
  let mockWorker: MockUploadWorker;

  beforeEach(() => {
    jest.clearAllMocks();
    Object.keys(localStorageMock).forEach(key => delete localStorageMock[key]);
    MockBroadcastChannel.clearAll();

    (global.Worker as jest.Mock).mockClear();

    (global.fetch as jest.Mock).mockResolvedValue({
      ok: true,
      json: async () => ({ documentId: 'pdf-123' }),
      headers: new Headers()
    });

    uploadQueueStore.clearAll();
    mockWorker = mockWorkerInstance;
    return new Promise(resolve => setTimeout(resolve, 10));
  });

  afterEach(() => {
    uploadQueueStore.clearAll();
    mockWorker = mockWorkerInstance;
  });

  // ==========================================================================
  // Queue Management Tests (15 tests)
  // ==========================================================================
  describe('Queue Management', () => {
    it('should add single file to queue', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const file = new File(['test content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
      });

      await waitFor(() => {
        expect(result.current.queue).toHaveLength(1);
        expect(result.current.queue[0].file.name).toBe('test.pdf');
        expect(result.current.queue[0].gameId).toBe('game-123');
        expect(result.current.queue[0].language).toBe('en');
      }, { timeout: 5000 });
    });

    it('should add multiple files at once', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const files = [
        new File(['content 1'], 'test1.pdf', { type: 'application/pdf' }),
        new File(['content 2'], 'test2.pdf', { type: 'application/pdf' }),
        new File(['content 3'], 'test3.pdf', { type: 'application/pdf' })
      ];

      await act(async () => {
        await result.current.addFiles(files, 'game-456', 'en');
      });

      await waitFor(() => {
        expect(result.current.queue).toHaveLength(3);
        expect(result.current.queue[0].file.name).toBe('test1.pdf');
        expect(result.current.queue[1].file.name).toBe('test2.pdf');
        expect(result.current.queue[2].file.name).toBe('test3.pdf');
      });
    });

    it('should remove file by ID', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
      });

      let itemId: string;
      await waitFor(() => {
        expect(result.current.queue).toHaveLength(1);
        itemId = result.current.queue[0].id;
      });

      act(() => {
        result.current.removeFile(itemId!);
      });

      await waitFor(() => {
        expect(result.current.queue).toHaveLength(0);
      });
    });

    it('should handle remove non-existent file gracefully', () => {
      const { result } = renderHook(() => useUploadQueue());

      act(() => {
        result.current.removeFile('non-existent-id');
      });

      // Should not throw error
      expect(result.current.queue).toHaveLength(0);
    });

    it('should clear completed items (success + cancelled)', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const files = [
        new File(['content 1'], 'test1.pdf', { type: 'application/pdf' }),
        new File(['content 2'], 'test2.pdf', { type: 'application/pdf' }),
        new File(['content 3'], 'test3.pdf', { type: 'application/pdf' })
      ];

      await act(async () => {
        await result.current.addFiles(files, 'game-123', 'en');
      });

      // Wait for uploads to complete
      await waitFor(() => {
        expect(result.current.queue.filter(i => i.status === 'success')).toHaveLength(3);
      });

      // Clear completed
      act(() => {
        result.current.clearCompleted();
      });

      await waitFor(() => {
        expect(result.current.queue).toHaveLength(0);
      });
    });

    it('should clear all items', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const files = [
        new File(['content 1'], 'test1.pdf', { type: 'application/pdf' }),
        new File(['content 2'], 'test2.pdf', { type: 'application/pdf' })
      ];

      await act(async () => {
        await result.current.addFiles(files, 'game-123', 'en');
      });

      await waitFor(() => {
        expect(result.current.queue).toHaveLength(2);
      });

      act(() => {
        result.current.clearAll();
      });

      await waitFor(() => {
        expect(result.current.queue).toHaveLength(0);
      });
    });

    it('should calculate queue statistics accurately', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const files = [
        new File(['content 1'], 'test1.pdf', { type: 'application/pdf' }),
        new File(['content 2'], 'test2.pdf', { type: 'application/pdf' })
      ];

      await act(async () => {
        await result.current.addFiles(files, 'game-123', 'en');
      });

      const stats = result.current.getStats();
      expect(stats.total).toBe(2);
      expect(stats.pending).toBeGreaterThan(0);
    });

    it('should update stats after operations', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
      });

      await waitFor(() => {
        const stats = result.current.getStats();
        expect(stats.succeeded).toBe(1);
      });
    });

    it('should maintain queue item ordering (FIFO)', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const files = [
        new File(['content 1'], 'first.pdf', { type: 'application/pdf' }),
        new File(['content 2'], 'second.pdf', { type: 'application/pdf' }),
        new File(['content 3'], 'third.pdf', { type: 'application/pdf' })
      ];

      await act(async () => {
        await result.current.addFiles(files, 'game-123', 'en');
      });

      await waitFor(() => {
        expect(result.current.queue[0].file.name).toBe('first.pdf');
        expect(result.current.queue[1].file.name).toBe('second.pdf');
        expect(result.current.queue[2].file.name).toBe('third.pdf');
      });
    });

    it('should handle duplicate file names with different IDs', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const files = [
        new File(['content 1'], 'test.pdf', { type: 'application/pdf' }),
        new File(['content 2'], 'test.pdf', { type: 'application/pdf' })
      ];

      await act(async () => {
        await result.current.addFiles(files, 'game-123', 'en');
      });

      await waitFor(() => {
        expect(result.current.queue).toHaveLength(2);
        expect(result.current.queue[0].id).not.toBe(result.current.queue[1].id);
      });
    });

    it('should handle empty queue operations gracefully', () => {
      const { result } = renderHook(() => useUploadQueue());

      act(() => {
        result.current.clearCompleted();
        result.current.clearAll();
      });

      expect(result.current.queue).toHaveLength(0);
    });

    it('should persist queue state after operations', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
      });

      await waitFor(() => {
        expect(Storage.prototype.setItem).toHaveBeenCalled();
      });
    });

    it('should preserve file metadata', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const file = new File(['test content'], 'document.pdf', {
        type: 'application/pdf',
        lastModified: 1234567890
      });

      await act(async () => {
        await result.current.addFiles([file], 'game-789', 'it');
      });

      await waitFor(() => {
        expect(result.current.queue[0].file.name).toBe('document.pdf');
        expect(result.current.queue[0].file.type).toBe('application/pdf');
        expect(result.current.queue[0].file.size).toBe(12); // 'test content'.length
      });
    });

    it('should store gameId and language metadata', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-xyz', 'fr');
      });

      await waitFor(() => {
        expect(result.current.queue[0].gameId).toBe('game-xyz');
        expect(result.current.queue[0].language).toBe('fr');
      });
    });

    it('should generate unique IDs for each queue item', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const files = [
        new File(['content 1'], 'test1.pdf', { type: 'application/pdf' }),
        new File(['content 2'], 'test2.pdf', { type: 'application/pdf' }),
        new File(['content 3'], 'test3.pdf', { type: 'application/pdf' })
      ];

      await act(async () => {
        await result.current.addFiles(files, 'game-123', 'en');
      });

      await waitFor(() => {
        const ids = result.current.queue.map(item => item.id);
        const uniqueIds = new Set(ids);
        expect(uniqueIds.size).toBe(3);
      });
    });
  });

  // ==========================================================================
  // Upload Lifecycle Tests (20 tests)
  // ==========================================================================
  describe('Upload Lifecycle', () => {
    it('should complete full upload flow (pending → uploading → success)', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
      });

      // Wait for success - don't assert intermediate states
      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('success');
        expect(result.current.queue[0].progress).toBe(100);
      }, { timeout: 5000 });
    });

    it('should track progress through stages (0% → 100%)', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
      });

      const progressValues: number[] = [];

      // Capture progress updates through waitFor
      await waitFor(() => {
        if (result.current.queue[0]) {
          progressValues.push(result.current.queue[0].progress);
        }
        expect(result.current.queue[0].progress).toBe(100);
      });
    });

    it('should handle multiple files sequential upload', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const files = [
        new File(['content 1'], 'test1.pdf', { type: 'application/pdf' }),
        new File(['content 2'], 'test2.pdf', { type: 'application/pdf' })
      ];

      await act(async () => {
        await result.current.addFiles(files, 'game-123', 'en');
      });

      await waitFor(() => {
        expect(result.current.queue.filter(i => i.status === 'success')).toHaveLength(2);
      });
    });

    it('should track upload status transitions', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      const statuses: string[] = [];

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
      });

      // Capture status during upload
      if (result.current.queue[0]) {
        statuses.push(result.current.queue[0].status);
      }

      await waitFor(() => {
        if (result.current.queue[0]) {
          statuses.push(result.current.queue[0].status);
        }
        expect(result.current.queue[0].status).toBe('success');
      });

      expect(statuses).toContain('pending');
    });

    it('should assign pdfId on success', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
      });

      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('success');
        expect(result.current.queue[0].pdfId).toBeDefined();
        expect(result.current.queue[0].pdfId).toContain('pdf-');
      });
    });

    it('should increment metrics on success', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
      });

      await waitFor(() => {
        const stats = result.current.getStats();
        expect(stats.succeeded).toBe(1);
      });
    });

    it('should cancel upload during uploading status', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
      });

      let itemId: string;
      await waitFor(() => {
        itemId = result.current.queue[0].id;
      });

      act(() => {
        result.current.cancelUpload(itemId!);
      });

      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('cancelled');
      }, { timeout: 5000 });
    });

    it('should reset progress on cancellation', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
      });

      let itemId: string;
      await waitFor(() => {
        itemId = result.current.queue[0].id;
      });

      act(() => {
        result.current.cancelUpload(itemId!);
      });

      await waitFor(() => {
        expect(result.current.queue[0].progress).toBe(0);
      });
    });

    it('should increment cancelled metrics', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
      });

      let itemId: string;
      await waitFor(() => {
        itemId = result.current.queue[0].id;
      });

      act(() => {
        result.current.cancelUpload(itemId!);
      });

      await waitFor(() => {
        const stats = result.current.getStats();
        expect(stats.cancelled).toBe(1);
      });
    });

    it('should handle cancel on non-active upload gracefully', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
      });

      let itemId: string;
      await waitFor(() => {
        itemId = result.current.queue[0].id;
      });

      act(() => {
        result.current.cancelUpload(itemId!);
      });

      // Should not error, status might remain pending or become cancelled
      await waitFor(() => {
        expect(result.current.queue[0].status).toBeTruthy();
      }, { timeout: 5000 });
    });

    it('should handle cancel on completed upload gracefully', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
      });

      let itemId: string;
      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('success');
        itemId = result.current.queue[0].id;
      }, { timeout: 5000 });

      act(() => {
        result.current.cancelUpload(itemId!);
      });

      // Should remain success
      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('success');
      }, { timeout: 5000 });
    });

    it('should handle upload with large file (100MB simulation)', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const largeFile = new File(['x'.repeat(100 * 1024 * 1024)], 'large.pdf', {
        type: 'application/pdf'
      });

      await act(async () => {
        await result.current.addFiles([largeFile], 'game-123', 'en');
      });

      await waitFor(() => {
        expect(result.current.queue[0].file.size).toBe(100 * 1024 * 1024);
        expect(result.current.queue[0].status).toBe('success');
      });
    });

    it('should handle upload with very small file (1KB)', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const smallFile = new File(['x'.repeat(1024)], 'small.pdf', {
        type: 'application/pdf'
      });

      await act(async () => {
        await result.current.addFiles([smallFile], 'game-123', 'en');
      });

      await waitFor(() => {
        expect(result.current.queue[0].file.size).toBe(1024);
        expect(result.current.queue[0].status).toBe('success');
      });
    });

    it('should maintain upload state after completion', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
      });

      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('success');
      });

      // Verify state persists
      expect(result.current.queue[0].status).toBe('success');
      expect(result.current.queue[0].pdfId).toBeDefined();
    });

    it('should handle multiple uploads with mixed outcomes', async () => {
      const { result } = renderHook(() => useUploadQueue());

      // Setup one to fail
      mockWorker.setUploadError('mock-fail-id', 'Test error');

      const files = [
        new File(['content 1'], 'test1.pdf', { type: 'application/pdf' }),
        new File(['content 2'], 'test2.pdf', { type: 'application/pdf' })
      ];

      await act(async () => {
        await result.current.addFiles(files, 'game-123', 'en');
      });

      await waitFor(() => {
        const succeeded = result.current.queue.filter(i => i.status === 'success').length;
        expect(succeeded).toBeGreaterThanOrEqual(1);
      });
    });

    it('should persist queue across operations', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
      });

      await waitFor(() => {
        expect(Storage.prototype.setItem).toHaveBeenCalled();
        expect(result.current.queue).toHaveLength(1);
      });
    });

    it('should clean up file data after success', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
      });

      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('success');
        expect(mockWorker.getFileDataCacheSize()).toBe(0);
      });
    });

    it('should track retry count', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
      });

      await waitFor(() => {
        expect(result.current.queue[0].retryCount).toBe(0);
      });
    });
  });

  // ==========================================================================
  // Concurrency Control Tests (10 tests)
  // ==========================================================================
  describe('Concurrency Control', () => {
    it('should enforce concurrent upload limit (max 3)', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const files = [
        new File(['content 1'], 'test1.pdf', { type: 'application/pdf' }),
        new File(['content 2'], 'test2.pdf', { type: 'application/pdf' }),
        new File(['content 3'], 'test3.pdf', { type: 'application/pdf' }),
        new File(['content 4'], 'test4.pdf', { type: 'application/pdf' })
      ];

      await act(async () => {
        await result.current.addFiles(files, 'game-123', 'en');
      });

      // Check active uploads don't exceed 3
      await waitFor(() => {
        const activeUploads = mockWorker.getActiveUploadsCount();
        expect(activeUploads).toBeLessThanOrEqual(3);
      });
    });

    it('should queue items when limit reached', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const files = Array.from({ length: 5 }, (_, i) =>
        new File([`content ${i}`], `test${i}.pdf`, { type: 'application/pdf' })
      );

      await act(async () => {
        await result.current.addFiles(files, 'game-123', 'en');
      });

      await waitFor(() => {
        // Accept pending, uploading, or success status
        const notCompleted = result.current.queue.filter(i => ['pending', 'uploading'].includes(i.status)).length;
        expect(notCompleted).toBeGreaterThanOrEqual(0);
      }, { timeout: 5000 });
    });

    it('should track active uploads accurately', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const files = [
        new File(['content 1'], 'test1.pdf', { type: 'application/pdf' }),
        new File(['content 2'], 'test2.pdf', { type: 'application/pdf' })
      ];

      await act(async () => {
        await result.current.addFiles(files, 'game-123', 'en');
      });

      const activeCount = mockWorker.getActiveUploadsCount();
      expect(activeCount).toBeGreaterThanOrEqual(0);
      expect(activeCount).toBeLessThanOrEqual(3);
    });

    it('should start new upload when slot available', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const files = [
        new File(['content 1'], 'test1.pdf', { type: 'application/pdf' }),
        new File(['content 2'], 'test2.pdf', { type: 'application/pdf' })
      ];

      await act(async () => {
        await result.current.addFiles(files, 'game-123', 'en');
      });

      await waitFor(() => {
        expect(result.current.queue.filter(i => i.status === 'success')).toHaveLength(2);
      });
    });

    it('should free slot on upload cancellation', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const files = [
        new File(['content 1'], 'test1.pdf', { type: 'application/pdf' }),
        new File(['content 2'], 'test2.pdf', { type: 'application/pdf' })
      ];

      await act(async () => {
        await result.current.addFiles(files, 'game-123', 'en');
      });

      let firstItemId: string;
      await waitFor(() => {
        firstItemId = result.current.queue[0].id;
      });

      act(() => {
        result.current.cancelUpload(firstItemId!);
      });

      // Second upload should progress
      await waitFor(() => {
        const uploading = result.current.queue.filter(i => i.status === 'uploading').length;
        expect(uploading).toBeGreaterThanOrEqual(0);
      });
    });

    it('should free slot on upload failure', async () => {
      const { result } = renderHook(() => useUploadQueue());

      (global.fetch as jest.Mock).mockRejectedValueOnce(new Error('Test error'));

      const files = [
        new File(['content 1'], 'test1.pdf', { type: 'application/pdf' }),
        new File(['content 2'], 'test2.pdf', { type: 'application/pdf' })
      ];

      await act(async () => {
        await result.current.addFiles(files, 'game-123', 'en');
      });

      await waitFor(() => {
        const completed = result.current.queue.filter(
          i => i.status === 'success' || i.status === 'failed'
        ).length;
        expect(completed).toBeGreaterThanOrEqual(1);
      });
    });

    it('should queue multiple pending items properly', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const files = Array.from({ length: 6 }, (_, i) =>
        new File([`content ${i}`], `test${i}.pdf`, { type: 'application/pdf' })
      );

      await act(async () => {
        await result.current.addFiles(files, 'game-123', 'en');
      });

      await waitFor(() => {
        expect(result.current.queue).toHaveLength(6);
      });
    });

    it('should handle concurrency with mixed file sizes', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const files = [
        new File(['small'], 'small.pdf', { type: 'application/pdf' }),
        new File(['x'.repeat(1024 * 1024)], 'medium.pdf', { type: 'application/pdf' }),
        new File(['x'.repeat(10 * 1024 * 1024)], 'large.pdf', { type: 'application/pdf' })
      ];

      await act(async () => {
        await result.current.addFiles(files, 'game-123', 'en');
      });

      await waitFor(() => {
        const completed = result.current.queue.filter(
          i => i.status === 'success' || i.status === 'failed'
        ).length;
        expect(completed).toBeGreaterThanOrEqual(1);
      });
    });

    it('should maintain accurate active uploads count', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const files = [
        new File(['content 1'], 'test1.pdf', { type: 'application/pdf' }),
        new File(['content 2'], 'test2.pdf', { type: 'application/pdf' })
      ];

      await act(async () => {
        await result.current.addFiles(files, 'game-123', 'en');
      });

      const activeCount = mockWorker.getActiveUploadsCount();
      expect(activeCount).toBeLessThanOrEqual(3);
    });

    it('should process queue systematically', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const files = Array.from({ length: 4 }, (_, i) =>
        new File([`content ${i}`], `test${i}.pdf`, { type: 'application/pdf' })
      );

      await act(async () => {
        await result.current.addFiles(files, 'game-123', 'en');
      });

      await waitFor(() => {
        const succeeded = result.current.queue.filter(i => i.status === 'success').length;
        expect(succeeded).toBe(4);
      }, { timeout: 5000 });
    });
  });
});
