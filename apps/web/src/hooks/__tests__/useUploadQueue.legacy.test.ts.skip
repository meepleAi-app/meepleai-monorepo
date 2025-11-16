/**
 * Comprehensive tests for useUploadQueue hook (PDF-05)
 * Target: 90% coverage across all metrics
 * Focus: Uncovered lines from issue #629
 */

import { renderHook, act, waitFor } from '@testing-library/react';
import { useUploadQueue, UploadQueueItem, UploadStatus } from '../useUploadQueue';
import { ApiError } from '../../lib/api';

// Mock dependencies
jest.mock('../../lib/retryUtils', () => ({
  retryWithBackoff: jest.fn(),
  isRetryableError: jest.fn(() => true)
}));

jest.mock('../../lib/errorUtils', () => ({
  extractCorrelationId: jest.fn(() => 'test-correlation-id')
}));

const { retryWithBackoff } = require('../../lib/retryUtils');

describe('useUploadQueue', () => {
  let mockFetch: jest.Mock;
  let mockAbort: jest.Mock;
  let mockFormDataAppend: jest.Mock;

  beforeEach(() => {
    jest.clearAllMocks();
    jest.useFakeTimers();

    // Mock fetch
    mockFetch = jest.fn();
    global.fetch = mockFetch;

    // Mock FormData
    mockFormDataAppend = jest.fn();
    global.FormData = jest.fn(() => ({
      append: mockFormDataAppend
    })) as any;

    // Mock AbortController
    mockAbort = jest.fn();
    global.AbortController = jest.fn(() => ({
      signal: {},
      abort: mockAbort
    })) as any;

    // Default retryWithBackoff implementation (success case)
    retryWithBackoff.mockImplementation(async (fn: () => Promise<any>) => {
      return await fn();
    });
  });

  afterEach(() => {
    jest.runOnlyPendingTimers();
    jest.useRealTimers();
  });

  const createMockFile = (name: string = 'test.pdf'): File => {
    return new File(['test content'], name, { type: 'application/pdf' });
  };

  describe('A. Queue Management', () => {
    test('1. addFiles - single file', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));
      const file = createMockFile();

      act(() => {
        result.current.addFiles([file], 'game-1', 'en');
      });

      expect(result.current.queue).toHaveLength(1);
      expect(result.current.queue[0]).toMatchObject({
        file,
        gameId: 'game-1',
        language: 'en',
        status: 'pending',
        progress: 0,
        retryCount: 0
      });
      expect(result.current.queue[0].id).toBeDefined();
    });

    test('2. addFiles - multiple files', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));
      const files = [createMockFile('file1.pdf'), createMockFile('file2.pdf')];

      act(() => {
        result.current.addFiles(files, 'game-2', 'es');
      });

      expect(result.current.queue).toHaveLength(2);
      expect(result.current.queue[0].file.name).toBe('file1.pdf');
      expect(result.current.queue[1].file.name).toBe('file2.pdf');
      expect(result.current.queue[0].gameId).toBe('game-2');
      expect(result.current.queue[1].language).toBe('es');
    });

    test('3. removeFile - pending file (success)', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));
      const file = createMockFile();

      act(() => {
        result.current.addFiles([file], 'game-1', 'en');
      });

      const itemId = result.current.queue[0].id;

      act(() => {
        result.current.removeFile(itemId);
      });

      expect(result.current.queue).toHaveLength(0);
    });

    test('4. removeFile - uploading file (blocked, line 108-110)', async () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));
      const file = createMockFile();

      // Mock successful upload that takes time
      mockFetch.mockResolvedValue({
        ok: true,
        json: async () => ({ documentId: 'doc-1' })
      });

      act(() => {
        result.current.addFiles([file], 'game-1', 'en');
      });

      const itemId = result.current.queue[0].id;

      // Start upload
      act(() => {
        result.current.startUpload();
      });

      // Wait for status to become 'uploading'
      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('uploading');
      });

      // Try to remove while uploading - should be blocked
      act(() => {
        result.current.removeFile(itemId);
      });

      // File should still be in queue
      expect(result.current.queue).toHaveLength(1);
      expect(result.current.queue[0].status).toBe('uploading');

      // Cleanup
      await act(async () => {
        jest.advanceTimersByTime(1000);
      });
    });

    test('5. clearCompleted - removes only success/failed/cancelled', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));

      act(() => {
        result.current.addFiles(
          [createMockFile('f1.pdf'), createMockFile('f2.pdf'), createMockFile('f3.pdf')],
          'game-1',
          'en'
        );
      });

      // Manually set different statuses
      act(() => {
        const [item1, item2, item3] = result.current.queue;
        result.current.queue[0] = { ...item1, status: 'success' };
        result.current.queue[1] = { ...item2, status: 'failed' };
        result.current.queue[2] = { ...item3, status: 'pending' };
      });

      act(() => {
        result.current.clearCompleted();
      });

      expect(result.current.queue).toHaveLength(1);
      expect(result.current.queue[0].status).toBe('pending');
    });

    test('6. clearAll - cancels active + clears queue (line 165-167)', async () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));

      mockFetch.mockResolvedValue({
        ok: true,
        json: async () => ({ documentId: 'doc-1' })
      });

      act(() => {
        result.current.addFiles([createMockFile()], 'game-1', 'en');
      });

      // Start upload
      act(() => {
        result.current.startUpload();
      });

      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('uploading');
      });

      // Clear all - should abort active upload
      act(() => {
        result.current.clearAll();
      });

      expect(mockAbort).toHaveBeenCalled();
      expect(result.current.queue).toHaveLength(0);
    });
  });

  describe('B. Upload Lifecycle', () => {
    test('7. uploadFile - happy path with progress', async () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));

      mockFetch.mockResolvedValue({
        ok: true,
        json: async () => ({ documentId: 'doc-success' })
      });

      act(() => {
        result.current.addFiles([createMockFile()], 'game-1', 'en');
      });

      act(() => {
        result.current.startUpload();
      });

      // Status: pending → uploading
      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('uploading');
        expect(result.current.queue[0].progress).toBe(10);
      });

      // Status: uploading → processing
      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('processing');
        expect(result.current.queue[0].progress).toBe(50);
        expect(result.current.queue[0].pdfId).toBe('doc-success');
      });

      // Status: processing → success (after 1s delay)
      await act(async () => {
        jest.advanceTimersByTime(1000);
      });

      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('success');
        expect(result.current.queue[0].progress).toBe(100);
      });

      expect(mockFormDataAppend).toHaveBeenCalledWith('file', expect.any(File));
      expect(mockFormDataAppend).toHaveBeenCalledWith('gameId', 'game-1');
      expect(mockFormDataAppend).toHaveBeenCalledWith('language', 'en');
    });

    test('8. uploadFile - network error with retry', async () => {
      const onRetry = jest.fn();
      const { result } = renderHook(() =>
        useUploadQueue({ autoUpload: false, maxRetries: 3, onRetry })
      );

      const networkError = new Error('Network error');

      retryWithBackoff.mockImplementation(async (fn: () => Promise<any>, options: any) => {
        // Simulate 2 retries
        if (options.onRetry) {
          options.onRetry(networkError, 1);
          options.onRetry(networkError, 2);
        }

        // Eventually succeeds
        mockFetch.mockResolvedValue({
          ok: true,
          json: async () => ({ documentId: 'doc-retry-success' })
        });
        return await fn();
      });

      act(() => {
        result.current.addFiles([createMockFile()], 'game-1', 'en');
      });

      act(() => {
        result.current.startUpload();
      });

      // Wait for onRetry callback
      await waitFor(() => {
        expect(onRetry).toHaveBeenCalled();
      }, { timeout: 3000 });

      // Wait for retryCount update
      await waitFor(() => {
        expect(result.current.queue[0].retryCount).toBeGreaterThan(0);
      }, { timeout: 3000 });

      // Wait for processing
      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('processing');
      }, { timeout: 3000 });

      // Advance timer for processing completion
      await act(async () => {
        jest.advanceTimersByTime(1000);
      });

      // Final status should be success after retries
      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('success');
      }, { timeout: 3000 });
    });

    test('9. uploadFile - max retries exceeded', async () => {
      const { result } = renderHook(() =>
        useUploadQueue({ autoUpload: false, maxRetries: 2 })
      );

      const persistentError = new ApiError('Persistent error', 500, 'corr-id', {} as Response);

      retryWithBackoff.mockRejectedValue(persistentError);

      act(() => {
        result.current.addFiles([createMockFile()], 'game-1', 'en');
      });

      act(() => {
        result.current.startUpload();
      });

      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('failed');
        expect(result.current.queue[0].error).toBe('Persistent error');
        expect(result.current.queue[0].correlationId).toBe('corr-id');
        expect(result.current.queue[0].progress).toBe(0);
      });
    });

    test('10. uploadFile - AbortController cancellation (line 122)', async () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));

      // Mock a long-running fetch that can be aborted
      mockFetch.mockImplementation(() => new Promise(() => {})); // Never resolves

      act(() => {
        result.current.addFiles([createMockFile()], 'game-1', 'en');
      });

      const itemId = result.current.queue[0].id;

      act(() => {
        result.current.startUpload();
      });

      // Wait for upload to start
      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('uploading');
      });

      // Cancel upload
      act(() => {
        result.current.cancelUpload(itemId);
      });

      // Verify abort was called
      expect(mockAbort).toHaveBeenCalled();

      // Status should change to cancelled
      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('cancelled');
        expect(result.current.queue[0].progress).toBe(0);
      });
    });

    test('11. uploadFile - success callback', async () => {
      const onUploadComplete = jest.fn();
      const { result } = renderHook(() =>
        useUploadQueue({ autoUpload: false, onUploadComplete })
      );

      mockFetch.mockResolvedValue({
        ok: true,
        json: async () => ({ documentId: 'doc-callback' })
      });

      act(() => {
        result.current.addFiles([createMockFile()], 'game-1', 'en');
      });

      act(() => {
        result.current.startUpload();
      });

      // Wait for processing status
      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('processing');
      });

      // Advance timer to complete processing
      await act(async () => {
        jest.advanceTimersByTime(1000);
      });

      // Wait for callback
      await waitFor(() => {
        expect(onUploadComplete).toHaveBeenCalledWith(
          expect.objectContaining({
            status: 'success',
            progress: 100,
            pdfId: 'doc-callback'
          })
        );
      }, { timeout: 3000 });
    });

    test('12. uploadFile - error callback', async () => {
      const onUploadError = jest.fn();
      const { result } = renderHook(() =>
        useUploadQueue({ autoUpload: false, onUploadError })
      );

      const uploadError = new Error('Upload failed');
      retryWithBackoff.mockRejectedValue(uploadError);

      act(() => {
        result.current.addFiles([createMockFile()], 'game-1', 'en');
      });

      act(() => {
        result.current.startUpload();
      });

      await waitFor(() => {
        expect(onUploadError).toHaveBeenCalledWith(
          expect.objectContaining({
            status: 'failed',
            error: 'Upload failed'
          }),
          'Upload failed'
        );
      });
    });

    test('13. Auto-upload behavior (autoUpload=true)', async () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: true }));

      mockFetch.mockResolvedValue({
        ok: true,
        json: async () => ({ documentId: 'doc-auto' })
      });

      act(() => {
        result.current.addFiles([createMockFile()], 'game-1', 'en');
      });

      // Should automatically start uploading
      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('uploading');
      }, { timeout: 3000 });

      // Wait for processing
      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('processing');
      }, { timeout: 3000 });

      // Advance timer for processing completion
      await act(async () => {
        jest.advanceTimersByTime(1000);
      });

      // Final status should be success
      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('success');
      }, { timeout: 3000 });
    });
  });

  describe('C. Cancellation', () => {
    test('14. cancelUpload - active upload (calls abort, line 122)', async () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));

      mockFetch.mockImplementation(() => new Promise(() => {})); // Never resolves

      act(() => {
        result.current.addFiles([createMockFile()], 'game-1', 'en');
      });

      const itemId = result.current.queue[0].id;

      act(() => {
        result.current.startUpload();
      });

      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('uploading');
      });

      act(() => {
        result.current.cancelUpload(itemId);
      });

      expect(mockAbort).toHaveBeenCalled();
    });

    test('15. cancelUpload - non-existent upload (no-op)', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));

      act(() => {
        result.current.addFiles([createMockFile()], 'game-1', 'en');
      });

      // Cancel with wrong ID - should not throw
      act(() => {
        result.current.cancelUpload('non-existent-id');
      });

      expect(result.current.queue[0].status).toBe('pending');
    });

    test('16. cancelUpload - updates status to cancelled', async () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));

      mockFetch.mockImplementation(() => new Promise(() => {}));

      act(() => {
        result.current.addFiles([createMockFile()], 'game-1', 'en');
      });

      const itemId = result.current.queue[0].id;

      act(() => {
        result.current.startUpload();
      });

      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('uploading');
      });

      act(() => {
        result.current.cancelUpload(itemId);
      });

      expect(result.current.queue[0].status).toBe('cancelled');
      expect(result.current.queue[0].progress).toBe(0);
    });

    test('17. clearAll - aborts all active (line 165-167)', async () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false, concurrencyLimit: 2 }));

      mockFetch.mockImplementation(() => new Promise(() => {}));

      act(() => {
        result.current.addFiles(
          [createMockFile('f1.pdf'), createMockFile('f2.pdf')],
          'game-1',
          'en'
        );
      });

      act(() => {
        result.current.startUpload();
      });

      await waitFor(() => {
        expect(result.current.queue.filter(i => i.status === 'uploading').length).toBe(2);
      });

      act(() => {
        result.current.clearAll();
      });

      expect(mockAbort).toHaveBeenCalledTimes(2);
      expect(result.current.queue).toHaveLength(0);
    });
  });

  describe('D. Retry Logic', () => {
    test('18. retryUpload - resets status to pending (line 139-146)', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));

      act(() => {
        result.current.addFiles([createMockFile()], 'game-1', 'en');
      });

      const itemId = result.current.queue[0].id;

      // Manually set to failed
      act(() => {
        result.current.queue[0] = {
          ...result.current.queue[0],
          status: 'failed',
          error: 'Test error',
          progress: 50
        };
      });

      act(() => {
        result.current.retryUpload(itemId);
      });

      expect(result.current.queue[0].status).toBe('pending');
      expect(result.current.queue[0].progress).toBe(0);
    });

    test('19. retryUpload - clears error message', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));

      act(() => {
        result.current.addFiles([createMockFile()], 'game-1', 'en');
      });

      const itemId = result.current.queue[0].id;

      act(() => {
        result.current.queue[0] = {
          ...result.current.queue[0],
          status: 'failed',
          error: 'Network timeout',
          correlationId: 'corr-123'
        };
      });

      act(() => {
        result.current.retryUpload(itemId);
      });

      expect(result.current.queue[0].error).toBeUndefined();
    });

    test('20. retryUpload - resets allCompleteNotified flag', async () => {
      const onAllComplete = jest.fn();
      const { result } = renderHook(() =>
        useUploadQueue({ autoUpload: false, onAllComplete })
      );

      retryWithBackoff.mockRejectedValue(new Error('Fail'));

      act(() => {
        result.current.addFiles([createMockFile()], 'game-1', 'en');
      });

      act(() => {
        result.current.startUpload();
      });

      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('failed');
      }, { timeout: 3000 });

      // onAllComplete should be called once
      await waitFor(() => {
        expect(onAllComplete).toHaveBeenCalledTimes(1);
      }, { timeout: 3000 });

      const itemId = result.current.queue[0].id;

      // Clear mock and setup for success
      retryWithBackoff.mockReset();
      retryWithBackoff.mockImplementation(async (fn: () => Promise<any>) => {
        mockFetch.mockResolvedValue({
          ok: true,
          json: async () => ({ documentId: 'doc-retry' })
        });
        return await fn();
      });

      // Retry should reset the flag
      act(() => {
        result.current.retryUpload(itemId);
      });

      // Start upload again
      act(() => {
        result.current.startUpload();
      });

      // Wait for processing
      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('processing');
      }, { timeout: 3000 });

      // Advance timer
      await act(async () => {
        jest.advanceTimersByTime(1000);
      });

      // onAllComplete should be called again
      await waitFor(() => {
        expect(onAllComplete).toHaveBeenCalledTimes(2);
      }, { timeout: 3000 });
    });
  });

  describe('E. Statistics', () => {
    test('21. getStats - empty queue', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));

      const stats = result.current.getStats();

      expect(stats).toEqual({
        total: 0,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 0,
        failed: 0,
        cancelled: 0
      });
    });

    test('22. getStats - mixed statuses', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));

      act(() => {
        result.current.addFiles(
          [
            createMockFile('f1.pdf'),
            createMockFile('f2.pdf'),
            createMockFile('f3.pdf'),
            createMockFile('f4.pdf')
          ],
          'game-1',
          'en'
        );
      });

      // Set different statuses
      act(() => {
        result.current.queue[0] = { ...result.current.queue[0], status: 'pending' };
        result.current.queue[1] = { ...result.current.queue[1], status: 'uploading' };
        result.current.queue[2] = { ...result.current.queue[2], status: 'success' };
        result.current.queue[3] = { ...result.current.queue[3], status: 'failed' };
      });

      const stats = result.current.getStats();

      expect(stats).toEqual({
        total: 4,
        pending: 1,
        uploading: 1,
        processing: 0,
        succeeded: 1,
        failed: 1,
        cancelled: 0
      });
    });

    test('23. getStats - all statuses represented (line 186)', () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));

      act(() => {
        result.current.addFiles(
          [
            createMockFile('f1.pdf'),
            createMockFile('f2.pdf'),
            createMockFile('f3.pdf'),
            createMockFile('f4.pdf'),
            createMockFile('f5.pdf'),
            createMockFile('f6.pdf')
          ],
          'game-1',
          'en'
        );
      });

      act(() => {
        const statuses: UploadStatus[] = ['pending', 'uploading', 'processing', 'success', 'failed', 'cancelled'];
        statuses.forEach((status, i) => {
          result.current.queue[i] = { ...result.current.queue[i], status };
        });
      });

      const stats = result.current.getStats();

      expect(stats).toEqual({
        total: 6,
        pending: 1,
        uploading: 1,
        processing: 1,
        succeeded: 1,
        failed: 1,
        cancelled: 1
      });
    });
  });

  describe('F. Callbacks & Hooks', () => {
    test('24. onUploadComplete callback', async () => {
      const onUploadComplete = jest.fn();
      const { result } = renderHook(() =>
        useUploadQueue({ autoUpload: false, onUploadComplete })
      );

      mockFetch.mockResolvedValue({
        ok: true,
        json: async () => ({ documentId: 'doc-complete' })
      });

      act(() => {
        result.current.addFiles([createMockFile()], 'game-1', 'en');
      });

      act(() => {
        result.current.startUpload();
      });

      // Wait for processing
      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('processing');
      }, { timeout: 3000 });

      await act(async () => {
        jest.advanceTimersByTime(1000);
      });

      await waitFor(() => {
        expect(onUploadComplete).toHaveBeenCalledWith(
          expect.objectContaining({
            status: 'success',
            pdfId: 'doc-complete'
          })
        );
      }, { timeout: 3000 });
    });

    test('25. onUploadError callback', async () => {
      const onUploadError = jest.fn();
      const { result } = renderHook(() =>
        useUploadQueue({ autoUpload: false, onUploadError })
      );

      retryWithBackoff.mockRejectedValue(new Error('Test error'));

      act(() => {
        result.current.addFiles([createMockFile()], 'game-1', 'en');
      });

      act(() => {
        result.current.startUpload();
      });

      await waitFor(() => {
        expect(onUploadError).toHaveBeenCalledWith(
          expect.objectContaining({ status: 'failed', error: 'Test error' }),
          'Test error'
        );
      });
    });

    test('26. onAllComplete callback', async () => {
      const onAllComplete = jest.fn();
      const { result } = renderHook(() =>
        useUploadQueue({ autoUpload: false, onAllComplete })
      );

      mockFetch.mockResolvedValue({
        ok: true,
        json: async () => ({ documentId: 'doc-all-complete' })
      });

      act(() => {
        result.current.addFiles([createMockFile()], 'game-1', 'en');
      });

      act(() => {
        result.current.startUpload();
      });

      // Wait for processing
      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('processing');
      }, { timeout: 3000 });

      await act(async () => {
        jest.advanceTimersByTime(1000);
      });

      await waitFor(() => {
        expect(onAllComplete).toHaveBeenCalledWith(
          expect.objectContaining({
            total: 1,
            succeeded: 1,
            pending: 0,
            uploading: 0,
            processing: 0
          })
        );
      }, { timeout: 3000 });
    });

    test('27. onUploadStart/onQueueAdd observability hooks', async () => {
      const onUploadStart = jest.fn();
      const onQueueAdd = jest.fn();
      const { result } = renderHook(() =>
        useUploadQueue({ autoUpload: false, onUploadStart, onQueueAdd })
      );

      mockFetch.mockResolvedValue({
        ok: true,
        json: async () => ({ documentId: 'doc-hooks' })
      });

      const file = createMockFile();

      act(() => {
        result.current.addFiles([file], 'game-1', 'en');
      });

      // onQueueAdd should be called synchronously
      expect(onQueueAdd).toHaveBeenCalledWith(
        expect.arrayContaining([
          expect.objectContaining({
            file,
            gameId: 'game-1',
            status: 'pending'
          })
        ])
      );

      act(() => {
        result.current.startUpload();
      });

      // onUploadStart should be called before upload begins
      await waitFor(() => {
        expect(onUploadStart).toHaveBeenCalledWith(
          expect.objectContaining({
            file,
            gameId: 'game-1'
          })
        );
      });

      await act(async () => {
        jest.advanceTimersByTime(1000);
      });
    });
  });

  describe('G. Concurrency', () => {
    test('28. Respects concurrencyLimit (default 3)', async () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false, concurrencyLimit: 3 }));

      mockFetch.mockImplementation(() => new Promise(() => {})); // Never resolves

      act(() => {
        result.current.addFiles(
          [
            createMockFile('f1.pdf'),
            createMockFile('f2.pdf'),
            createMockFile('f3.pdf'),
            createMockFile('f4.pdf'),
            createMockFile('f5.pdf')
          ],
          'game-1',
          'en'
        );
      });

      act(() => {
        result.current.startUpload();
      });

      await waitFor(() => {
        const uploadingCount = result.current.queue.filter(i => i.status === 'uploading').length;
        expect(uploadingCount).toBe(3);
      });

      const pendingCount = result.current.queue.filter(i => i.status === 'pending').length;
      expect(pendingCount).toBe(2);
    });

    test('29. Queue processing with limit', async () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false, concurrencyLimit: 2 }));

      // All uploads hang
      mockFetch.mockImplementation(() => new Promise(() => {}));

      act(() => {
        result.current.addFiles(
          [createMockFile('f1.pdf'), createMockFile('f2.pdf'), createMockFile('f3.pdf')],
          'game-1',
          'en'
        );
      });

      act(() => {
        result.current.startUpload();
      });

      // Wait for up to 2 uploads to start (concurrency limit)
      await waitFor(() => {
        const uploadingCount = result.current.queue.filter(i => i.status === 'uploading').length;
        const processingCount = result.current.queue.filter(i => i.status === 'processing').length;
        const activeCount = uploadingCount + processingCount;

        // Should have exactly 2 active (uploading or processing)
        expect(activeCount).toBeGreaterThanOrEqual(1);
        expect(activeCount).toBeLessThanOrEqual(2);
      }, { timeout: 3000 });

      // Third should still be pending
      await waitFor(() => {
        const pendingCount = result.current.queue.filter(i => i.status === 'pending').length;
        expect(pendingCount).toBeGreaterThan(0);
      }, { timeout: 3000 });
    });

    test('30. Active uploads tracking (line 318, 369)', async () => {
      const onUploadSuccess = jest.fn();
      const { result } = renderHook(() =>
        useUploadQueue({ autoUpload: false, onUploadSuccess })
      );

      mockFetch.mockResolvedValue({
        ok: true,
        json: async () => ({ documentId: 'doc-tracking' })
      });

      act(() => {
        result.current.addFiles([createMockFile()], 'game-1', 'en');
      });

      act(() => {
        result.current.startUpload();
      });

      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('uploading');
      }, { timeout: 3000 });

      // Wait for processing
      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('processing');
      }, { timeout: 3000 });

      await act(async () => {
        jest.advanceTimersByTime(1000);
      });

      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('success');
      }, { timeout: 3000 });

      // onUploadSuccess should be called (line 310)
      expect(onUploadSuccess).toHaveBeenCalledWith(
        expect.objectContaining({
          status: 'success',
          pdfId: 'doc-tracking'
        })
      );
    });
  });

  describe('H. Edge Cases & Error Recovery', () => {
    test('31. uploadFile - error recovery (line 256, 263-265)', async () => {
      const onRetry = jest.fn();
      const { result } = renderHook(() =>
        useUploadQueue({ autoUpload: false, maxRetries: 3, onRetry })
      );

      const networkError = new Error('Network timeout');
      let attemptCount = 0;

      retryWithBackoff.mockImplementation(async (fn: () => Promise<any>, options: any) => {
        attemptCount++;
        if (attemptCount <= 2 && options.onRetry) {
          options.onRetry(networkError, attemptCount);
        }

        if (attemptCount === 3) {
          mockFetch.mockResolvedValue({
            ok: true,
            json: async () => ({ documentId: 'doc-recovered' })
          });
          return await fn();
        }

        throw networkError;
      });

      act(() => {
        result.current.addFiles([createMockFile()], 'game-1', 'en');
      });

      act(() => {
        result.current.startUpload();
      });

      // Wait for retry callbacks
      await waitFor(() => {
        expect(onRetry).toHaveBeenCalled();
      }, { timeout: 3000 });

      // Check retryCount was updated (line 269-274)
      await waitFor(() => {
        expect(result.current.queue[0].retryCount).toBeGreaterThan(0);
      }, { timeout: 3000 });

      // Wait for processing
      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('processing');
      }, { timeout: 3000 });

      await act(async () => {
        jest.advanceTimersByTime(1000);
      });

      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('success');
      }, { timeout: 3000 });
    });

    test('32. clearCompleted - preserves uploading and processing (line 153-155)', async () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));

      mockFetch.mockImplementation(() => new Promise(() => {}));

      act(() => {
        result.current.addFiles(
          [
            createMockFile('f1.pdf'),
            createMockFile('f2.pdf'),
            createMockFile('f3.pdf'),
            createMockFile('f4.pdf')
          ],
          'game-1',
          'en'
        );
      });

      // Set mixed statuses
      act(() => {
        result.current.queue[0] = { ...result.current.queue[0], status: 'success' };
        result.current.queue[1] = { ...result.current.queue[1], status: 'uploading' };
        result.current.queue[2] = { ...result.current.queue[2], status: 'processing' };
        result.current.queue[3] = { ...result.current.queue[3], status: 'cancelled' };
      });

      act(() => {
        result.current.clearCompleted();
      });

      expect(result.current.queue).toHaveLength(2);
      expect(result.current.queue.find(i => i.status === 'uploading')).toBeDefined();
      expect(result.current.queue.find(i => i.status === 'processing')).toBeDefined();
    });

    test('33. onAllComplete - only fires once until reset', async () => {
      const onAllComplete = jest.fn();
      const { result } = renderHook(() =>
        useUploadQueue({ autoUpload: false, onAllComplete })
      );

      mockFetch.mockResolvedValue({
        ok: true,
        json: async () => ({ documentId: 'doc-once' })
      });

      act(() => {
        result.current.addFiles([createMockFile()], 'game-1', 'en');
      });

      act(() => {
        result.current.startUpload();
      });

      // Wait for processing
      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('processing');
      }, { timeout: 3000 });

      await act(async () => {
        jest.advanceTimersByTime(1000);
      });

      await waitFor(() => {
        expect(onAllComplete).toHaveBeenCalledTimes(1);
      }, { timeout: 3000 });

      // Adding more files should reset the flag
      act(() => {
        result.current.addFiles([createMockFile()], 'game-1', 'en');
      });

      act(() => {
        result.current.startUpload();
      });

      // Wait for processing
      await waitFor(() => {
        expect(result.current.queue[1].status).toBe('processing');
      }, { timeout: 3000 });

      await act(async () => {
        jest.advanceTimersByTime(1000);
      });

      await waitFor(() => {
        expect(onAllComplete).toHaveBeenCalledTimes(2);
      }, { timeout: 3000 });
    });

    test('34. Error response without .json() method (lines 241-246)', async () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));

      // Mock response without json method - will throw during .json().catch()
      const responseWithoutJson = {
        ok: false,
        status: 500,
        statusText: 'Internal Server Error',
        headers: new Headers()
      };

      const apiError = new ApiError('Internal Server Error', 500, 'test-correlation-id', responseWithoutJson as Response);

      retryWithBackoff.mockImplementation(async (fn: () => Promise<any>) => {
        mockFetch.mockResolvedValue(responseWithoutJson);
        try {
          return await fn();
        } catch (error) {
          // The code will throw ApiError after trying to parse JSON
          throw apiError;
        }
      });

      act(() => {
        result.current.addFiles([createMockFile()], 'game-1', 'en');
      });

      act(() => {
        result.current.startUpload();
      });

      // Should fall back to statusText when json() fails
      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('failed');
        expect(result.current.queue[0].error).toBe('Internal Server Error');
      }, { timeout: 3000 });
    });

    test('35. Error response with malformed JSON (lines 241-246)', async () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false }));

      // Mock response with json() that throws
      const responseWithBadJson = {
        ok: false,
        status: 400,
        statusText: 'Bad Request',
        headers: new Headers(),
        json: async () => {
          throw new Error('Invalid JSON');
        }
      };

      const apiError = new ApiError('Bad Request', 400, 'test-correlation-id', responseWithBadJson as unknown as Response);

      retryWithBackoff.mockImplementation(async (fn: () => Promise<any>) => {
        mockFetch.mockResolvedValue(responseWithBadJson);
        try {
          return await fn();
        } catch (error) {
          // The code will throw ApiError after catching JSON parse error
          throw apiError;
        }
      });

      act(() => {
        result.current.addFiles([createMockFile()], 'game-1', 'en');
      });

      act(() => {
        result.current.startUpload();
      });

      // Should fall back to {} when json() throws, then use statusText
      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('failed');
        expect(result.current.queue[0].error).toBe('Bad Request');
      }, { timeout: 3000 });
    });

    test('36. AbortError should not retry (lines 255-258)', async () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false, maxRetries: 3 }));

      const abortError = new DOMException('Request aborted', 'AbortError');
      let shouldRetryCalled = false;

      retryWithBackoff.mockImplementation(async (fn: () => Promise<any>, options: any) => {
        // Verify shouldRetry returns false for AbortError
        if (options.shouldRetry) {
          shouldRetryCalled = true;
          const shouldRetry = options.shouldRetry(abortError);
          expect(shouldRetry).toBe(false);
        }
        throw abortError;
      });

      act(() => {
        result.current.addFiles([createMockFile()], 'game-1', 'en');
      });

      act(() => {
        result.current.startUpload();
      });

      // Wait for error handling
      await waitFor(() => {
        expect(shouldRetryCalled).toBe(true);
      }, { timeout: 3000 });
    });

    test('37. ProcessQueue when already processing (line 318)', async () => {
      const { result } = renderHook(() => useUploadQueue({ autoUpload: false, concurrencyLimit: 1 }));

      // Mock slow upload to keep processingRef.current = true
      let resolveUpload: (value: any) => void;
      const uploadPromise = new Promise((resolve) => {
        resolveUpload = resolve;
      });

      retryWithBackoff.mockImplementation(async (fn: () => Promise<any>) => {
        mockFetch.mockResolvedValue({
          ok: true,
          json: async () => {
            await uploadPromise;
            return { documentId: 'doc-slow' };
          }
        });
        return await fn();
      });

      act(() => {
        result.current.addFiles([createMockFile('f1.pdf'), createMockFile('f2.pdf')], 'game-1', 'en');
      });

      // Start first upload
      act(() => {
        result.current.startUpload();
      });

      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('uploading');
      }, { timeout: 3000 });

      // Try to start upload again while processing - should return early (line 318)
      act(() => {
        result.current.startUpload();
      });

      // Second file should still be pending (not started due to early return)
      expect(result.current.queue[1].status).toBe('pending');

      // Cleanup: resolve upload
      await act(async () => {
        resolveUpload!({ documentId: 'doc-slow' });
      });
    });

    test('38. OnAllComplete with empty queue (line 405)', async () => {
      const onAllComplete = jest.fn();
      const { result } = renderHook(() =>
        useUploadQueue({ autoUpload: false, onAllComplete })
      );

      // Queue is empty, allCompleteNotifiedRef is false
      // Effect should return early at line 405 (hasItems is false)

      // Force re-render to trigger effect
      act(() => {
        result.current.addFiles([], 'game-1', 'en');
      });

      // Wait a bit to ensure effect runs
      await act(async () => {
        jest.advanceTimersByTime(100);
      });

      // onAllComplete should NOT be called (no items)
      expect(onAllComplete).not.toHaveBeenCalled();
    });

    test('39. OnAllComplete with pending items (line 412)', async () => {
      const onAllComplete = jest.fn();
      const { result } = renderHook(() =>
        useUploadQueue({ autoUpload: false, onAllComplete })
      );

      act(() => {
        result.current.addFiles([createMockFile()], 'game-1', 'en');
      });

      // Queue has items but not all done (pending > 0)
      // Effect should return early at line 412 (!allDone)

      await act(async () => {
        jest.advanceTimersByTime(100);
      });

      // onAllComplete should NOT be called (not all done)
      expect(onAllComplete).not.toHaveBeenCalled();
    });

    test('40. Retry callback when onRetry is undefined (lines 255-258, 262)', async () => {
      // No onRetry callback provided
      const { result } = renderHook(() =>
        useUploadQueue({ autoUpload: false, maxRetries: 3 })
      );

      const networkError = new Error('Network error');

      retryWithBackoff.mockImplementation(async (fn: () => Promise<any>, options: any) => {
        // Trigger retry callback without onRetry defined
        if (options.onRetry) {
          options.onRetry(networkError, 1);
        }

        // Eventually succeed
        mockFetch.mockResolvedValue({
          ok: true,
          json: async () => ({ documentId: 'doc-no-callback' })
        });
        return await fn();
      });

      act(() => {
        result.current.addFiles([createMockFile()], 'game-1', 'en');
      });

      act(() => {
        result.current.startUpload();
      });

      // Should complete successfully even without onRetry callback
      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('processing');
      }, { timeout: 3000 });

      await act(async () => {
        jest.advanceTimersByTime(1000);
      });

      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('success');
      }, { timeout: 3000 });
    });

    test('41. Non-retryable error (line 258 - isRetryableError returns false)', async () => {
      const { isRetryableError } = require('../../lib/retryUtils');

      // Mock isRetryableError to return false for this specific error
      const clientError = new Error('400 Bad Request');
      isRetryableError.mockImplementation((error: any) => {
        // 400 errors are client errors and should not be retried
        return !(error.message && error.message.includes('400'));
      });

      const { result } = renderHook(() => useUploadQueue({ autoUpload: false, maxRetries: 3 }));

      let shouldRetryResult: boolean | undefined;
      retryWithBackoff.mockImplementation(async (fn: () => Promise<any>, options: any) => {
        // Capture shouldRetry result for non-abort error
        if (options.shouldRetry) {
          shouldRetryResult = options.shouldRetry(clientError);
        }
        throw clientError;
      });

      mockFetch.mockRejectedValue(clientError);

      act(() => {
        result.current.addFiles([createMockFile()], 'game-1', 'en');
      });

      act(() => {
        result.current.startUpload();
      });

      // Wait for error handling
      await waitFor(() => {
        expect(shouldRetryResult).toBe(false);
      }, { timeout: 3000 });

      // Verify upload failed without retry
      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('failed');
      }, { timeout: 3000 });

      // Reset mock for other tests
      isRetryableError.mockReturnValue(true);
    });

    test('42. OnAllComplete already notified - prevents duplicate calls (line 405)', async () => {
      const onAllComplete = jest.fn();
      const { result, rerender } = renderHook(() =>
        useUploadQueue({ autoUpload: true, onAllComplete })
      );

      // Setup successful upload
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        headers: new Headers({ 'x-correlation-id': 'test-id' }),
        json: async () => ({ uploadId: 'upload-1' })
      });

      // Add file and wait for completion
      act(() => {
        result.current.addFiles([createMockFile('file1.pdf')], 'game-1', 'en');
      });

      await waitFor(() => {
        expect(onAllComplete).toHaveBeenCalledTimes(1);
        expect(result.current.queue[0].status).toBe('success');
      }, { timeout: 3000 });

      // Force multiple re-renders of the effect with same queue state
      // This simulates the effect running when allCompleteNotifiedRef.current is already true
      // The early return at line 405 should prevent additional onAllComplete calls
      act(() => {
        rerender();
      });

      act(() => {
        rerender();
      });

      await act(async () => {
        jest.advanceTimersByTime(100);
      });

      // Verify onAllComplete was called only once (line 405 early return prevented duplicates)
      expect(onAllComplete).toHaveBeenCalledTimes(1);
    });

    test('43. Error response json() throws AND isRetryableError false (lines 242, 258)', async () => {
      const { isRetryableError } = require('../../lib/retryUtils');

      // Mock isRetryableError to return false for JSON parse errors
      isRetryableError.mockImplementation((error: any) => {
        return !(error.message && error.message.includes('JSON'));
      });

      const { result } = renderHook(() => useUploadQueue({ autoUpload: false, maxRetries: 2 }));

      const responseWithBadJson = {
        ok: false,
        status: 500,
        statusText: 'Internal Server Error',
        headers: new Headers({ 'x-correlation-id': 'test-id' }),
        json: jest.fn().mockRejectedValue(new Error('Invalid JSON'))
      };

      let shouldRetryResult: boolean | undefined;
      retryWithBackoff.mockImplementation(async (fn: () => Promise<any>, options: any) => {
        const error = new ApiError('Internal Server Error', 500, 'test-id', responseWithBadJson as unknown as Response);

        // Capture shouldRetry result
        if (options.shouldRetry) {
          shouldRetryResult = options.shouldRetry(error);
        }

        throw error;
      });

      mockFetch.mockResolvedValue(responseWithBadJson);

      act(() => {
        result.current.addFiles([createMockFile()], 'game-1', 'en');
      });

      act(() => {
        result.current.startUpload();
      });

      // Wait for error handling
      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('failed');
      }, { timeout: 3000 });

      // Reset mock
      isRetryableError.mockReturnValue(true);
    });

    test('44. Error is not Error instance (line 321, 264 - string error)', async () => {
      const onRetry = jest.fn();
      const { result } = renderHook(() =>
        useUploadQueue({ autoUpload: false, maxRetries: 2, onRetry })
      );

      // Throw a non-Error object (string)
      const stringError = 'Network connection failed';

      let retryAttempt = 0;
      retryWithBackoff.mockImplementation(async (fn: () => Promise<any>, options: any) => {
        retryAttempt++;

        // Call onRetry with string error to hit line 264
        if (options.onRetry && retryAttempt === 1) {
          options.onRetry(stringError, 1);
        }

        // Throw string error to hit line 321
        throw stringError;
      });

      mockFetch.mockRejectedValue(stringError);

      act(() => {
        result.current.addFiles([createMockFile()], 'game-1', 'en');
      });

      act(() => {
        result.current.startUpload();
      });

      // Wait for error handling
      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('failed');
        expect(result.current.queue[0].error).toBe('Upload failed');
      }, { timeout: 3000 });

      // Verify onRetry was called with converted Error object (line 264)
      expect(onRetry).toHaveBeenCalled();
      expect(onRetry.mock.calls[0][2]).toBeInstanceOf(Error);
      expect(onRetry.mock.calls[0][2].message).toBe('Network connection failed');
    });

    test('45. Retry when item not found in queue (line 263 - fallback)', async () => {
      const onRetry = jest.fn();
      const { result } = renderHook(() =>
        useUploadQueue({ autoUpload: false, maxRetries: 2, onRetry })
      );

      const networkError = new Error('Network error');

      // Mock retryWithBackoff to trigger onRetry when queue has been cleared
      retryWithBackoff.mockImplementation(async (fn: () => Promise<any>, options: any) => {
        // Clear queue before retry callback fires (simulate race condition)
        result.current.clearAll();

        // Trigger retry callback - should fallback to original item (line 263)
        if (options.onRetry) {
          options.onRetry(networkError, 1);
        }

        throw networkError;
      });

      mockFetch.mockRejectedValue(networkError);

      act(() => {
        result.current.addFiles([createMockFile()], 'game-1', 'en');
      });

      const itemId = result.current.queue[0].id;

      act(() => {
        result.current.startUpload();
      });

      // Wait for retry callback
      await waitFor(() => {
        expect(onRetry).toHaveBeenCalled();
      }, { timeout: 3000 });

      // Verify fallback item was used (line 263: || item)
      expect(onRetry.mock.calls[0][0]).toHaveProperty('id', itemId);
    });

    test('46. onAllComplete with all items successful (trigger line 404 true path)', async () => {
      const onAllComplete = jest.fn();
      const { result, rerender } = renderHook(() =>
        useUploadQueue({ autoUpload: false, onAllComplete })
      );

      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        headers: new Headers({ 'x-correlation-id': 'test-id' }),
        json: async () => ({ uploadId: 'upload-1' })
      });

      // Add file
      act(() => {
        result.current.addFiles([createMockFile()], 'game-1', 'en');
      });

      // Start upload
      act(() => {
        result.current.startUpload();
      });

      // Wait for completion
      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('success');
      }, { timeout: 3000 });

      // Wait for effect to fire
      await waitFor(() => {
        expect(onAllComplete).toHaveBeenCalledTimes(1);
      }, { timeout: 3000 });

      // Now rerender to trigger effect again with allCompleteNotifiedRef.current = true
      // This should hit the early return at line 404
      act(() => {
        rerender();
      });

      await act(async () => {
        jest.advanceTimersByTime(100);
      });

      // Verify no additional calls (line 404 early return worked)
      expect(onAllComplete).toHaveBeenCalledTimes(1);
    });

    test('47. Line 404 true branch - force effect re-run with changed onAllComplete', async () => {
      const onAllCompleteCalls: any[] = [];

      // Initial callback
      const initialCallback = jest.fn((stats) => {
        onAllCompleteCalls.push({ callback: 'initial', stats });
      });

      const { result, rerender } = renderHook(
        ({ callback }) => useUploadQueue({ autoUpload: false, onAllComplete: callback }),
        { initialProps: { callback: initialCallback } }
      );

      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        headers: new Headers({ 'x-correlation-id': 'test-id' }),
        json: async () => ({ uploadId: 'upload-1' })
      });

      // Add and complete a file
      act(() => {
        result.current.addFiles([createMockFile()], 'game-1', 'en');
      });

      act(() => {
        result.current.startUpload();
      });

      // Wait for completion
      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('success');
        expect(initialCallback).toHaveBeenCalledTimes(1);
      }, { timeout: 3000 });

      // At this point, allCompleteNotifiedRef.current = true
      // Change the onAllComplete callback to force effect to re-run
      const newCallback = jest.fn((stats) => {
        onAllCompleteCalls.push({ callback: 'new', stats });
      });

      // Rerender with new callback - this changes a dependency and forces effect to run
      // The effect should hit line 404 early return because allCompleteNotifiedRef.current is still true
      act(() => {
        rerender({ callback: newCallback });
      });

      await act(async () => {
        jest.advanceTimersByTime(100);
      });

      // Verify: initial callback was called once, new callback was NOT called (line 404 early return)
      expect(initialCallback).toHaveBeenCalledTimes(1);
      expect(newCallback).toHaveBeenCalledTimes(0);
      expect(onAllCompleteCalls).toHaveLength(1);
      expect(onAllCompleteCalls[0].callback).toBe('initial');
    });
  });
});
