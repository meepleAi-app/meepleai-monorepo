/**
 * Tests for Alert Rules API (Issue #2340)
 *
 * Coverage target: 90%+
 * Tests: Alert rules CRUD operations, toggle, test alerts
 */

import type {
  AlertRule,
  CreateAlertRule,
  UpdateAlertRule,
  AlertTemplate,
} from '../schemas/alert-rules.schemas';

// Mock HttpClient before import
const mockGet = vi.fn();
const mockPost = vi.fn();
const mockPut = vi.fn();
const mockDelete = vi.fn();
const mockPatch = vi.fn();

vi.mock('../core/httpClient', () => ({
  HttpClient: vi.fn().mockImplementation(() => ({
    get: mockGet,
    post: mockPost,
    put: mockPut,
    delete: mockDelete,
    patch: mockPatch,
  })),
}));

// Import after mock
const { alertRulesApi } = await import('../alert-rules.api');

describe('alertRulesApi', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('getAll', () => {
    it('should fetch all alert rules', async () => {
      const mockRules: AlertRule[] = [
        {
          id: 'rule-1',
          name: 'High Error Rate',
          isActive: true,
          priority: 'high' as const,
          condition: 'error_rate > 5%',
          channels: ['slack'],
        },
      ];

      mockGet.mockResolvedValueOnce(mockRules);

      const result = await alertRulesApi.getAll();

      expect(mockGet).toHaveBeenCalledWith('/admin/alert-rules');
      expect(result).toEqual(mockRules);
    });

    it('should return empty array on null', async () => {
      mockGet.mockResolvedValueOnce(null);

      const result = await alertRulesApi.getAll();

      expect(result).toEqual([]);
    });
  });

  describe('getById', () => {
    it('should fetch rule by ID', async () => {
      const mockRule: AlertRule = {
        id: 'rule-1',
        name: 'Test Rule',
        isActive: true,
        priority: 'medium' as const,
        condition: 'cpu > 80%',
        channels: ['email'],
      };

      mockGet.mockResolvedValueOnce(mockRule);

      const result = await alertRulesApi.getById('rule-1');

      expect(mockGet).toHaveBeenCalledWith('/admin/alert-rules/rule-1');
      expect(result).toEqual(mockRule);
    });

    it('should return null for missing rule', async () => {
      mockGet.mockResolvedValueOnce(null);

      const result = await alertRulesApi.getById('missing');

      expect(result).toBeNull();
    });
  });

  describe('create', () => {
    it('should create new alert rule', async () => {
      const createData: CreateAlertRule = {
        name: 'New Rule',
        priority: 'high',
        condition: 'disk > 90%',
        channels: ['slack', 'email'],
      };

      const mockResponse = { id: 'rule-123' };
      mockPost.mockResolvedValueOnce(mockResponse);

      const result = await alertRulesApi.create(createData);

      expect(mockPost).toHaveBeenCalledWith('/admin/alert-rules', createData);
      expect(result).toEqual(mockResponse);
    });

    it('should throw on null response', async () => {
      const createData: CreateAlertRule = {
        name: 'New Rule',
        priority: 'low',
        condition: 'test',
        channels: [],
      };

      mockPost.mockResolvedValueOnce(null);

      await expect(alertRulesApi.create(createData)).rejects.toThrow('Failed to create alert rule');
    });
  });

  describe('update', () => {
    it('should update alert rule', async () => {
      const updateData: UpdateAlertRule = {
        name: 'Updated Rule',
        isActive: false,
      };

      mockPut.mockResolvedValueOnce(undefined);

      await alertRulesApi.update('rule-1', updateData);

      expect(mockPut).toHaveBeenCalledWith('/admin/alert-rules/rule-1', updateData);
    });
  });

  describe('delete', () => {
    it('should delete alert rule', async () => {
      mockDelete.mockResolvedValueOnce(undefined);

      await alertRulesApi.delete('rule-1');

      expect(mockDelete).toHaveBeenCalledWith('/admin/alert-rules/rule-1');
    });
  });

  describe('toggle', () => {
    it('should toggle alert rule active state', async () => {
      mockPatch.mockResolvedValueOnce(undefined);

      await alertRulesApi.toggle('rule-1');

      expect(mockPatch).toHaveBeenCalledWith('/admin/alert-rules/rule-1/toggle', {});
    });
  });

  describe('getTemplates', () => {
    it('should fetch alert templates', async () => {
      const mockTemplates: AlertTemplate[] = [
        {
          id: 'template-1',
          name: 'High CPU',
          description: 'CPU usage above threshold',
          defaultCondition: 'cpu > 80%',
          defaultPriority: 'high' as const,
        },
      ];

      mockGet.mockResolvedValueOnce(mockTemplates);

      const result = await alertRulesApi.getTemplates();

      expect(mockGet).toHaveBeenCalledWith('/admin/alert-templates');
      expect(result).toEqual(mockTemplates);
    });

    it('should return empty array on null', async () => {
      mockGet.mockResolvedValueOnce(null);

      const result = await alertRulesApi.getTemplates();

      expect(result).toEqual([]);
    });
  });

  describe('testAlert', () => {
    it('should test alert successfully', async () => {
      const mockResponse = { success: true };
      mockPost.mockResolvedValueOnce(mockResponse);

      const result = await alertRulesApi.testAlert('HighCPU', 'slack');

      expect(mockPost).toHaveBeenCalledWith('/admin/alert-test', {
        alertType: 'HighCPU',
        channel: 'slack',
      });
      expect(result).toEqual(mockResponse);
    });

    it('should return success=false on null response', async () => {
      mockPost.mockResolvedValueOnce(null);

      const result = await alertRulesApi.testAlert('Test', 'email');

      expect(result).toEqual({ success: false });
    });
  });
});
