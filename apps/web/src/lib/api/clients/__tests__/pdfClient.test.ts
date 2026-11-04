/**
 * Comprehensive Tests for PDF Client (Issue #1661 - Fase 1.2)
 *
 * Coverage target: 95%+
 * Tests: Processing progress, cancellation, download URLs, edge cases
 */

import type { Mock } from 'vitest';
import { createPdfClient } from '../pdfClient';
import { HttpClient } from '../../core/httpClient';
import { getApiBase } from '../../core/httpClient';
import { ImageRegionsResponseSchema } from '../../schemas';

vi.mock('../../core/httpClient', async () => ({
  ...(await vi.importActual('../../core/httpClient')),
  getApiBase: vi.fn(),
}));

describe('createPdfClient', () => {
  let mockHttpClient: Mocked<HttpClient>;
  let pdfClient: ReturnType<typeof createPdfClient>;

  beforeEach(() => {
    mockHttpClient = {
      get: vi.fn(),
      post: vi.fn(),
      put: vi.fn(),
      delete: vi.fn(),
      deleteWithResult: vi.fn(),
      postFile: vi.fn(),
    } as any;

    pdfClient = createPdfClient({ httpClient: mockHttpClient });

    (getApiBase as Mock).mockReturnValue('http://localhost:8080');
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  describe('getUploadLimits (#4449)', () => {
    it('reads GET /api/v1/ingest/pdf/limits with a schema that accepts the backend body', async () => {
      mockHttpClient.get.mockResolvedValue(null);

      await pdfClient.getUploadLimits();

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/ingest/pdf/limits',
        expect.any(Object)
      );
      const schema = mockHttpClient.get.mock.calls[0]?.[1] as {
        safeParse: (x: unknown) => { success: boolean };
      };
      // The body GetUploadLimitsQueryHandler returns, as the API serializes it.
      expect(
        schema.safeParse({ maxFileSizeBytes: 104_857_600, maxConcurrentChunkedUploads: 3 }).success
      ).toBe(true);
      expect(
        schema.safeParse({ maxFileSizeBytes: 0, maxConcurrentChunkedUploads: 3 }).success
      ).toBe(false);
    });
  });

  describe('adminDeletePdf (#4683)', () => {
    it('reads DELETE /api/v1/pdf/{id} with the id URL-encoded and a schema for the outcome body', async () => {
      mockHttpClient.deleteWithResult.mockResolvedValue(null);

      await pdfClient.adminDeletePdf('a/b c');

      expect(mockHttpClient.deleteWithResult).toHaveBeenCalledWith(
        '/api/v1/pdf/a%2Fb%20c',
        expect.any(Object)
      );
      expect(mockHttpClient.delete).not.toHaveBeenCalled();
    });

    it('accepts the body DeletePdfCommandHandler sends and nothing else', async () => {
      mockHttpClient.deleteWithResult.mockResolvedValue(null);
      await pdfClient.adminDeletePdf('pdf-1');
      const schema = mockHttpClient.deleteWithResult.mock.calls[0]?.[1] as {
        safeParse: (x: unknown) => { success: boolean };
      };

      for (const outcome of ['unlinked', 'kept']) {
        expect(
          schema.safeParse({ outcome, message: 'ancora in uso', pdfId: 'pdf-1' }).success
        ).toBe(true);
      }
      // The message is only informative (the page shows its own translated text): its absence is fine.
      expect(schema.safeParse({ outcome: 'kept' }).success).toBe(true);
      // 'deleted' is never in a body: it is what a 204 means.
      expect(schema.safeParse({ outcome: 'deleted', message: 'x' }).success).toBe(false);
      expect(schema.safeParse({ message: 'x' }).success).toBe(false);
    });

    it('reports a 204 (no body) as deleted', async () => {
      mockHttpClient.deleteWithResult.mockResolvedValue(null);

      await expect(pdfClient.adminDeletePdf('pdf-1')).resolves.toEqual({ outcome: 'deleted' });
    });

    it.each(['unlinked', 'kept'] as const)('passes a %s outcome through', async outcome => {
      mockHttpClient.deleteWithResult.mockResolvedValue({ outcome, message: 'ancora in uso' });

      await expect(pdfClient.adminDeletePdf('pdf-1')).resolves.toEqual({
        outcome,
        message: 'ancora in uso',
      });
    });

    it('propagates a failed request', async () => {
      mockHttpClient.deleteWithResult.mockRejectedValue(new Error('boom'));

      await expect(pdfClient.adminDeletePdf('pdf-1')).rejects.toThrow('boom');
    });
  });

  describe('getImageRegions', () => {
    it('fetches and unwraps image regions', async () => {
      const regions = [{ page: 4, x: 0.1, y: 0.5, width: 0.8, height: 0.3, elementType: 'Image' }];
      mockHttpClient.get.mockResolvedValueOnce({ regions });

      const result = await pdfClient.getImageRegions('pdf-123');

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/pdf/pdf-123/image-regions',
        ImageRegionsResponseSchema
      );
      expect(result).toEqual(regions);
    });

    it('encodes the pdf id in the request path', async () => {
      mockHttpClient.get.mockResolvedValueOnce({ regions: [] });

      await pdfClient.getImageRegions('pdf/with spaces');

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/pdf/pdf%2Fwith%20spaces/image-regions',
        ImageRegionsResponseSchema
      );
    });

    it('returns null when the endpoint returns null', async () => {
      mockHttpClient.get.mockResolvedValueOnce(null);

      const result = await pdfClient.getImageRegions('pdf-123');

      expect(result).toBeNull();
    });
  });

  describe('getProcessingProgress', () => {
    it('should fetch processing progress', async () => {
      const mockProgress = {
        pdfId: 'pdf-123',
        status: 'processing',
        progress: 0.5,
        currentStep: 'extracting',
        totalSteps: 3,
        completedSteps: 1,
      };

      mockHttpClient.get.mockResolvedValueOnce(mockProgress);

      const result = await pdfClient.getProcessingProgress('pdf-123');

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/pdfs/pdf-123/progress',
        expect.anything()
      );
      expect(result).toEqual(mockProgress);
    });

    it('should return null when progress not found', async () => {
      mockHttpClient.get.mockResolvedValueOnce(null);

      const result = await pdfClient.getProcessingProgress('non-existent');

      expect(result).toBeNull();
    });

    it('should encode pdfId in URL', async () => {
      mockHttpClient.get.mockResolvedValueOnce(null);

      await pdfClient.getProcessingProgress('pdf with/spaces');

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('pdf%20with%2Fspaces'),
        expect.anything()
      );
    });

    it('should handle special characters in pdfId', async () => {
      mockHttpClient.get.mockResolvedValueOnce(null);

      await pdfClient.getProcessingProgress('pdf&id=123');

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('pdf%26id%3D123'),
        expect.anything()
      );
    });

    it('should handle UUID format pdfIds', async () => {
      const uuid = '550e8400-e29b-41d4-a716-446655440000';
      mockHttpClient.get.mockResolvedValueOnce({ pdfId: uuid });

      await pdfClient.getProcessingProgress(uuid);

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        `/api/v1/pdfs/${uuid}/progress`,
        expect.anything()
      );
    });

    it('should handle empty pdfId', async () => {
      mockHttpClient.get.mockResolvedValueOnce(null);

      await pdfClient.getProcessingProgress('');

      expect(mockHttpClient.get).toHaveBeenCalledWith('/api/v1/pdfs//progress', expect.anything());
    });

    it('should propagate httpClient errors', async () => {
      const error = new Error('Network error');
      mockHttpClient.get.mockRejectedValueOnce(error);

      await expect(pdfClient.getProcessingProgress('pdf-123')).rejects.toThrow('Network error');
    });
  });

  describe('cancelProcessing', () => {
    it('should cancel processing', async () => {
      mockHttpClient.delete.mockResolvedValueOnce(undefined);

      await pdfClient.cancelProcessing('pdf-123');

      expect(mockHttpClient.delete).toHaveBeenCalledWith('/api/v1/pdfs/pdf-123/processing');
    });

    it('should return void', async () => {
      mockHttpClient.delete.mockResolvedValueOnce(undefined);

      const result = await pdfClient.cancelProcessing('pdf-123');

      expect(result).toBeUndefined();
    });

    it('should encode pdfId in URL', async () => {
      mockHttpClient.delete.mockResolvedValueOnce(undefined);

      await pdfClient.cancelProcessing('pdf with/spaces');

      expect(mockHttpClient.delete).toHaveBeenCalledWith(
        expect.stringContaining('pdf%20with%2Fspaces')
      );
    });

    it('should handle special characters in pdfId', async () => {
      mockHttpClient.delete.mockResolvedValueOnce(undefined);

      await pdfClient.cancelProcessing('pdf&id=123');

      expect(mockHttpClient.delete).toHaveBeenCalledWith(expect.stringContaining('pdf%26id%3D123'));
    });

    it('should handle UUID format pdfIds', async () => {
      const uuid = '550e8400-e29b-41d4-a716-446655440000';
      mockHttpClient.delete.mockResolvedValueOnce(undefined);

      await pdfClient.cancelProcessing(uuid);

      expect(mockHttpClient.delete).toHaveBeenCalledWith(`/api/v1/pdfs/${uuid}/processing`);
    });

    it('should propagate httpClient errors', async () => {
      const error = new Error('Unauthorized');
      mockHttpClient.delete.mockRejectedValueOnce(error);

      await expect(pdfClient.cancelProcessing('pdf-123')).rejects.toThrow('Unauthorized');
    });

    it('should handle empty pdfId', async () => {
      mockHttpClient.delete.mockResolvedValueOnce(undefined);

      await pdfClient.cancelProcessing('');

      expect(mockHttpClient.delete).toHaveBeenCalledWith('/api/v1/pdfs//processing');
    });
  });

  describe('getPdfDownloadUrl', () => {
    it('should generate download URL', () => {
      const url = pdfClient.getPdfDownloadUrl('pdf-123');

      expect(url).toBe('http://localhost:8080/api/v1/pdfs/pdf-123/download');
    });

    it('should encode pdfId in URL', () => {
      const url = pdfClient.getPdfDownloadUrl('pdf with/spaces');

      expect(url).toContain('pdf%20with%2Fspaces');
    });

    it('should handle special characters in pdfId', () => {
      const url = pdfClient.getPdfDownloadUrl('pdf&id=123');

      expect(url).toContain('pdf%26id%3D123');
    });

    it('should use correct API base URL', () => {
      (getApiBase as Mock).mockReturnValueOnce('https://api.meepleai.dev');

      const url = pdfClient.getPdfDownloadUrl('pdf-123');

      expect(url).toBe('https://api.meepleai.dev/api/v1/pdfs/pdf-123/download');
    });

    it('should handle trailing slash in API base URL', () => {
      (getApiBase as Mock).mockReturnValueOnce('http://localhost:8080/');

      const url = pdfClient.getPdfDownloadUrl('pdf-123');

      // Should handle both with and without trailing slash
      expect(url).toContain('/api/v1/pdfs/pdf-123/download');
    });

    it('should handle UUID format pdfIds', () => {
      const uuid = '550e8400-e29b-41d4-a716-446655440000';

      const url = pdfClient.getPdfDownloadUrl(uuid);

      expect(url).toBe(`http://localhost:8080/api/v1/pdfs/${uuid}/download`);
    });

    it('should handle empty pdfId', () => {
      const url = pdfClient.getPdfDownloadUrl('');

      expect(url).toBe('http://localhost:8080/api/v1/pdfs//download');
    });

    it('should handle numeric pdfIds', () => {
      const url = pdfClient.getPdfDownloadUrl('12345');

      expect(url).toContain('12345');
    });

    it('should return different URLs for different pdfIds', () => {
      const url1 = pdfClient.getPdfDownloadUrl('pdf-1');
      const url2 = pdfClient.getPdfDownloadUrl('pdf-2');

      expect(url1).not.toBe(url2);
      expect(url1).toContain('pdf-1');
      expect(url2).toContain('pdf-2');
    });

    it('should be a synchronous operation', () => {
      const url = pdfClient.getPdfDownloadUrl('pdf-123');

      expect(typeof url).toBe('string');
      expect(url.length).toBeGreaterThan(0);
    });
  });

  describe('Error Handling', () => {
    it('should propagate 404 errors from getProcessingProgress', async () => {
      const error = new Error('PDF not found');
      mockHttpClient.get.mockRejectedValueOnce(error);

      await expect(pdfClient.getProcessingProgress('pdf-123')).rejects.toThrow('PDF not found');
    });

    it('should propagate 401 errors from cancelProcessing', async () => {
      const error = new Error('Unauthorized');
      mockHttpClient.delete.mockRejectedValueOnce(error);

      await expect(pdfClient.cancelProcessing('pdf-123')).rejects.toThrow('Unauthorized');
    });

    it('should propagate timeout errors', async () => {
      const error = new TypeError('Request timeout');
      mockHttpClient.get.mockRejectedValueOnce(error);

      await expect(pdfClient.getProcessingProgress('pdf-123')).rejects.toThrow('Request timeout');
    });
  });

  describe('Integration Tests', () => {
    it('should handle complete progress tracking workflow', async () => {
      const mockProgress1 = { status: 'Processing', percentComplete: 30 };
      const mockProgress2 = { status: 'Processing', percentComplete: 70 };
      const mockProgress3 = { status: 'Completed', percentComplete: 100 };

      mockHttpClient.get
        .mockResolvedValueOnce(mockProgress1)
        .mockResolvedValueOnce(mockProgress2)
        .mockResolvedValueOnce(mockProgress3);

      const result1 = await pdfClient.getProcessingProgress('pdf-123');
      const result2 = await pdfClient.getProcessingProgress('pdf-123');
      const result3 = await pdfClient.getProcessingProgress('pdf-123');

      expect(result1?.percentComplete).toBe(30);
      expect(result2?.percentComplete).toBe(70);
      expect(result3?.percentComplete).toBe(100);
    });

    it('should handle cancel during processing', async () => {
      mockHttpClient.delete.mockResolvedValueOnce(undefined);

      await expect(pdfClient.cancelProcessing('pdf-123')).resolves.toBeUndefined();
      expect(mockHttpClient.delete).toHaveBeenCalledTimes(1);
    });

    it('should generate consistent download URLs for same pdfId', () => {
      const url1 = pdfClient.getPdfDownloadUrl('pdf-123');
      const url2 = pdfClient.getPdfDownloadUrl('pdf-123');

      expect(url1).toBe(url2);
    });
  });

  describe('Edge Cases', () => {
    it('should handle very long pdfIds', async () => {
      const longId = 'pdf-' + 'a'.repeat(1000);
      mockHttpClient.get.mockResolvedValueOnce(null);

      await pdfClient.getProcessingProgress(longId);

      expect(mockHttpClient.get).toHaveBeenCalled();
    });

    it('should handle Unicode in pdfIds', async () => {
      const unicodeId = 'pdf-中文-测试';
      mockHttpClient.get.mockResolvedValueOnce(null);

      await pdfClient.getProcessingProgress(unicodeId);

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining(encodeURIComponent(unicodeId)),
        expect.anything()
      );
    });

    it('should handle concurrent progress checks', async () => {
      mockHttpClient.get.mockResolvedValue({ status: 'processing' });

      const promises = [
        pdfClient.getProcessingProgress('pdf-1'),
        pdfClient.getProcessingProgress('pdf-2'),
        pdfClient.getProcessingProgress('pdf-3'),
      ];

      await Promise.all(promises);

      expect(mockHttpClient.get).toHaveBeenCalledTimes(3);
    });

    it('should handle null response from getProcessingProgress', async () => {
      mockHttpClient.get.mockResolvedValueOnce(null);

      const result = await pdfClient.getProcessingProgress('pdf-123');

      expect(result).toBeNull();
    });
  });
});
