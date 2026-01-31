/**
 * Alerts Client Tests - Issue #2340
 * Coverage target: alerts.ts (57 lines)
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { createAlertsClient } from '../alerts';
import type { HttpClient } from '../../core/httpClient';

const mockHttpClient: HttpClient = {
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  delete: vi.fn(),
  patch: vi.fn(),
} as any;

describe('AlertsClient - Issue #2340', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('getAlerts', () => {
    it('should fetch active alerts by default', async () => {
      const mockAlerts = [{ id: '1', type: 'HighCPU', severity: 'Critical' }];
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockAlerts);

      const client = createAlertsClient({ httpClient: mockHttpClient });
      const result = await client.getAlerts();

      expect(result).toEqual(mockAlerts);
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/admin/alerts?activeOnly=true',
        expect.any(Object)
      );
    });

    it('should fetch all alerts when activeOnly=false', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue([]);

      const client = createAlertsClient({ httpClient: mockHttpClient });
      await client.getAlerts(false);

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/admin/alerts?activeOnly=false',
        expect.any(Object)
      );
    });

    it('should return empty array when get returns null', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const client = createAlertsClient({ httpClient: mockHttpClient });
      const result = await client.getAlerts();

      expect(result).toEqual([]);
    });
  });

  describe('resolveAlert', () => {
    it('should resolve alert by type', async () => {
      const mockResponse = { success: true, alertType: 'HighCPU' };
      vi.mocked(mockHttpClient.post).mockResolvedValue(mockResponse);

      const client = createAlertsClient({ httpClient: mockHttpClient });
      const result = await client.resolveAlert('HighCPU');

      expect(result).toEqual(mockResponse);
      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/admin/alerts/HighCPU/resolve',
        {},
        expect.any(Object)
      );
    });

    it('should encode special characters in alert type', async () => {
      vi.mocked(mockHttpClient.post).mockResolvedValue({ success: true });

      const client = createAlertsClient({ httpClient: mockHttpClient });
      await client.resolveAlert('High CPU Usage');

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/admin/alerts/High%20CPU%20Usage/resolve',
        {},
        expect.any(Object)
      );
    });
  });
});
