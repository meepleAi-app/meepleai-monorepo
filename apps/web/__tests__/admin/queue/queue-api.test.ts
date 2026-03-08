/**
 * Queue API Functions Tests (Issue #4737)
 *
 * Tests for queue API client functions:
 * - cancelJob, retryJob, removeJob, reorderQueue, enqueuePdf
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';

const { mockGet, mockPost, mockPut, mockDelete, mockPatch } = vi.hoisted(() => ({
  mockGet: vi.fn(),
  mockPost: vi.fn(),
  mockPut: vi.fn(),
  mockDelete: vi.fn(),
  mockPatch: vi.fn(),
}));

vi.mock('@/lib/api/client', () => ({
  apiClient: {
    get: mockGet,
    post: mockPost,
    put: mockPut,
    delete: mockDelete,
    patch: mockPatch,
  },
}));

import {
  cancelJob,
  retryJob,
  removeJob,
  reorderQueue,
  enqueuePdf,
  bumpPriority,
  getQueueConfig,
  updateQueueConfig,
  bulkReindexFailed,
  getExtractedText,
  getQueueStatus,
} from '@/app/admin/(dashboard)/knowledge-base/queue/lib/queue-api';

describe('Queue API Functions', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('cancelJob', () => {
    it('should POST to cancel endpoint', async () => {
      mockPost.mockResolvedValue(undefined);
      await cancelJob('job-123');
      expect(mockPost).toHaveBeenCalledWith('/api/v1/admin/queue/job-123/cancel');
    });
  });

  describe('retryJob', () => {
    it('should POST to retry endpoint', async () => {
      mockPost.mockResolvedValue(undefined);
      await retryJob('job-456');
      expect(mockPost).toHaveBeenCalledWith('/api/v1/admin/queue/job-456/retry');
    });
  });

  describe('removeJob', () => {
    it('should DELETE the job', async () => {
      mockDelete.mockResolvedValue(undefined);
      await removeJob('job-789');
      expect(mockDelete).toHaveBeenCalledWith('/api/v1/admin/queue/job-789');
    });
  });

  describe('reorderQueue', () => {
    it('should PUT ordered job IDs', async () => {
      mockPut.mockResolvedValue(undefined);
      await reorderQueue(['a', 'b', 'c']);
      expect(mockPut).toHaveBeenCalledWith('/api/v1/admin/queue/reorder', {
        orderedJobIds: ['a', 'b', 'c'],
      });
    });
  });

  describe('enqueuePdf', () => {
    it('should POST with pdfDocumentId and default priority', async () => {
      mockPost.mockResolvedValue({ jobId: 'new-job' });
      const result = await enqueuePdf('pdf-doc-1');
      expect(mockPost).toHaveBeenCalledWith('/api/v1/admin/queue/enqueue', {
        pdfDocumentId: 'pdf-doc-1',
        priority: 0,
      });
      expect(result).toEqual({ jobId: 'new-job' });
    });

    it('should POST with custom priority', async () => {
      mockPost.mockResolvedValue({ jobId: 'new-job' });
      await enqueuePdf('pdf-doc-2', 10);
      expect(mockPost).toHaveBeenCalledWith('/api/v1/admin/queue/enqueue', {
        pdfDocumentId: 'pdf-doc-2',
        priority: 10,
      });
    });
  });

  // Issue #5458: New API functions for queue management
  describe('bumpPriority', () => {
    it('should PATCH priority endpoint with new priority', async () => {
      mockPatch.mockResolvedValue(undefined);
      await bumpPriority('job-123', 'Urgent');
      expect(mockPatch).toHaveBeenCalledWith('/api/v1/admin/queue/job-123/priority', {
        newPriority: 'Urgent',
      });
    });
  });

  describe('getQueueConfig', () => {
    it('should GET queue config', async () => {
      const config = {
        isPaused: false,
        maxConcurrentWorkers: 3,
        updatedAt: '2026-01-01',
        updatedBy: null,
      };
      mockGet.mockResolvedValue(config);
      const result = await getQueueConfig();
      expect(mockGet).toHaveBeenCalledWith('/api/v1/admin/queue/config');
      expect(result).toEqual(config);
    });
  });

  describe('updateQueueConfig', () => {
    it('should PATCH config with isPaused', async () => {
      mockPatch.mockResolvedValue(undefined);
      await updateQueueConfig(true);
      expect(mockPatch).toHaveBeenCalledWith('/api/v1/admin/queue/config', {
        isPaused: true,
        maxConcurrentWorkers: undefined,
      });
    });

    it('should PATCH config with maxConcurrentWorkers', async () => {
      mockPatch.mockResolvedValue(undefined);
      await updateQueueConfig(undefined, 5);
      expect(mockPatch).toHaveBeenCalledWith('/api/v1/admin/queue/config', {
        isPaused: undefined,
        maxConcurrentWorkers: 5,
      });
    });
  });

  describe('bulkReindexFailed', () => {
    it('should POST to reindex-failed endpoint', async () => {
      const result = { enqueuedCount: 3, skippedCount: 1, errors: [] };
      mockPost.mockResolvedValue(result);
      const response = await bulkReindexFailed();
      expect(mockPost).toHaveBeenCalledWith('/api/v1/admin/queue/reindex-failed');
      expect(response).toEqual(result);
    });
  });

  describe('getExtractedText', () => {
    it('should GET extracted text for a document', async () => {
      const textResult = {
        id: 'doc-1',
        fileName: 'rules.pdf',
        extractedText: 'Game rules...',
        processingStatus: 'Completed',
        processedAt: '2026-01-01',
        pageCount: 5,
        characterCount: 1200,
        processingError: null,
      };
      mockGet.mockResolvedValue(textResult);
      const result = await getExtractedText('doc-1');
      expect(mockGet).toHaveBeenCalledWith('/api/v1/admin/queue/documents/doc-1/extracted-text');
      expect(result).toEqual(textResult);
    });

    it('should return null for not found document', async () => {
      mockGet.mockResolvedValue(null);
      const result = await getExtractedText('doc-missing');
      expect(result).toBeNull();
    });
  });

  describe('getQueueStatus', () => {
    it('should GET queue status with backpressure info', async () => {
      const status = {
        queueDepth: 42,
        backpressureThreshold: 50,
        isUnderPressure: false,
        isPaused: false,
        maxConcurrentWorkers: 3,
        estimatedWaitMinutes: 14.0,
      };
      mockGet.mockResolvedValue(status);
      const result = await getQueueStatus();
      expect(mockGet).toHaveBeenCalledWith('/api/v1/admin/queue/status');
      expect(result).toEqual(status);
    });
  });
});
