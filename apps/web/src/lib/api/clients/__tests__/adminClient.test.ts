/**
 * Administration Client Tests - Issue #2340
 * Coverage: 40 tests spanning user management, prompts, stats, workflows, reports, and testing metrics
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { createAdminClient } from '../adminClient';
import type { HttpClient } from '../../core/httpClient';

const mockHttpClient: HttpClient = {
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  delete: vi.fn(),
  patch: vi.fn(),
  baseUrl: 'http://localhost:8080',
} as any;

describe('AdminClient - Issue #2340', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // ========== User Management Tests ==========

  describe('createUser', () => {
    it('should create new admin user', async () => {
      const mockUser = { id: 'user_123', email: 'admin@example.com', role: 'Admin' };
      const mockResponse = { user: mockUser };
      vi.mocked(mockHttpClient.post).mockResolvedValue(mockResponse);

      const client = createAdminClient({ httpClient: mockHttpClient });
      const result = await client.createUser({
        email: 'admin@example.com',
        password: 'SecurePass123!',
        role: 'Admin',
      });

      expect(result).toEqual(mockUser);
      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/admin/users',
        expect.objectContaining({ email: 'admin@example.com' }),
        expect.any(Object)
      );
    });

    it('should create user with different roles', async () => {
      const mockUser = { id: 'user_456', email: 'moderator@example.com', role: 'Moderator' };
      vi.mocked(mockHttpClient.post).mockResolvedValue({ user: mockUser });

      const client = createAdminClient({ httpClient: mockHttpClient });
      const result = await client.createUser({
        email: 'moderator@example.com',
        password: 'Password123!',
        role: 'Moderator',
      });

      expect(result.role).toBe('Moderator');
    });
  });

  describe('updateUser', () => {
    it('should update existing user', async () => {
      vi.mocked(mockHttpClient.put).mockResolvedValue(undefined);

      const client = createAdminClient({ httpClient: mockHttpClient });
      await client.updateUser('user_123', {
        email: 'newemail@example.com',
        role: 'User',
      });

      expect(mockHttpClient.put).toHaveBeenCalledWith(
        '/api/v1/admin/users/user_123',
        expect.objectContaining({ email: 'newemail@example.com' })
      );
    });
  });

  describe('deleteUser', () => {
    it('should delete user by ID', async () => {
      vi.mocked(mockHttpClient.delete).mockResolvedValue(undefined);

      const client = createAdminClient({ httpClient: mockHttpClient });
      await client.deleteUser('user_123');

      expect(mockHttpClient.delete).toHaveBeenCalledWith('/api/v1/admin/users/user_123');
    });
  });

  describe('getAllUsers', () => {
    it('should fetch all users with pagination', async () => {
      const mockResult = {
        users: [
          { id: '1', email: 'user1@example.com', role: 'Admin' },
          { id: '2', email: 'user2@example.com', role: 'User' },
        ],
        total: 2,
      };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockResult);

      const client = createAdminClient({ httpClient: mockHttpClient });
      const result = await client.getAllUsers({ page: 1, limit: 20 });

      expect(result.users).toHaveLength(2);
      expect(result.total).toBe(2);
    });

    it('should filter users by search term', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue({ users: [], total: 0 });

      const client = createAdminClient({ httpClient: mockHttpClient });
      await client.getAllUsers({ search: 'john' });

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('search=john'),
        expect.any(Object)
      );
    });

    it('should return empty result when no users found', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const client = createAdminClient({ httpClient: mockHttpClient });
      const result = await client.getAllUsers();

      expect(result.users).toEqual([]);
      expect(result.total).toBe(0);
    });
  });

  describe('exportUsersToCSV', () => {
    it('should export users to CSV file', async () => {
      const mockBlob = new Blob(['csv,data'], { type: 'text/csv' });
      global.fetch = vi.fn().mockResolvedValue({
        ok: true,
        blob: vi.fn().mockResolvedValue(mockBlob),
      });

      const client = createAdminClient({ httpClient: mockHttpClient });
      const result = await client.exportUsersToCSV();

      expect(result).toBeInstanceOf(Blob);
    });
  });

  describe('importUsersFromCSV', () => {
    it('should import users from CSV content', async () => {
      const mockResult = { successCount: 5, failureCount: 0, errors: [] };
      vi.mocked(mockHttpClient.post).mockResolvedValue(mockResult);

      const client = createAdminClient({ httpClient: mockHttpClient });
      const result = await client.importUsersFromCSV('email,role\nuser@test.com,User');

      expect(result.successCount).toBe(5);
      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/admin/users/bulk/import',
        expect.any(String),
        expect.any(Object),
        expect.objectContaining({
          headers: { 'Content-Type': 'text/csv' },
        })
      );
    });
  });

  // ========== Prompt Template Management Tests ==========

  describe('createPrompt', () => {
    it('should create new prompt template', async () => {
      const mockTemplate = { id: 'prompt_123', name: 'Game Rules', content: 'How do you play?' };
      vi.mocked(mockHttpClient.post).mockResolvedValue({ template: mockTemplate });

      const client = createAdminClient({ httpClient: mockHttpClient });
      const result = await client.createPrompt({
        name: 'Game Rules',
        content: 'How do you play?',
        category: 'General',
      });

      expect(result).toEqual(mockTemplate);
      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/admin/prompts',
        expect.objectContaining({ name: 'Game Rules' }),
        expect.any(Object)
      );
    });
  });

  describe('updatePrompt', () => {
    it('should update existing prompt template', async () => {
      const mockTemplate = { id: 'prompt_123', name: 'Updated Game Rules' };
      vi.mocked(mockHttpClient.put).mockResolvedValue({ template: mockTemplate });

      const client = createAdminClient({ httpClient: mockHttpClient });
      const result = await client.updatePrompt('prompt_123', {
        name: 'Updated Game Rules',
      });

      expect(result.name).toBe('Updated Game Rules');
      expect(mockHttpClient.put).toHaveBeenCalledWith(
        '/api/v1/admin/prompts/prompt_123',
        expect.objectContaining({ name: 'Updated Game Rules' }),
        expect.any(Object)
      );
    });
  });

  describe('deletePrompt', () => {
    it('should delete prompt template', async () => {
      vi.mocked(mockHttpClient.delete).mockResolvedValue(undefined);

      const client = createAdminClient({ httpClient: mockHttpClient });
      await client.deletePrompt('prompt_123');

      expect(mockHttpClient.delete).toHaveBeenCalledWith('/api/v1/admin/prompts/prompt_123');
    });
  });

  describe('activatePromptVersion', () => {
    it('should activate specific prompt version', async () => {
      const mockResult = {
        promptId: 'prompt_123',
        activeVersionId: 'v2',
        activatedAt: '2024-01-15',
      };
      vi.mocked(mockHttpClient.post).mockResolvedValue(mockResult);

      const client = createAdminClient({ httpClient: mockHttpClient });
      const result = await client.activatePromptVersion('prompt_123', 'v2');

      expect(result.activeVersionId).toBe('v2');
      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/admin/prompts/prompt_123/versions/v2/activate',
        expect.any(Object),
        expect.any(Object)
      );
    });
  });

  describe('getPrompts', () => {
    it('should fetch prompts with pagination', async () => {
      const mockResult = {
        items: [{ id: 'p1', name: 'Rules' }],
        total: 1,
        page: 1,
        pageSize: 20,
      };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockResult);

      const client = createAdminClient({ httpClient: mockHttpClient });
      const result = await client.getPrompts({ page: 1 });

      expect(result.items).toHaveLength(1);
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('page=1'),
        expect.any(Object)
      );
    });

    it('should return empty result when no prompts found', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const client = createAdminClient({ httpClient: mockHttpClient });
      const result = await client.getPrompts();

      expect(result.items).toEqual([]);
    });
  });

  describe('getPromptById', () => {
    it('should fetch single prompt by ID', async () => {
      const mockTemplate = { id: 'prompt_123', name: 'Game Rules' };
      vi.mocked(mockHttpClient.get).mockResolvedValue({ template: mockTemplate });

      const client = createAdminClient({ httpClient: mockHttpClient });
      const result = await client.getPromptById('prompt_123');

      expect(result).toEqual(mockTemplate);
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/admin/prompts/prompt_123',
        expect.any(Object)
      );
    });

    it('should return null when prompt not found', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const client = createAdminClient({ httpClient: mockHttpClient });
      const result = await client.getPromptById('nonexistent');

      expect(result).toBeNull();
    });
  });

  describe('getPromptVersions', () => {
    it('should fetch all versions of prompt', async () => {
      const mockVersions = [
        { id: 'v1', version: 1, content: 'Original' },
        { id: 'v2', version: 2, content: 'Updated' },
      ];
      vi.mocked(mockHttpClient.get).mockResolvedValue({ versions: mockVersions });

      const client = createAdminClient({ httpClient: mockHttpClient });
      const result = await client.getPromptVersions('prompt_123');

      expect(result).toHaveLength(2);
    });

    it('should return empty array when no versions', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const client = createAdminClient({ httpClient: mockHttpClient });
      const result = await client.getPromptVersions('prompt_123');

      expect(result).toEqual([]);
    });
  });

  describe('createPromptVersion', () => {
    it('should create new prompt version', async () => {
      const mockResponse = {
        versionId: 'v3',
        promptId: 'prompt_123',
        createdAt: '2024-01-15',
      };
      vi.mocked(mockHttpClient.post).mockResolvedValue(mockResponse);

      const client = createAdminClient({ httpClient: mockHttpClient });
      const result = await client.createPromptVersion('prompt_123', {
        content: 'Updated content',
        changeReason: 'Improved clarity',
      });

      expect(result.versionId).toBe('v3');
      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/admin/prompts/prompt_123/versions',
        expect.objectContaining({ content: 'Updated content' }),
        expect.any(Object)
      );
    });
  });

  describe('getPromptAuditLogs', () => {
    it('should fetch prompt audit logs', async () => {
      const mockResult = {
        logs: [{ id: 'log_1', action: 'Created', timestamp: '2024-01-15' }],
        totalPages: 1,
      };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockResult);

      const client = createAdminClient({ httpClient: mockHttpClient });
      const result = await client.getPromptAuditLogs('prompt_123');

      expect(result.logs).toHaveLength(1);
    });
  });

  // ========== Admin Stats & Monitoring Tests ==========

  describe('getStats', () => {
    it('should fetch admin dashboard statistics', async () => {
      const mockStats = {
        totalUsers: 150,
        activeUsers: 85,
        totalRequests: 10000,
      };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockStats);

      const client = createAdminClient({ httpClient: mockHttpClient });
      const result = await client.getStats();

      expect(result.totalUsers).toBe(150);
      expect(mockHttpClient.get).toHaveBeenCalledWith('/api/v1/admin/stats', expect.any(Object));
    });

    it('should throw error when stats fetch fails', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const client = createAdminClient({ httpClient: mockHttpClient });

      await expect(client.getStats()).rejects.toThrow('Failed to fetch admin stats');
    });
  });

  describe('getAiRequests', () => {
    it('should fetch AI requests with pagination', async () => {
      const mockResult = {
        requests: [{ id: 'req_1', endpoint: '/api/v1/chat', status: 'success' }],
        totalCount: 100,
      };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockResult);

      const client = createAdminClient({ httpClient: mockHttpClient });
      const result = await client.getAiRequests({ page: 1, pageSize: 20 });

      expect(result.requests).toHaveLength(1);
      expect(result.totalCount).toBe(100);
    });

    it('should return empty result when no requests', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const client = createAdminClient({ httpClient: mockHttpClient });
      const result = await client.getAiRequests();

      expect(result.requests).toEqual([]);
      expect(result.totalCount).toBe(0);
    });
  });

  describe('getAnalytics', () => {
    it('should fetch analytics data with date filters', async () => {
      const mockAnalytics = {
        totalSessions: 500,
        avgSessionDuration: 15.5,
        bounceRate: 0.25,
      };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockAnalytics);

      const client = createAdminClient({ httpClient: mockHttpClient });
      const result = await client.getAnalytics({
        startDate: new Date('2024-01-01'),
        endDate: new Date('2024-01-31'),
      });

      expect(result.totalSessions).toBe(500);
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('fromDate='),
        expect.any(Object)
      );
    });

    it('should return null when analytics unavailable', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const client = createAdminClient({ httpClient: mockHttpClient });
      const result = await client.getAnalytics();

      expect(result).toBeNull();
    });
  });

  describe('getUsers', () => {
    it('should fetch users with pagination', async () => {
      const mockResult = {
        items: [{ id: '1', email: 'user@example.com' }],
        total: 1,
        page: 1,
        pageSize: 20,
      };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockResult);

      const client = createAdminClient({ httpClient: mockHttpClient });
      const result = await client.getUsers({ page: 1 });

      expect(result.items).toHaveLength(1);
    });
  });

  // ========== N8N Workflow Templates Tests ==========

  describe('getWorkflowTemplates', () => {
    it('should fetch n8n workflow templates', async () => {
      const mockTemplates = [{ id: 'wf_1', name: 'Email Notification' }];
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockTemplates);

      const client = createAdminClient({ httpClient: mockHttpClient });
      const result = await client.getWorkflowTemplates();

      expect(result).toHaveLength(1);
      expect(result[0].name).toBe('Email Notification');
    });

    it('should filter templates by category', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue([]);

      const client = createAdminClient({ httpClient: mockHttpClient });
      await client.getWorkflowTemplates('notifications');

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('category=notifications'),
        expect.any(Object)
      );
    });
  });

  describe('importWorkflowTemplate', () => {
    it('should import n8n workflow template', async () => {
      const mockResult = { workflowId: 'new_wf_1', status: 'imported' };
      vi.mocked(mockHttpClient.post).mockResolvedValue(mockResult);

      const client = createAdminClient({ httpClient: mockHttpClient });
      const result = await client.importWorkflowTemplate('template_1', {
        apiKey: 'key_123',
      });

      expect(result.workflowId).toBe('new_wf_1');
    });
  });

  // ========== Activity & Audit Tests ==========

  describe('getRecentActivity', () => {
    it('should fetch recent activity feed', async () => {
      const mockActivity = {
        activities: [{ id: 'act_1', action: 'USER_CREATED', timestamp: '2024-01-15' }],
        totalCount: 100,
      };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockActivity);

      const client = createAdminClient({ httpClient: mockHttpClient });
      const result = await client.getRecentActivity({ limit: 10 });

      expect(result.activities).toHaveLength(1);
    });

    it('should throw error when activity fetch fails', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const client = createAdminClient({ httpClient: mockHttpClient });

      await expect(client.getRecentActivity()).rejects.toThrow('Failed to fetch recent activity');
    });
  });

  describe('getInfrastructureDetails', () => {
    it('should fetch infrastructure details', async () => {
      const mockInfra = {
        services: [{ name: 'postgres', status: 'healthy' }],
        metrics: { cpuUsage: 45.5 },
      };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockInfra);

      const client = createAdminClient({ httpClient: mockHttpClient });
      const result = await client.getInfrastructureDetails();

      expect(result.services).toHaveLength(1);
    });
  });

  // ========== API Key Management Tests ==========

  describe('getApiKeysWithStats', () => {
    it('should fetch API keys with usage statistics', async () => {
      const mockResult = {
        keys: [{ id: 'key_1', name: 'Dev Key', requestCount: 150 }],
        totalCount: 1,
      };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockResult);

      const client = createAdminClient({ httpClient: mockHttpClient });
      const result = await client.getApiKeysWithStats();

      expect(result.keys).toHaveLength(1);
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/admin/api-keys/stats'),
        expect.any(Object)
      );
    });

    it('should throw error when API keys fetch fails', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const client = createAdminClient({ httpClient: mockHttpClient });

      await expect(client.getApiKeysWithStats()).rejects.toThrow('Failed to fetch API keys');
    });
  });

  describe('deleteApiKey', () => {
    it('should delete API key', async () => {
      vi.mocked(mockHttpClient.delete).mockResolvedValue(undefined);

      const client = createAdminClient({ httpClient: mockHttpClient });
      await client.deleteApiKey('key_123');

      expect(mockHttpClient.delete).toHaveBeenCalledWith('/api/v1/admin/api-keys/key_123');
    });
  });

  describe('importApiKeysFromCSV', () => {
    it('should import API keys from CSV', async () => {
      const mockResult = { successCount: 3, failureCount: 1, errors: [] };
      vi.mocked(mockHttpClient.post).mockResolvedValue(mockResult);

      const client = createAdminClient({ httpClient: mockHttpClient });
      const result = await client.importApiKeysFromCSV('key,name\nmpl_test,TestKey');

      expect(result.successCount).toBe(3);
    });
  });

  // ========== Report Generation Tests ==========

  describe('scheduleReport', () => {
    it('should schedule recurring report', async () => {
      const mockResult = { reportId: 'rpt_123', schedule: 'weekly' };
      vi.mocked(mockHttpClient.post).mockResolvedValue(mockResult);

      const client = createAdminClient({ httpClient: mockHttpClient });
      const result = await client.scheduleReport({
        reportType: 'UserActivity',
        frequency: 'weekly',
        recipients: ['admin@example.com'],
      });

      expect(result.reportId).toBe('rpt_123');
    });
  });

  describe('getScheduledReports', () => {
    it('should fetch all scheduled reports', async () => {
      const mockReports = [{ id: 'rpt_1', reportType: 'UserActivity' }];
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockReports);

      const client = createAdminClient({ httpClient: mockHttpClient });
      const result = await client.getScheduledReports();

      expect(result).toHaveLength(1);
    });
  });

  describe('deleteScheduledReport', () => {
    it('should delete scheduled report', async () => {
      vi.mocked(mockHttpClient.delete).mockResolvedValue(undefined);

      const client = createAdminClient({ httpClient: mockHttpClient });
      await client.deleteScheduledReport('rpt_123');

      expect(mockHttpClient.delete).toHaveBeenCalledWith('/api/v1/admin/reports/rpt_123');
    });
  });

  // ========== Testing Metrics Tests ==========

  describe('getAccessibilityMetrics', () => {
    it('should fetch accessibility test metrics', async () => {
      const mockMetrics = {
        totalTests: 50,
        passedTests: 48,
        wcagLevel: 'AA',
      };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockMetrics);

      const client = createAdminClient({ httpClient: mockHttpClient });
      const result = await client.getAccessibilityMetrics();

      expect(result.wcagLevel).toBe('AA');
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/admin/testing/accessibility',
        expect.any(Object)
      );
    });

    it('should throw error when metrics fetch fails', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const client = createAdminClient({ httpClient: mockHttpClient });

      await expect(client.getAccessibilityMetrics()).rejects.toThrow(
        'Failed to fetch accessibility metrics'
      );
    });
  });

  describe('getPerformanceMetrics', () => {
    it('should fetch performance test metrics', async () => {
      const mockMetrics = {
        avgLoadTime: 2.5,
        p95LoadTime: 5.0,
        uptime: 0.9995,
      };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockMetrics);

      const client = createAdminClient({ httpClient: mockHttpClient });
      const result = await client.getPerformanceMetrics();

      expect(result.avgLoadTime).toBe(2.5);
    });

    it('should throw error when performance metrics fail', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const client = createAdminClient({ httpClient: mockHttpClient });

      await expect(client.getPerformanceMetrics()).rejects.toThrow(
        'Failed to fetch performance metrics'
      );
    });
  });

  describe('getE2EMetrics', () => {
    it('should fetch E2E test metrics', async () => {
      const mockMetrics = {
        totalTests: 120,
        passedTests: 118,
        avgExecutionTime: 15.5,
      };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockMetrics);

      const client = createAdminClient({ httpClient: mockHttpClient });
      const result = await client.getE2EMetrics();

      expect(result.passedTests).toBe(118);
    });

    it('should throw error when E2E metrics fail', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const client = createAdminClient({ httpClient: mockHttpClient });

      await expect(client.getE2EMetrics()).rejects.toThrow('Failed to fetch E2E metrics');
    });
  });
});
