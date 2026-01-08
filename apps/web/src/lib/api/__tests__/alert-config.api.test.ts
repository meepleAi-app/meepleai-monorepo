/**
 * Tests for Alert Configuration API (Issue #2340)
 *
 * Coverage target: 90%+
 * Tests: Alert config CRUD operations
 */

import type { AlertConfiguration, UpdateAlertConfiguration } from '../schemas/alert-config.schemas';

// Mock HttpClient before import
const mockGet = vi.fn();
const mockPut = vi.fn();

vi.mock('../core/httpClient', () => ({
  HttpClient: vi.fn().mockImplementation(() => ({
    get: mockGet,
    put: mockPut,
  })),
}));

// Import after mock
const { alertConfigApi } = await import('../alert-config.api');

describe('alertConfigApi', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('getAll', () => {
    it('should fetch all alert configurations', async () => {
      const mockConfigs: AlertConfiguration[] = [
        {
          category: 'System',
          isEnabled: true,
          webhookUrl: 'https://hooks.slack.com/test',
          emailRecipients: [],
        },
      ];

      mockGet.mockResolvedValueOnce(mockConfigs);

      const result = await alertConfigApi.getAll();

      expect(mockGet).toHaveBeenCalledWith('/admin/alert-configuration');
      expect(result).toEqual(mockConfigs);
    });

    it('should return empty array on null response', async () => {
      mockGet.mockResolvedValueOnce(null);

      const result = await alertConfigApi.getAll();

      expect(result).toEqual([]);
    });
  });

  describe('getByCategory', () => {
    it('should fetch config by category', async () => {
      const mockConfig: AlertConfiguration = {
        category: 'Security',
        isEnabled: true,
        webhookUrl: null,
        emailRecipients: ['admin@example.com'],
      };

      mockGet.mockResolvedValueOnce(mockConfig);

      const result = await alertConfigApi.getByCategory('Security');

      expect(mockGet).toHaveBeenCalledWith('/admin/alert-configuration/Security');
      expect(result).toEqual(mockConfig);
    });

    it('should return null for missing category', async () => {
      mockGet.mockResolvedValueOnce(null);

      const result = await alertConfigApi.getByCategory('System');

      expect(result).toBeNull();
    });
  });

  describe('update', () => {
    it('should update alert configuration', async () => {
      const updateData: UpdateAlertConfiguration = {
        category: 'System',
        isEnabled: true,
        webhookUrl: 'https://hooks.slack.com/updated',
      };

      const mockResponse = { message: 'Configuration updated' };
      mockPut.mockResolvedValueOnce(mockResponse);

      const result = await alertConfigApi.update(updateData);

      expect(mockPut).toHaveBeenCalledWith('/admin/alert-configuration', updateData);
      expect(result).toEqual(mockResponse);
    });

    it('should throw on null response', async () => {
      const updateData: UpdateAlertConfiguration = {
        category: 'System',
        isEnabled: false,
      };

      mockPut.mockResolvedValueOnce(null);

      await expect(alertConfigApi.update(updateData)).rejects.toThrow(
        'Failed to update alert configuration'
      );
    });
  });
});
