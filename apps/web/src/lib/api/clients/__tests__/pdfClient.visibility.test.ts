/**
 * Tests for PDF Client - setVisibility method (Issue #2309)
 *
 * Coverage gap: setVisibility not tested (lines 58-67)
 * Target: Complete pdfClient coverage to 90%+
 */

import { createPdfClient } from '../pdfClient';
import type { HttpClient } from '../../core/httpClient';

describe('pdfClient - setVisibility (Issue #2309)', () => {
  let mockHttpClient: Mocked<HttpClient>;
  let pdfClient: ReturnType<typeof createPdfClient>;

  beforeEach(() => {
    mockHttpClient = {
      get: vi.fn(),
      post: vi.fn(),
      put: vi.fn(),
      delete: vi.fn(),
      patch: vi.fn(),
      postFile: vi.fn(),
    } as any;

    pdfClient = createPdfClient({ httpClient: mockHttpClient });
  });

  describe('setVisibility', () => {
    it('should set PDF as public successfully', async () => {
      const mockResponse = {
        success: true,
        message: 'PDF visibility updated',
        pdfId: 'pdf-123',
      };

      mockHttpClient.patch.mockResolvedValueOnce(mockResponse);

      const result = await pdfClient.setVisibility('pdf-123', true);

      expect(mockHttpClient.patch).toHaveBeenCalledWith('/api/v1/pdfs/pdf-123/visibility', {
        isPublic: true,
      });
      expect(result.success).toBe(true);
      expect(result.message).toBe('PDF visibility updated');
      expect(result.pdfId).toBe('pdf-123');
    });

    it('should set PDF as private successfully', async () => {
      const mockResponse = {
        success: true,
        message: 'PDF visibility updated',
        pdfId: 'pdf-456',
      };

      mockHttpClient.patch.mockResolvedValueOnce(mockResponse);

      const result = await pdfClient.setVisibility('pdf-456', false);

      expect(mockHttpClient.patch).toHaveBeenCalledWith('/api/v1/pdfs/pdf-456/visibility', {
        isPublic: false,
      });
      expect(result.success).toBe(true);
    });

    it('should handle null response from server', async () => {
      mockHttpClient.patch.mockResolvedValueOnce(null);

      const result = await pdfClient.setVisibility('pdf-789', true);

      expect(result.success).toBe(false);
      expect(result.message).toBe('No response from server');
      expect(result.pdfId).toBeUndefined();
    });

    it('should encode pdfId in URL', async () => {
      mockHttpClient.patch.mockResolvedValueOnce({ success: true, message: 'OK' });

      await pdfClient.setVisibility('pdf with/spaces', true);

      expect(mockHttpClient.patch).toHaveBeenCalledWith(
        expect.stringContaining('pdf%20with%2Fspaces'),
        expect.anything()
      );
    });

    it('should handle UUID format pdfIds', async () => {
      const uuid = '550e8400-e29b-41d4-a716-446655440000';
      mockHttpClient.patch.mockResolvedValueOnce({ success: true, message: 'OK' });

      await pdfClient.setVisibility(uuid, false);

      expect(mockHttpClient.patch).toHaveBeenCalledWith(`/api/v1/pdfs/${uuid}/visibility`, {
        isPublic: false,
      });
    });

    it('should propagate httpClient errors', async () => {
      const error = new Error('Forbidden');
      mockHttpClient.patch.mockRejectedValueOnce(error);

      await expect(pdfClient.setVisibility('pdf-123', true)).rejects.toThrow('Forbidden');
    });

    it('should handle boolean true for isPublic', async () => {
      mockHttpClient.patch.mockResolvedValueOnce({ success: true, message: 'OK' });

      await pdfClient.setVisibility('pdf-123', true);

      expect(mockHttpClient.patch).toHaveBeenCalledWith(expect.anything(), { isPublic: true });
    });

    it('should handle boolean false for isPublic', async () => {
      mockHttpClient.patch.mockResolvedValueOnce({ success: true, message: 'OK' });

      await pdfClient.setVisibility('pdf-123', false);

      expect(mockHttpClient.patch).toHaveBeenCalledWith(expect.anything(), { isPublic: false });
    });

    it('should return success=false on server failure', async () => {
      mockHttpClient.patch.mockResolvedValueOnce({
        success: false,
        message: 'PDF not found',
      });

      const result = await pdfClient.setVisibility('non-existent', true);

      expect(result.success).toBe(false);
      expect(result.message).toBe('PDF not found');
    });

    it('should handle special characters in pdfId', async () => {
      mockHttpClient.patch.mockResolvedValueOnce({ success: true, message: 'OK' });

      await pdfClient.setVisibility('pdf&id=123', true);

      expect(mockHttpClient.patch).toHaveBeenCalledWith(
        expect.stringContaining('pdf%26id%3D123'),
        expect.anything()
      );
    });
  });
});
