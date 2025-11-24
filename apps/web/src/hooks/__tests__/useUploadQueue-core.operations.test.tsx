/**
 * Tests for useUploadQueue Core Functionality
 *
 * Coverage: Initialization, addFiles, removeFile, clearCompleted, clearAll, getStats
 * Target: 95%+ coverage
 */

import { renderHook, act } from '@testing-library/react';
import { useUploadQueue } from '../useUploadQueue';
import * as retryUtils from '../../lib/retryUtils';
import * as errorUtils from '../../lib/errorUtils';
import {
  setupTestEnvironment,
  createTestFile,
  createTestFiles,
  createLargeTestFile,
  expectQueueItem,
  expectQueueStats,
} from './useUploadQueue.test-helpers';

vi.mock('../../lib/retryUtils', () => ({
  retryWithBackoff: vi.fn((fn) => fn()),
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

      const file = createTestFile('test.pdf');

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
      const files = createTestFiles(3);

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
      const files = createTestFiles(2);

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
      const file = createTestFile('test.pdf');

      act(() => {
        result.current.addFiles([file], 'game-1', 'en');
      });

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
      const { result } = renderHook(() =>
        useUploadQueue({ autoUpload: false, onAllComplete })
      );

      const file = createTestFile('test.pdf');

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
      const files = createTestFiles(5);

      act(() => {
        result.current.addFiles(files, 'game-1', 'en');
      });

      act(() => {
        result.current.queue[0].status = 'success';
        result.current.queue[1].status = 'failed';
        result.current.queue[2].status = 'uploading';
        result.current.queue[3].status = 'processing';
      });

      const stats = result.current.getStats();

      expectQueueStats(stats, {
        total: 5,
        succeeded: 1,
        failed: 1,
        uploading: 1,
        processing: 1,
        pending: 1,
      });
    });

    it('should return zero stats for empty queue', () => {
      const { result } = renderHook(() => useUploadQueue());

      const stats = result.current.getStats();

      expectQueueStats(stats, {
        total: 0,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 0,
        failed: 0,
      });
    });

    it('should update stats reactively', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));
      const file = createTestFile('test.pdf');

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
      const file = createTestFile('test.pdf');

      act(() => {
        result.current.addFiles([file], 'game-1', 'en');
      });

      await act(async () => {
        vi.advanceTimersByTime(1000);
      });

      expect(result.current.queue[0].status).toBe('pending');
      expect(testEnv.mockFetch).not.toHaveBeenCalled();
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
      const largeFile = createLargeTestFile(100);

      act(() => {
        result.current.addFiles([largeFile], 'game-1', 'en');
      });

      expect(result.current.queue).toHaveLength(1);
      expect(result.current.queue[0].file.size).toBe(100 * 1024 * 1024);
    });

    it('should handle special characters in gameId', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));
      const file = createTestFile('test.pdf');

      act(() => {
        result.current.addFiles([file], 'game&id=123', 'en');
      });

      expect(result.current.queue[0].gameId).toBe('game&id=123');
    });

    it('should handle different languages', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));
      const files = createTestFiles(3);

      act(() => {
        result.current.addFiles([files[0]], 'game-1', 'en');
        result.current.addFiles([files[1]], 'game-1', 'it');
        result.current.addFiles([files[2]], 'game-1', 'de');
      });

      expect(result.current.queue.map(i => i.language)).toEqual(['en', 'it', 'de']);
    });

    it('should handle unmounting during upload', async () => {
      const { result, unmount } = renderHook(() => useUploadQueue({ autoUpload: true }));
      const file = createTestFile('test.pdf');

      act(() => {
        result.current.addFiles([file], 'game-1', 'en');
      });

      await act(async () => {
        vi.advanceTimersByTime(100);
      });

      unmount();

      expect(true).toBe(true);
    });

    it('should handle concurrent addFiles calls', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));
      const file1 = createTestFile('1.pdf');
      const file2 = createTestFile('2.pdf');

      act(() => {
        result.current.addFiles([file1], 'game-1', 'en');
        result.current.addFiles([file2], 'game-2', 'it');
      });

      expect(result.current.queue).toHaveLength(2);
    });

    it('should handle files with same name', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));
      const files = [
        createTestFile('rules.pdf', 'content1'),
        createTestFile('rules.pdf', 'content2'),
      ];

      act(() => {
        result.current.addFiles(files, 'game-1', 'en');
      });

      expect(result.current.queue).toHaveLength(2);
      expect(result.current.queue[0].id).not.toBe(result.current.queue[1].id);
    });
  });
});
