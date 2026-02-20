/**
 * Queue API Functions Tests (Issue #4737)
 *
 * Tests for queue API client functions:
 * - cancelJob, retryJob, removeJob, reorderQueue, enqueuePdf
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';

const { mockGet, mockPost, mockPut, mockDelete } = vi.hoisted(() => ({
  mockGet: vi.fn(),
  mockPost: vi.fn(),
  mockPut: vi.fn(),
  mockDelete: vi.fn(),
}));

vi.mock('@/lib/api/client', () => ({
  apiClient: {
    get: mockGet,
    post: mockPost,
    put: mockPut,
    delete: mockDelete,
  },
}));

import {
  cancelJob,
  retryJob,
  removeJob,
  reorderQueue,
  enqueuePdf,
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
});
