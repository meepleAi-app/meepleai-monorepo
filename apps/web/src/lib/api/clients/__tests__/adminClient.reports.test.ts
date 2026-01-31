/**
 * AdminClient - Reports Methods Tests (Issue #2309)
 *
 * Coverage gap: lines 697-706, 713-717
 * Tests: getReportExecutions, updateReportSchedule
 */

import { createAdminClient } from '../adminClient';
import type { HttpClient } from '../../core/httpClient';

describe('adminClient - Reports (Issue #2309)', () => {
  let mockHttpClient: Mocked<HttpClient>;
  let adminClient: ReturnType<typeof createAdminClient>;

  beforeEach(() => {
    mockHttpClient = {
      get: vi.fn(),
      post: vi.fn(),
      put: vi.fn(),
      delete: vi.fn(),
      patch: vi.fn(),
      postFile: vi.fn(),
    } as any;

    adminClient = createAdminClient({ httpClient: mockHttpClient });
  });

  describe('getReportExecutions', () => {
    it('should get all executions without filters', async () => {
      const mockExecutions = [
        { id: 'exec-1', reportId: 'report-1', executedAt: '2024-01-01', status: 'completed' },
      ];

      mockHttpClient.get.mockResolvedValueOnce(mockExecutions);

      const result = await adminClient.getReportExecutions();

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/admin/reports/executions',
        expect.anything()
      );
      expect(result).toEqual(mockExecutions);
    });

    it('should filter by reportId', async () => {
      mockHttpClient.get.mockResolvedValueOnce([]);

      await adminClient.getReportExecutions({ reportId: 'report-123' });

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/admin/reports/executions?reportId=report-123',
        expect.anything()
      );
    });

    it('should return empty array when null response', async () => {
      mockHttpClient.get.mockResolvedValueOnce(null);

      const result = await adminClient.getReportExecutions();

      expect(result).toEqual([]);
    });

    it('should handle empty reportId parameter', async () => {
      mockHttpClient.get.mockResolvedValueOnce([]);

      await adminClient.getReportExecutions({});

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/admin/reports/executions',
        expect.anything()
      );
    });
  });

  describe('updateReportSchedule', () => {
    it('should update report schedule', async () => {
      mockHttpClient.patch.mockResolvedValueOnce(undefined);

      await adminClient.updateReportSchedule('report-123', { cronExpression: '0 0 * * *' });

      expect(mockHttpClient.patch).toHaveBeenCalledWith(
        '/api/v1/admin/reports/report-123/schedule',
        {
          cronExpression: '0 0 * * *',
        }
      );
    });

    it('should handle schedule with multiple parameters', async () => {
      mockHttpClient.patch.mockResolvedValueOnce(undefined);

      await adminClient.updateReportSchedule('report-456', {
        cronExpression: '0 12 * * 1-5',
        enabled: true,
        timezone: 'Europe/Rome',
      });

      expect(mockHttpClient.patch).toHaveBeenCalledWith(
        '/api/v1/admin/reports/report-456/schedule',
        {
          cronExpression: '0 12 * * 1-5',
          enabled: true,
          timezone: 'Europe/Rome',
        }
      );
    });
  });
});
