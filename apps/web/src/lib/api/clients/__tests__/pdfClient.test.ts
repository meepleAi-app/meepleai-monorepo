/**
 * Comprehensive Tests for PDF Client (Issue #1661 - Fase 1.2)
 *
 * Coverage target: 95%+
 * Tests: Processing progress, cancellation, download URLs, edge cases
 */

import { createPdfClient } from '../pdfClient';
import { HttpClient } from '../../core/httpClient';
import { getApiBase } from '../../core/httpClient';

jest.mock('../../core/httpClient', () => ({
  ...jest.requireActual('../../core/httpClient'),
  getApiBase: jest.fn(),
}));

describe('createPdfClient', () => {
  let mockHttpClient: jest.Mocked<HttpClient>;
  let pdfClient: ReturnType<typeof createPdfClient>;

  beforeEach(() => {
    mockHttpClient = {
      get: jest.fn(),
      post: jest.fn(),
      put: jest.fn(),
      delete: jest.fn(),
      postFile: jest.fn(),
    } as any;

    pdfClient = createPdfClient({ httpClient: mockHttpClient });

    (getApiBase as jest.Mock).mockReturnValue('http://localhost:8080');
  });

  afterEach(() => {
    jest.clearAllMocks();
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
      (getApiBase as jest.Mock).mockReturnValueOnce('https://api.meepleai.dev');

      const url = pdfClient.getPdfDownloadUrl('pdf-123');

      expect(url).toBe('https://api.meepleai.dev/api/v1/pdfs/pdf-123/download');
    });

    it('should handle trailing slash in API base URL', () => {
      (getApiBase as jest.Mock).mockReturnValueOnce('http://localhost:8080/');

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
      const mockProgress1 = { status: 'processing', progress: 0.3 };
      const mockProgress2 = { status: 'processing', progress: 0.7 };
      const mockProgress3 = { status: 'completed', progress: 1.0 };

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
