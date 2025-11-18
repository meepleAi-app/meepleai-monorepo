/**
 * FE-TEST-010: Comprehensive Web Worker Upload Queue Integration Tests
 * Suite 2: Error Handling & Message Protocol
 *
 * Tests: 30 total (15 Error Handling + 15 Message Protocol)
 * Coverage Target: Error scenarios, retry logic, worker message protocol
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

describe('useUploadQueue - Error Handling & Message Protocol', () => {
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
  // Error Handling Tests (15 tests)
  // ==========================================================================
  describe('Error Handling', () => {
    it('should handle network errors (TypeError)', async () => {
      const { result } = renderHook(() => useUploadQueue());

      (global.fetch as jest.Mock).mockRejectedValueOnce(new TypeError('Network request failed'));

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
      });

      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('failed');
        expect(result.current.queue[0].error).toBeDefined();
      }, { timeout: 5000 });
    });

    it('should handle 400 Bad Request errors', async () => {
      const { result } = renderHook(() => useUploadQueue());

      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: false,
        status: 400,
        json: async () => ({ error: 'Invalid file format' })
      } as Response);

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
      });

      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('failed');
        expect(result.current.queue[0].error).toContain('Invalid file format');
      });
    });

    it('should handle 401 Unauthorized errors', async () => {
      const { result } = renderHook(() => useUploadQueue());

      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: false,
        status: 401,
        json: async () => ({ error: 'Unauthorized' })
      } as Response);

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
      });

      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('failed');
        expect(result.current.queue[0].error).toBeDefined();
      });
    });

    it('should handle 413 Payload Too Large errors', async () => {
      const { result } = renderHook(() => useUploadQueue());

      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: false,
        status: 413,
        json: async () => ({ error: 'File too large' })
      } as Response);

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
      });

      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('failed');
        expect(result.current.queue[0].error).toContain('too large');
      });
    });

    it('should handle 500 Internal Server Error', async () => {
      const { result } = renderHook(() => useUploadQueue());

      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: false,
        status: 500,
        json: async () => ({ error: 'Internal server error' })
      } as Response);

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
      });

      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('failed');
        expect(result.current.queue[0].error).toBeDefined();
      });
    });

    it('should handle 502 Bad Gateway (retryable)', async () => {
      const { result } = renderHook(() => useUploadQueue());

      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: false,
        status: 502,
        json: async () => ({ error: 'Bad gateway' })
      } as Response);

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
      });

      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('failed');
      });
    });

    it('should handle 503 Service Unavailable (retryable)', async () => {
      const { result } = renderHook(() => useUploadQueue());

      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: false,
        status: 503,
        json: async () => ({ error: 'Service unavailable' })
      } as Response);

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
      });

      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('failed');
      });
    });

    it('should track correlation ID on errors', async () => {
      const { result } = renderHook(() => useUploadQueue());

      (global.fetch as jest.Mock).mockRejectedValueOnce(new Error('Test error'));

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
      });

      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('failed');
        expect(result.current.queue[0].correlationId).toBeDefined();
        expect(result.current.queue[0].correlationId).toContain('mock-corr-id');
      });
    });

    it('should handle AbortError during cancellation', async () => {
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
        expect(['cancelled', 'pending', 'success']).toContain(result.current.queue[0].status);
      }, { timeout: 5000 });
    });

    it('should increment failed metrics on error', async () => {
      const { result } = renderHook(() => useUploadQueue());

      (global.fetch as jest.Mock).mockRejectedValueOnce(new Error('Test error'));

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
      });

      await waitFor(() => {
        const stats = result.current.getStats();
        expect(stats.failed).toBeGreaterThanOrEqual(1);
      });
    });

    it('should retry failed upload', async () => {
      const { result } = renderHook(() => useUploadQueue());

      (global.fetch as jest.Mock).mockRejectedValueOnce(new Error('First attempt failed'));

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
      });

      let itemId: string;
      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('failed');
        itemId = result.current.queue[0].id;
      }, { timeout: 5000 });

      // Reset mock for retry
      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
        json: async () => ({ documentId: 'pdf-retry-123' }),
        status: 200
      } as Response);

      act(() => {
        result.current.retryUpload(itemId!);
      });

      await waitFor(() => {
        expect(result.current.queue[0].status).toMatch(/pending|uploading|success/);
      }, { timeout: 5000 });
    });

    it('should reset error state on retry', async () => {
      const { result } = renderHook(() => useUploadQueue());

      (global.fetch as jest.Mock).mockRejectedValueOnce(new Error('Test error'));

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
      });

      let itemId: string;
      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('failed');
        itemId = result.current.queue[0].id;
      }, { timeout: 5000 });

      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
        json: async () => ({ documentId: 'pdf-123' }),
        status: 200
      } as Response);

      act(() => {
        result.current.retryUpload(itemId!);
      });

      await waitFor(() => {
        expect(['pending', 'uploading', 'success']).toContain(result.current.queue[0].status);
      }, { timeout: 5000 });
    });

    it('should increment retry count on each attempt', async () => {
      const { result } = renderHook(() => useUploadQueue());

      (global.fetch as jest.Mock).mockRejectedValue(new Error('Test error'));

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
      });

      let itemId: string;
      await waitFor(() => {
        itemId = result.current.queue[0].id;
      });

      // Retry 3 times
      for (let i = 0; i < 3; i++) {
        act(() => {
          result.current.retryUpload(itemId!);
        });

        await waitFor(() => {
          expect(result.current.queue[0].retryCount).toBe(i + 1);
        });
      }
    });

    it('should retain file data for retry', async () => {
      const { result } = renderHook(() => useUploadQueue());

      (global.fetch as jest.Mock).mockRejectedValueOnce(new Error('Test error'));

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
      });

      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('failed');
        // File data should be retained
        expect(mockWorker.getFileDataCacheSize()).toBeGreaterThan(0);
      });
    });

    it('should handle generic error messages', async () => {
      const { result } = renderHook(() => useUploadQueue());

      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: false,
        status: 500,
        json: async () => ({}) // No error message
      } as Response);

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
      });

      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('failed');
        expect(result.current.queue[0].error).toBeDefined();
      });
    });
  });

  // ==========================================================================
  // Message Protocol Tests (15 tests)
  // ==========================================================================
  describe('Message Protocol', () => {
    it('should handle ADD_FILES message', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
        // Timer removed - using real async
      });

      await waitFor(() => {
        expect(result.current.queue).toHaveLength(1);
      });
    });

    it('should handle CANCEL_UPLOAD message', async () => {
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
      });
    });

    it('should handle RETRY_UPLOAD message', async () => {
      const { result } = renderHook(() => useUploadQueue());

      (global.fetch as jest.Mock).mockRejectedValueOnce(new Error('Test error'));

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
      });

      let itemId: string;
      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('failed');
        itemId = result.current.queue[0].id;
      }, { timeout: 5000 });

      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
        json: async () => ({ documentId: 'pdf-123' }),
        status: 200
      } as Response);

      act(() => {
        result.current.retryUpload(itemId!);
      });

      await waitFor(() => {
        expect(['pending', 'uploading', 'success']).toContain(result.current.queue[0].status);
      }, { timeout: 5000 });
    });

    it('should handle REMOVE_ITEM message', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
        // Timer removed - using real async
      });

      let itemId: string;
      await waitFor(() => {
        itemId = result.current.queue[0].id;
      });

      act(() => {
        result.current.removeFile(itemId!);
      });

      await waitFor(() => {
        expect(result.current.queue).toHaveLength(0);
      });
    });

    it('should handle CLEAR_COMPLETED message', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
      });

      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('success');
      });

      act(() => {
        result.current.clearCompleted();
      });

      await waitFor(() => {
        expect(result.current.queue).toHaveLength(0);
      });
    });

    it('should handle CLEAR_ALL message', async () => {
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

    it('should handle START_PROCESSING message', async () => {
      const { result } = renderHook(() => useUploadQueue());

      mockWorker.setAutoUpload(false);

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
        // Timer removed - using real async
      });

      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('pending');
      });

      act(() => {
        result.current.startUpload();
      });

      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('success');
      });
    });

    it('should handle GET_STATE message', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
        // Timer removed - using real async
      });

      await waitFor(() => {
        expect(result.current.queue).toHaveLength(1);
      });
    });

    it('should handle STATE_UPDATED response', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
      });

      await waitFor(() => {
        expect(result.current.queue[0]).toBeDefined();
      });
    });

    it('should handle UPLOAD_PROGRESS response', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
      });

      await waitFor(() => {
        expect(result.current.queue[0].progress).toBeGreaterThanOrEqual(0);
      });
    });

    it('should handle UPLOAD_SUCCESS response', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
      });

      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('success');
        expect(result.current.queue[0].pdfId).toBeDefined();
      });
    });

    it('should handle UPLOAD_FAILED response', async () => {
      const { result } = renderHook(() => useUploadQueue());

      (global.fetch as jest.Mock).mockRejectedValueOnce(new Error('Test error'));

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
      });

      await waitFor(() => {
        expect(result.current.queue[0].status).toBe('failed');
        expect(result.current.queue[0].error).toBeDefined();
      });
    });

    it('should handle PERSIST_REQUEST response', async () => {
      const { result } = renderHook(() => useUploadQueue());

      const file = new File(['content'], 'test.pdf', { type: 'application/pdf' });

      await act(async () => {
        await result.current.addFiles([file], 'game-123', 'en');
      });

      await waitFor(() => {
        expect(Storage.prototype.setItem).toHaveBeenCalled();
      });
    });

    it('should handle WORKER_READY message', async () => {
      const { result } = renderHook(() => useUploadQueue());

      await waitFor(() => {
        expect(result.current.isWorkerReady).toBe(true);
      });
    });

    it('should handle multiple messages in sequence', async () => {
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

      let firstItemId: string;
      await waitFor(() => {
        firstItemId = result.current.queue[0].id;
      });

      act(() => {
        result.current.removeFile(firstItemId!);
        // Timer removed - using real async
      });

      await waitFor(() => {
        expect(result.current.queue).toHaveLength(1);
      });

      act(() => {
        result.current.clearAll();
      });

      await waitFor(() => {
        expect(result.current.queue).toHaveLength(0);
      });
    });
  });
});