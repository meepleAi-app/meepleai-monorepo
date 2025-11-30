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

describe('useUploadQueue - Core', () => {
  let testEnv: ReturnType<typeof setupTestEnvironment>;

  beforeEach(() => {
    testEnv = setupTestEnvironment();
  });

  afterEach(() => {
    testEnv.cleanup();
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
      const file = createTestFile('test.pdf');

      act(() => {
        result.current.addFiles([file], 'game-1', 'en');
      });

      expect(result.current.queue).toHaveLength(1);
      expectQueueItem(result.current.queue[0], {
        file,
        gameId: 'game-1',
        language: 'en',
        status: 'pending',
      });
    });

    it('should add multiple files', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));
      const files = createTestFiles(3);

      act(() => {
        result.current.addFiles(files, 'game-1', 'en');
      });

      expect(result.current.queue).toHaveLength(3);
      expect(result.current.queue.map(i => i.file.name)).toEqual(['test1.pdf', 'test2.pdf', 'test3.pdf']);
    });

    it('should generate unique IDs for each file', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));
      const files = createTestFiles(2);

      act(() => {
        result.current.addFiles(files, 'game-1', 'en');
      });

      const ids = result.current.queue.map(i => i.id);
      expect(new Set(ids).size).toBe(2);
    });

    it('should initialize items with pending status and 0 progress', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));
      const file = createTestFile('test.pdf');

      act(() => {
        result.current.addFiles([file], 'game-1', 'en');
      });

      const item = result.current.queue[0];
      expectQueueItem(item, { status: 'pending', progress: 0 });
      expect(item.retryCount).toBe(0);
      expect(item.error).toBeUndefined();
      expect(item.pdfId).toBeUndefined();
    });

    it('should trigger onQueueAdd callback', () => {
      const onQueueAdd = vi.fn();
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false, onQueueAdd }));
      const file = createTestFile('test.pdf');

      act(() => {
        result.current.addFiles([file], 'game-1', 'en');
      });

      expect(onQueueAdd).toHaveBeenCalledTimes(1);
      expect(onQueueAdd).toHaveBeenCalledWith(expect.arrayContaining([
        expect.objectContaining({ file, gameId: 'game-1' })
      ]));
    });

    it('should reset allCompleteNotified flag when adding files', () => {
      const onAllComplete = vi.fn();
      const { result } = renderHook(() =>
        useUploadQueue({ autoUpload: false, onAllComplete })
      );

      const file1 = createTestFile('1.pdf');

      act(() => {
        result.current.addFiles([file1], 'game-1', 'en');
      });

      act(() => {
        result.current.clearAll();
      });

      const file2 = createTestFile('2.pdf');

      act(() => {
        result.current.addFiles([file2], 'game-1', 'en');
      });

      expect(result.current.queue).toHaveLength(1);
    });
  });

  describe('removeFile', () => {
    it('should remove file from queue when not uploading', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));
      const file = createTestFile('test.pdf');

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
      const file = createTestFile('test.pdf');

      act(() => {
        result.current.addFiles([file], 'game-1', 'en');
      });

      const itemId = result.current.queue[0].id;

      act(() => {
        result.current.queue[0].status = 'uploading';
      });

      act(() => {
        result.current.removeFile(itemId);
      });

      expect(result.current.queue).toHaveLength(1);
    });

    it('should remove specific file from multiple files', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));
      const files = createTestFiles(3);

      act(() => {
        result.current.addFiles(files, 'game-1', 'en');
      });

      const middleItemId = result.current.queue[1].id;

      act(() => {
        result.current.removeFile(middleItemId);
      });

      expect(result.current.queue).toHaveLength(2);
      expect(result.current.queue.map(i => i.file.name)).toEqual(['test1.pdf', 'test3.pdf']);
    });

    it('should handle removing non-existent ID gracefully', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));

      act(() => {
        result.current.removeFile('non-existent-id');
      });

      expect(result.current.queue).toHaveLength(0);
    });
  });

  describe('clearCompleted', () => {
    it('should remove completed uploads', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));
      const files = createTestFiles(3);

      act(() => {
        result.current.addFiles(files, 'game-1', 'en');
      });

      act(() => {
        result.current.queue[0].status = 'success';
        result.current.queue[1].status = 'failed';
      });

      act(() => {
        result.current.clearCompleted();
      });

      expect(result.current.queue).toHaveLength(1);
      expect(result.current.queue[0].status).toBe('pending');
    });

    it('should remove cancelled uploads', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));
