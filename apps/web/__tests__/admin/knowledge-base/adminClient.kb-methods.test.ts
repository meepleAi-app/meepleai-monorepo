/**
 * Admin Client - Knowledge Base Methods Tests (Issue #4788)
 *
 * Tests for 10 new adminClient methods added in Issue #4784/#4787:
 * - bulkDeletePdfs, reindexPdf, purgeStaleDocuments, cleanupOrphans
 * - getPdfStatusDistribution, getPdfStorageHealth, getPdfMetrics
 * - getVectorCollections, getProcessingQueueAdmin, getAllPdfs
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';

import { createAdminClient } from '@/lib/api/clients/adminClient';
import type { HttpClient } from '@/lib/api/core/httpClient';

const mockHttpClient: HttpClient = {
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  delete: vi.fn(),
  patch: vi.fn(),
  baseUrl: 'http://localhost:8080',
} as any;

describe('AdminClient - Knowledge Base Methods (Issue #4784)', () => {
  let client: ReturnType<typeof createAdminClient>;

  beforeEach(() => {
    vi.clearAllMocks();
    client = createAdminClient({ httpClient: mockHttpClient });
  });

  // ========== PDF Management ==========

  describe('bulkDeletePdfs', () => {
    it('should POST to bulk delete endpoint with pdfIds', async () => {
      const mockResult = { totalRequested: 2, successCount: 2, failedCount: 0, items: [] };
      vi.mocked(mockHttpClient.post).mockResolvedValue(mockResult);

      const result = await client.bulkDeletePdfs(['pdf-1', 'pdf-2']);

      expect(result).toEqual(mockResult);
      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/admin/pdfs/bulk/delete',
        { pdfIds: ['pdf-1', 'pdf-2'] },
        expect.any(Object)
      );
    });
  });

  describe('reindexPdf', () => {
    it('should POST to reindex endpoint with encoded pdfId', async () => {
      const mockResult = { success: true, message: 'Reindex queued' };
      vi.mocked(mockHttpClient.post).mockResolvedValue(mockResult);

      const result = await client.reindexPdf('pdf-123');

      expect(result).toEqual(mockResult);
      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/admin/pdfs/pdf-123/reindex',
        {},
        expect.any(Object)
      );
    });

    it('should URL-encode special characters in pdfId', async () => {
      vi.mocked(mockHttpClient.post).mockResolvedValue({ success: true, message: 'ok' });

      await client.reindexPdf('pdf with spaces');

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/admin/pdfs/pdf%20with%20spaces/reindex',
        {},
        expect.any(Object)
      );
    });
  });

  describe('purgeStaleDocuments', () => {
    it('should POST to purge-stale endpoint', async () => {
      const mockResult = { affected: 3, message: '3 documents marked as failed' };
      vi.mocked(mockHttpClient.post).mockResolvedValue(mockResult);

      const result = await client.purgeStaleDocuments();

      expect(result).toEqual(mockResult);
      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/admin/pdfs/maintenance/purge-stale',
        {},
        expect.any(Object)
      );
    });
  });

  describe('cleanupOrphans', () => {
    it('should POST to cleanup-orphans endpoint', async () => {
      const mockResult = { affected: 5, message: '5 orphaned chunks removed' };
      vi.mocked(mockHttpClient.post).mockResolvedValue(mockResult);

      const result = await client.cleanupOrphans();

      expect(result).toEqual(mockResult);
      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/admin/pdfs/maintenance/cleanup-orphans',
        {},
        expect.any(Object)
      );
    });
  });

  // ========== PDF Analytics ==========

  describe('getPdfStatusDistribution', () => {
    it('should GET status distribution', async () => {
      const mockResult = {
        countByState: { Completed: 10, Processing: 3, Failed: 1 },
        totalDocuments: 14,
        topBySize: [{ id: 'pdf-1', fileName: 'big.pdf', fileSizeBytes: 5000000 }],
      };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockResult);

      const result = await client.getPdfStatusDistribution();

      expect(result).toEqual(mockResult);
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/admin/pdfs/analytics/distribution',
        expect.any(Object)
      );
    });

    it('should return default when API returns null', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const result = await client.getPdfStatusDistribution();

      expect(result).toEqual({ countByState: {}, totalDocuments: 0, topBySize: [] });
    });
  });

  describe('getPdfStorageHealth', () => {
    it('should GET storage health', async () => {
      const mockResult = {
        postgres: { totalDocuments: 100, totalChunks: 5000, estimatedChunksSizeMB: 250 },
        qdrant: { vectorCount: 5000, memoryBytes: 1073741824, memoryFormatted: '1 GB', isAvailable: true },
        fileStorage: { totalFiles: 100, totalSizeBytes: 524288000, totalSizeFormatted: '500 MB', sizeByState: {} },
        overallHealth: 'Healthy',
        measuredAt: '2026-02-19T00:00:00Z',
      };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockResult);

      const result = await client.getPdfStorageHealth();

      expect(result).toEqual(mockResult);
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/admin/pdfs/storage/health',
        expect.any(Object)
      );
    });

    it('should throw when API returns null', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      await expect(client.getPdfStorageHealth()).rejects.toThrow('Failed to fetch PDF storage health');
    });
  });

  describe('getPdfMetrics', () => {
    it('should GET processing metrics', async () => {
      const mockResult = {
        averages: { extraction: { step: 'extraction', avgDuration: 12.5, sampleSize: 50 } },
        percentiles: { extraction: { p50: 10, p95: 25, p99: 35 } },
        lastUpdated: '2026-02-19T00:00:00Z',
      };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockResult);

      const result = await client.getPdfMetrics();

      expect(result).toEqual(mockResult);
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/admin/pdfs/metrics/processing',
        expect.any(Object)
      );
    });
  });

  // ========== Knowledge Base ==========

  describe('getVectorCollections', () => {
    it('should GET vector collections', async () => {
      const mockResult = {
        collections: [
          { name: 'Game Rules', vectorCount: 42500, dimensions: 384, storage: '3.2 GB', health: 98 },
        ],
      };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockResult);

      const result = await client.getVectorCollections();

      expect(result).toEqual(mockResult);
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/admin/kb/vector-collections',
        expect.any(Object)
      );
    });

    it('should return empty collections when API returns null', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const result = await client.getVectorCollections();

      expect(result).toEqual({ collections: [] });
    });
  });

  describe('getProcessingQueueAdmin', () => {
    it('should GET processing queue with default params', async () => {
      const mockResult = { jobs: [], total: 0, page: 1, pageSize: 20, totalPages: 0 };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockResult);

      const result = await client.getProcessingQueueAdmin();

      expect(result).toEqual(mockResult);
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/admin/kb/processing-queue',
        expect.any(Object)
      );
    });

    it('should append query params when provided', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue({ jobs: [], total: 0, page: 2, pageSize: 10, totalPages: 0 });

      await client.getProcessingQueueAdmin({
        statusFilter: 'Processing',
        page: 2,
        pageSize: 10,
      });

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('statusFilter=Processing'),
        expect.any(Object)
      );
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('page=2'),
        expect.any(Object)
      );
    });

    it('should return default when API returns null', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const result = await client.getProcessingQueueAdmin();

      expect(result).toEqual({ jobs: [], total: 0, page: 1, pageSize: 20, totalPages: 0 });
    });
  });

  describe('getAllPdfs', () => {
    it('should GET all PDFs with default params', async () => {
      const mockResult = { items: [], total: 0, page: 1, pageSize: 50 };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockResult);

      const result = await client.getAllPdfs();

      expect(result).toEqual(mockResult);
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/admin/pdfs',
        expect.any(Object)
      );
    });

    it('should append filter params when provided', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 20 });

      await client.getAllPdfs({
        state: 'Completed',
        sortBy: 'uploadedAt',
        sortOrder: 'desc',
        page: 1,
        pageSize: 20,
      });

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('state=Completed'),
        expect.any(Object)
      );
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('sortBy=uploadedAt'),
        expect.any(Object)
      );
    });

    it('should return default when API returns null', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const result = await client.getAllPdfs();

      expect(result).toEqual({ items: [], total: 0, page: 1, pageSize: 50 });
    });
  });
});
