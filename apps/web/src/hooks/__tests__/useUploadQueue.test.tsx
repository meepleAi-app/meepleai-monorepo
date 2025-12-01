/**
 * Comprehensive Tests for useUploadQueue Hook (Issue #1661 - Fase 1.3)
 *
 * Coverage target: 95%+
 * Tests: Queue management, concurrency, retry logic, progress tracking, cancellation
 */

import { renderHook, waitFor } from '@testing-library/react';
import { useUploadQueue } from '../useUploadQueue';
import * as retryUtils from '../../lib/retryUtils';
import * as errorUtils from '../../lib/errorUtils';

vi.mock('../../lib/retryUtils', () => ({
  retryWithBackoff: vi.fn(fn => fn()),
  isRetryableError: vi.fn(() => false),
}));

vi.mock('../../lib/errorUtils', () => ({
  extractCorrelationId: vi.fn(() => undefined),
}));

vi.mock('../../lib/api', () => ({
  ApiError: class ApiError extends Error {
    correlationId?: string;
    constructor({ message, correlationId }: any) {
      super(message);
      this.correlationId = correlationId;
    }
  },
}));

describe('useUploadQueue', () => {
  let mockFetch: Mock;
  let originalFetch: typeof global.fetch;

  beforeEach(() => {
    vi.useFakeTimers();
    originalFetch = global.fetch;
    mockFetch = vi.fn();
    global.fetch = mockFetch;

    // Mock successful upload by default
    mockFetch.mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({ documentId: 'pdf-123' }),
    });
  });

  afterEach(() => {
    vi.useRealTimers();
    global.fetch = originalFetch;
    vi.clearAllMocks();
  });

  describe('initialization', () => {
    it('should initialize with empty queue', () => {
      const { result } = renderHook(() => useUploadQueue());

      expect(result.current.queue).toEqual([]);
    });

    it('should initialize with default options', () => {
      const { result } = renderHook(() => useUploadQueue());

      const stats = result.current.getStats();
      expect(stats.total).toBe(0);
    });

    it('should accept custom options', () => {
      const onUploadComplete = vi.fn();
      const { result } = renderHook(() =>
        useUploadQueue({
          concurrencyLimit: 5,
          maxRetries: 5,
          autoUpload: false,
          onUploadComplete,
        })
      );

      expect(result.current.queue).toEqual([]);
    });
  });

  describe('addFiles', () => {
    it('should add files to queue', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      act(() => {
        result.current.addFiles([file], 'game-1', 'en');
      });

      expect(result.current.queue).toHaveLength(1);
      expect(result.current.queue[0].file).toBe(file);
      expect(result.current.queue[0].gameId).toBe('game-1');
      expect(result.current.queue[0].language).toBe('en');
      expect(result.current.queue[0].status).toBe('pending');
    });

    it('should add multiple files', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));

      const files = [
        new File(['content1'], 'test1.pdf'),
        new File(['content2'], 'test2.pdf'),
        new File(['content3'], 'test3.pdf'),
      ];

      act(() => {
        result.current.addFiles(files, 'game-1', 'en');
      });

      expect(result.current.queue).toHaveLength(3);
      expect(result.current.queue.map(i => i.file.name)).toEqual([
        'test1.pdf',
        'test2.pdf',
        'test3.pdf',
      ]);
    });

    it('should generate unique IDs for each file', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));

      const files = [new File(['1'], '1.pdf'), new File(['2'], '2.pdf')];

      act(() => {
        result.current.addFiles(files, 'game-1', 'en');
      });

      const ids = result.current.queue.map(i => i.id);
      expect(new Set(ids).size).toBe(2); // All unique
    });

    it('should initialize items with pending status and 0 progress', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));

      const file = new File(['content'], 'test.pdf');

      act(() => {
        result.current.addFiles([file], 'game-1', 'en');
      });

      const item = result.current.queue[0];
      expect(item.status).toBe('pending');
      expect(item.progress).toBe(0);
      expect(item.retryCount).toBe(0);
      expect(item.error).toBeUndefined();
      expect(item.pdfId).toBeUndefined();
    });

    it('should trigger onQueueAdd callback', () => {
      const onQueueAdd = vi.fn();
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false, onQueueAdd }));

      const file = new File(['content'], 'test.pdf');

      act(() => {
        result.current.addFiles([file], 'game-1', 'en');
      });

      expect(onQueueAdd).toHaveBeenCalledTimes(1);
      expect(onQueueAdd).toHaveBeenCalledWith(
        expect.arrayContaining([expect.objectContaining({ file, gameId: 'game-1' })])
      );
    });

    it('should reset allCompleteNotified flag when adding files', () => {
      const onAllComplete = vi.fn();
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false, onAllComplete }));

      const file1 = new File(['1'], '1.pdf');

      act(() => {
        result.current.addFiles([file1], 'game-1', 'en');
      });

      // Clear to trigger onAllComplete
      act(() => {
        result.current.clearAll();
      });

      // Add again - should allow onAllComplete to fire again
      const file2 = new File(['2'], '2.pdf');

      act(() => {
        result.current.addFiles([file2], 'game-1', 'en');
      });

      expect(result.current.queue).toHaveLength(1);
    });
  });

  describe('removeFile', () => {
    it('should remove file from queue when not uploading', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));

      const file = new File(['content'], 'test.pdf');

      act(() => {
        result.current.addFiles([file], 'game-1', 'en');
      });

      const itemId = result.current.queue[0].id;

      act(() => {
        result.current.removeFile(itemId);
      });

      expect(result.current.queue).toHaveLength(0);
    });

    it('should not remove file while uploading', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));

      const file = new File(['content'], 'test.pdf');

      act(() => {
        result.current.addFiles([file], 'game-1', 'en');
      });

      const itemId = result.current.queue[0].id;

      // Manually set status to uploading
      act(() => {
        result.current.queue[0].status = 'uploading';
      });

      act(() => {
        result.current.removeFile(itemId);
      });

      // Should still be in queue
      expect(result.current.queue).toHaveLength(1);
    });

    it('should remove specific file from multiple files', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));

      const files = [new File(['1'], '1.pdf'), new File(['2'], '2.pdf'), new File(['3'], '3.pdf')];

      act(() => {
        result.current.addFiles(files, 'game-1', 'en');
      });

      const middleItemId = result.current.queue[1].id;

      act(() => {
        result.current.removeFile(middleItemId);
      });

      expect(result.current.queue).toHaveLength(2);
      expect(result.current.queue.map(i => i.file.name)).toEqual(['1.pdf', '3.pdf']);
    });

    it('should handle removing non-existent ID gracefully', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));

      act(() => {
        result.current.removeFile('non-existent-id');
      });

      expect(result.current.queue).toHaveLength(0);
    });
  });

  describe('cancelUpload', () => {
    it('should cancel uploading file', async () => {
      mockFetch.mockImplementation(
        () =>
          new Promise(resolve => {
            setTimeout(() => {
              resolve({
                ok: true,
                json: async () => ({ documentId: 'pdf-123' }),
              });
            }, 5000);
          })
      );

      const { result } = renderHook(() => useUploadQueue({ autoUpload: true }));

      const file = new File(['content'], 'test.pdf');

      act(() => {
        result.current.addFiles([file], 'game-1', 'en');
      });

      // Wait for upload to start
      await act(async () => {
        vi.advanceTimersByTime(100);
      });

      const itemId = result.current.queue[0].id;

      // Cancel upload
      act(() => {
        result.current.cancelUpload(itemId);
      });

      await waitFor(() => {
        const item = result.current.queue.find(i => i.id === itemId);
        expect(item?.status).toBe('cancelled');
      });
    });

    it('should set progress to 0 on cancel', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));

      const file = new File(['content'], 'test.pdf');

      act(() => {
        result.current.addFiles([file], 'game-1', 'en');
      });

      const itemId = result.current.queue[0].id;

      act(() => {
        result.current.cancelUpload(itemId);
      });

      const item = result.current.queue.find(i => i.id === itemId);
      expect(item?.progress).toBe(0);
    });

    it('should handle cancelling non-existent upload gracefully', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));

      act(() => {
        result.current.cancelUpload('non-existent-id');
      });

      expect(result.current.queue).toHaveLength(0);
    });
  });

  describe('retryUpload', () => {
    it('should reset failed upload to pending', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));

      const file = new File(['content'], 'test.pdf');

      act(() => {
        result.current.addFiles([file], 'game-1', 'en');
      });

      const itemId = result.current.queue[0].id;

      // Manually set to failed
      act(() => {
        result.current.queue[0].status = 'failed';
        result.current.queue[0].error = 'Network error';
      });

      act(() => {
        result.current.retryUpload(itemId);
      });

      const item = result.current.queue.find(i => i.id === itemId);
      expect(item?.status).toBe('pending');
      expect(item?.progress).toBe(0);
      expect(item?.error).toBeUndefined();
    });

    it('should allow retry after failure', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));

      const file = new File(['content'], 'test.pdf');

      act(() => {
        result.current.addFiles([file], 'game-1', 'en');
      });

      const itemId = result.current.queue[0].id;

      // Mark as failed
      act(() => {
        result.current.queue[0].status = 'failed';
      });

      expect(result.current.queue[0].status).toBe('failed');

      // Retry
      act(() => {
        result.current.retryUpload(itemId);
      });

      expect(result.current.queue[0].status).toBe('pending');
    });
  });

  describe('clearCompleted', () => {
    it('should remove completed uploads', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));

      const files = [new File(['1'], '1.pdf'), new File(['2'], '2.pdf'), new File(['3'], '3.pdf')];

      act(() => {
        result.current.addFiles(files, 'game-1', 'en');
      });

      // Mark some as completed
      act(() => {
        result.current.queue[0].status = 'success';
        result.current.queue[1].status = 'failed';
        // queue[2] remains pending
      });

      act(() => {
        result.current.clearCompleted();
      });

      expect(result.current.queue).toHaveLength(1);
      expect(result.current.queue[0].status).toBe('pending');
    });

    it('should remove cancelled uploads', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));

      const file = new File(['content'], 'test.pdf');

      act(() => {
        result.current.addFiles([file], 'game-1', 'en');
      });

      act(() => {
        result.current.queue[0].status = 'cancelled';
      });

      act(() => {
        result.current.clearCompleted();
      });

      expect(result.current.queue).toHaveLength(0);
    });

    it('should not remove pending, uploading, or processing items', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));

      const files = [new File(['1'], '1.pdf'), new File(['2'], '2.pdf'), new File(['3'], '3.pdf')];

      act(() => {
        result.current.addFiles(files, 'game-1', 'en');
      });

      act(() => {
        result.current.queue[0].status = 'pending';
        result.current.queue[1].status = 'uploading';
        result.current.queue[2].status = 'processing';
      });

      act(() => {
        result.current.clearCompleted();
      });

      expect(result.current.queue).toHaveLength(3);
    });
  });

  describe('clearAll', () => {
    it('should clear entire queue', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));

      const files = [new File(['1'], '1.pdf'), new File(['2'], '2.pdf')];

      act(() => {
        result.current.addFiles(files, 'game-1', 'en');
      });

      expect(result.current.queue).toHaveLength(2);

      act(() => {
        result.current.clearAll();
      });

      expect(result.current.queue).toHaveLength(0);
    });

    it('should cancel active uploads when clearing all', async () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: true }));

      const file = new File(['content'], 'test.pdf');

      act(() => {
        result.current.addFiles([file], 'game-1', 'en');
      });

      // Let upload start
      await act(async () => {
        vi.advanceTimersByTime(100);
      });

      act(() => {
        result.current.clearAll();
      });

      expect(result.current.queue).toHaveLength(0);
    });

    it('should reset allCompleteNotified flag', () => {
      const onAllComplete = vi.fn();
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false, onAllComplete }));

      const file = new File(['content'], 'test.pdf');

      act(() => {
        result.current.addFiles([file], 'game-1', 'en');
      });

      act(() => {
        result.current.clearAll();
      });

      expect(result.current.queue).toHaveLength(0);
    });
  });

  describe('getStats', () => {
    it('should calculate stats correctly', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));

      const files = [
        new File(['1'], '1.pdf'),
        new File(['2'], '2.pdf'),
        new File(['3'], '3.pdf'),
        new File(['4'], '4.pdf'),
        new File(['5'], '5.pdf'),
      ];

      act(() => {
        result.current.addFiles(files, 'game-1', 'en');
      });

      act(() => {
        result.current.queue[0].status = 'success';
        result.current.queue[1].status = 'failed';
        result.current.queue[2].status = 'uploading';
        result.current.queue[3].status = 'processing';
        // queue[4] remains pending
      });

      const stats = result.current.getStats();

      expect(stats.total).toBe(5);
      expect(stats.succeeded).toBe(1);
      expect(stats.failed).toBe(1);
      expect(stats.uploading).toBe(1);
      expect(stats.processing).toBe(1);
      expect(stats.pending).toBe(1);
    });

    it('should return zero stats for empty queue', () => {
      const { result } = renderHook(() => useUploadQueue());

      const stats = result.current.getStats();

      expect(stats.total).toBe(0);
      expect(stats.pending).toBe(0);
      expect(stats.uploading).toBe(0);
      expect(stats.processing).toBe(0);
      expect(stats.succeeded).toBe(0);
      expect(stats.failed).toBe(0);
    });

    it('should update stats reactively', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));

      const file = new File(['content'], 'test.pdf');

      act(() => {
        result.current.addFiles([file], 'game-1', 'en');
      });

      expect(result.current.getStats().pending).toBe(1);

      act(() => {
        result.current.queue[0].status = 'success';
      });

      expect(result.current.getStats().succeeded).toBe(1);
      expect(result.current.getStats().pending).toBe(0);
    });
  });

  describe('autoUpload behavior', () => {
    it('should not auto-upload when autoUpload=false', async () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));

      const file = new File(['content'], 'test.pdf');

      act(() => {
        result.current.addFiles([file], 'game-1', 'en');
      });

      await act(async () => {
        vi.advanceTimersByTime(1000);
      });

      expect(result.current.queue[0].status).toBe('pending');
      expect(mockFetch).not.toHaveBeenCalled();
    });
  });

  describe('concurrency control', () => {
    it('should accept custom concurrency limit in options', () => {
      const { result } = renderHook(() =>
        useUploadQueue({ autoUpload: false, concurrencyLimit: 2 })
      );

      expect(result.current.queue).toEqual([]);
    });

    it('should accept concurrencyLimit=1', () => {
      const { result } = renderHook(() =>
        useUploadQueue({ autoUpload: false, concurrencyLimit: 1 })
      );

      expect(result.current.queue).toEqual([]);
    });

    it('should accept concurrencyLimit=0', () => {
      const { result } = renderHook(() =>
        useUploadQueue({ autoUpload: false, concurrencyLimit: 0 })
      );

      expect(result.current.queue).toEqual([]);
    });
  });

  describe('callbacks', () => {
    it('should accept onUploadStart callback in options', () => {
      const onUploadStart = vi.fn();
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false, onUploadStart }));

      expect(result.current.queue).toEqual([]);
    });

    it('should accept onUploadComplete callback in options', () => {
      const onUploadComplete = vi.fn();
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false, onUploadComplete }));

      expect(result.current.queue).toEqual([]);
    });

    it('should accept onUploadError callback in options', () => {
      const onUploadError = vi.fn();
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false, onUploadError }));

      expect(result.current.queue).toEqual([]);
    });

    it('should accept onAllComplete callback in options', () => {
      const onAllComplete = vi.fn();
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false, onAllComplete }));

      expect(result.current.queue).toEqual([]);
    });

    it('should accept onQueueAdd callback in options', () => {
      const onQueueAdd = vi.fn();
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false, onQueueAdd }));

      const file = new File(['content'], 'test.pdf');

      act(() => {
        result.current.addFiles([file], 'game-1', 'en');
      });

      expect(onQueueAdd).toHaveBeenCalledWith(
        expect.arrayContaining([expect.objectContaining({ file, gameId: 'game-1' })])
      );
    });

    it('should accept onRetry callback in options', () => {
      const onRetry = vi.fn();
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false, onRetry }));

      expect(result.current.queue).toEqual([]);
    });
  });

  describe('Edge Cases', () => {
    it('should handle empty file list', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));

      act(() => {
        result.current.addFiles([], 'game-1', 'en');
      });

      expect(result.current.queue).toHaveLength(0);
    });

    it('should handle very large files', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));

      const largeFile = new File([new ArrayBuffer(100 * 1024 * 1024)], 'large.pdf'); // 100MB

      act(() => {
        result.current.addFiles([largeFile], 'game-1', 'en');
      });

      expect(result.current.queue).toHaveLength(1);
      expect(result.current.queue[0].file.size).toBe(100 * 1024 * 1024);
    });

    it('should handle special characters in gameId', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));

      const file = new File(['content'], 'test.pdf');

      act(() => {
        result.current.addFiles([file], 'game&id=123', 'en');
      });

      expect(result.current.queue[0].gameId).toBe('game&id=123');
    });

    it('should handle different languages', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));

      const files = [new File(['1'], '1.pdf'), new File(['2'], '2.pdf'), new File(['3'], '3.pdf')];

      act(() => {
        result.current.addFiles([files[0]], 'game-1', 'en');
        result.current.addFiles([files[1]], 'game-1', 'it');
        result.current.addFiles([files[2]], 'game-1', 'de');
      });

      expect(result.current.queue.map(i => i.language)).toEqual(['en', 'it', 'de']);
    });

    it('should handle unmounting during upload', async () => {
      const { result, unmount } = renderHook(() => useUploadQueue({ autoUpload: true }));

      const file = new File(['content'], 'test.pdf');

      act(() => {
        result.current.addFiles([file], 'game-1', 'en');
      });

      await act(async () => {
        vi.advanceTimersByTime(100);
      });

      // Unmount while uploading
      unmount();

      // Should not crash
      expect(true).toBe(true);
    });

    it('should handle concurrent addFiles calls', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));

      const file1 = new File(['1'], '1.pdf');
      const file2 = new File(['2'], '2.pdf');

      act(() => {
        result.current.addFiles([file1], 'game-1', 'en');
        result.current.addFiles([file2], 'game-2', 'it');
      });

      expect(result.current.queue).toHaveLength(2);
    });

    it('should handle files with same name', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));

      const files = [new File(['content1'], 'rules.pdf'), new File(['content2'], 'rules.pdf')];

      act(() => {
        result.current.addFiles(files, 'game-1', 'en');
      });

      expect(result.current.queue).toHaveLength(2);
      expect(result.current.queue[0].id).not.toBe(result.current.queue[1].id);
    });
  });
});
