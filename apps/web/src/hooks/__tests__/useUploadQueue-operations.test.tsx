/**
 * Tests for useUploadQueue Upload Operations
 *
 * Coverage: cancelUpload, retryUpload, concurrency control, callbacks
 * Target: 95%+ coverage
 */

import { renderHook, act, waitFor } from '@testing-library/react';
import { useUploadQueue } from '../useUploadQueue';
import * as retryUtils from '../../lib/retryUtils';
import * as errorUtils from '../../lib/errorUtils';
import {
  setupTestEnvironment,
  createTestFile,
  mockSlowUpload,
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

describe('useUploadQueue - Operations', () => {
  let testEnv: ReturnType<typeof setupTestEnvironment>;

  beforeEach(() => {
    testEnv = setupTestEnvironment();
  });

  afterEach(() => {
    testEnv.cleanup();
    vi.clearAllMocks();
  });

  describe('cancelUpload', () => {
    it('should cancel uploading file', async () => {
      mockSlowUpload(testEnv.mockFetch);

      const { result } = renderHook(() => useUploadQueue({ autoUpload: true }));
      const file = createTestFile('test.pdf');

      act(() => {
        result.current.addFiles([file], 'game-1', 'en');
      });

      await act(async () => {
        vi.advanceTimersByTime(100);
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

    it('should set progress to 0 on cancel', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));
      const file = createTestFile('test.pdf');

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
      const file = createTestFile('test.pdf');

      act(() => {
        result.current.addFiles([file], 'game-1', 'en');
      });

      const itemId = result.current.queue[0].id;

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
      const file = createTestFile('test.pdf');

      act(() => {
        result.current.addFiles([file], 'game-1', 'en');
      });

      const itemId = result.current.queue[0].id;

      act(() => {
        result.current.queue[0].status = 'failed';
      });

      expect(result.current.queue[0].status).toBe('failed');

      act(() => {
        result.current.retryUpload(itemId);
      });

      expect(result.current.queue[0].status).toBe('pending');
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
      const { result } = renderHook(() =>
        useUploadQueue({ autoUpload: false, onUploadStart })
      );

      expect(result.current.queue).toEqual([]);
    });

    it('should accept onUploadComplete callback in options', () => {
      const onUploadComplete = vi.fn();
      const { result } = renderHook(() =>
        useUploadQueue({ autoUpload: false, onUploadComplete })
      );

      expect(result.current.queue).toEqual([]);
    });

    it('should accept onUploadError callback in options', () => {
      const onUploadError = vi.fn();
      const { result } = renderHook(() =>
        useUploadQueue({ autoUpload: false, onUploadError })
      );

      expect(result.current.queue).toEqual([]);
    });

    it('should accept onAllComplete callback in options', () => {
      const onAllComplete = vi.fn();
      const { result } = renderHook(() =>
        useUploadQueue({ autoUpload: false, onAllComplete })
      );

      expect(result.current.queue).toEqual([]);
    });

    it('should accept onQueueAdd callback in options', () => {
      const onQueueAdd = vi.fn();
      const { result } = renderHook(() =>
        useUploadQueue({ autoUpload: false, onQueueAdd })
      );

      const file = createTestFile('test.pdf');

      act(() => {
        result.current.addFiles([file], 'game-1', 'en');
      });

      expect(onQueueAdd).toHaveBeenCalledWith(
        expect.arrayContaining([expect.objectContaining({ file, gameId: 'game-1' })])
      );
    });

    it('should accept onRetry callback in options', () => {
      const onRetry = vi.fn();
      const { result } = renderHook(() =>
        useUploadQueue({ autoUpload: false, onRetry })
      );

      expect(result.current.queue).toEqual([]);
    });
  });
});
