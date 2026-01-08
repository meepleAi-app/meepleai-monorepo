/**
 * Configuration Client Tests - Issue #2340
 * Coverage: 14 tests for configuration CRUD, bulk operations, validation, export/import, history, utilities
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { createConfigClient } from '../configClient';
import type { HttpClient } from '../../core/httpClient';

const mockHttpClient: HttpClient = {
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  delete: vi.fn(),
  patch: vi.fn(),
} as any;

describe('ConfigClient - Issue #2340', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('getConfigurations', () => {
    it('should fetch configurations with default pagination', async () => {
      const mockConfigs = {
        items: [{ id: '1', key: 'app.name', value: 'MeepleAI' }],
        total: 1,
        page: 1,
        pageSize: 50,
      };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockConfigs);

      const client = createConfigClient({ httpClient: mockHttpClient });
      const result = await client.getConfigurations();

      expect(result).toEqual(mockConfigs);
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('activeOnly=true&page=1&pageSize=50'),
        expect.any(Object)
      );
    });

    it('should apply category filter', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue({
        items: [],
        total: 0,
        page: 1,
        pageSize: 50,
      });

      const client = createConfigClient({ httpClient: mockHttpClient });
      await client.getConfigurations('auth');

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('category=auth'),
        expect.any(Object)
      );
    });

    it('should handle null response by returning empty result', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const client = createConfigClient({ httpClient: mockHttpClient });
      const result = await client.getConfigurations();

      expect(result.items).toEqual([]);
      expect(result.total).toBe(0);
    });
  });

  describe('getConfiguration', () => {
    it('should fetch single configuration by ID', async () => {
      const mockConfig = { id: '123', key: 'app.name', value: 'MeepleAI' };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockConfig);

      const client = createConfigClient({ httpClient: mockHttpClient });
      const result = await client.getConfiguration('123');

      expect(result).toEqual(mockConfig);
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/admin/configurations/123',
        expect.any(Object)
      );
    });

    it('should throw error when configuration not found', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const client = createConfigClient({ httpClient: mockHttpClient });

      await expect(client.getConfiguration('nonexistent')).rejects.toThrow(
        'Configuration nonexistent not found'
      );
    });

    it('should encode special characters in ID', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue({ id: '123/456' });

      const client = createConfigClient({ httpClient: mockHttpClient });
      await client.getConfiguration('123/456');

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('123%2F456'),
        expect.any(Object)
      );
    });
  });

  describe('getConfigurationByKey', () => {
    it('should fetch configuration by key', async () => {
      const mockConfig = { id: '123', key: 'db.timeout', value: '5000' };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockConfig);

      const client = createConfigClient({ httpClient: mockHttpClient });
      const result = await client.getConfigurationByKey('db.timeout');

      expect(result).toEqual(mockConfig);
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('key/db.timeout'),
        expect.any(Object)
      );
    });

    it('should throw error when key not found', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const client = createConfigClient({ httpClient: mockHttpClient });

      await expect(client.getConfigurationByKey('invalid.key')).rejects.toThrow(
        "Configuration with key 'invalid.key' not found"
      );
    });
  });

  describe('createConfiguration', () => {
    it('should create new configuration', async () => {
      const mockConfig = { id: '123', key: 'new.config', value: 'test' };
      vi.mocked(mockHttpClient.post).mockResolvedValue(mockConfig);

      const client = createConfigClient({ httpClient: mockHttpClient });
      const result = await client.createConfiguration({
        key: 'new.config',
        value: 'test',
        valueType: 'String',
        category: 'app',
        environment: 'dev',
      });

      expect(result).toEqual(mockConfig);
      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/admin/configurations',
        expect.objectContaining({ key: 'new.config' }),
        expect.any(Object)
      );
    });
  });

  describe('updateConfiguration', () => {
    it('should update existing configuration', async () => {
      const mockConfig = { id: '123', key: 'app.name', value: 'UpdatedName' };
      vi.mocked(mockHttpClient.put).mockResolvedValue(mockConfig);

      const client = createConfigClient({ httpClient: mockHttpClient });
      const result = await client.updateConfiguration('123', { value: 'UpdatedName' });

      expect(result).toEqual(mockConfig);
      expect(mockHttpClient.put).toHaveBeenCalledWith(
        '/api/v1/admin/configurations/123',
        expect.objectContaining({ value: 'UpdatedName' }),
        expect.any(Object)
      );
    });
  });

  describe('deleteConfiguration', () => {
    it('should delete configuration by ID', async () => {
      vi.mocked(mockHttpClient.delete).mockResolvedValue(undefined);

      const client = createConfigClient({ httpClient: mockHttpClient });
      await client.deleteConfiguration('123');

      expect(mockHttpClient.delete).toHaveBeenCalledWith('/api/v1/admin/configurations/123');
    });
  });

  describe('bulkUpdate', () => {
    it('should perform bulk configuration updates', async () => {
      const mockConfigs = [
        { id: '1', value: 'updated1' },
        { id: '2', value: 'updated2' },
      ];
      vi.mocked(mockHttpClient.post).mockResolvedValue(mockConfigs);

      const client = createConfigClient({ httpClient: mockHttpClient });
      const result = await client.bulkUpdate({
        updates: [
          { id: '1', value: 'updated1' },
          { id: '2', value: 'updated2' },
        ],
      });

      expect(result).toEqual(mockConfigs);
      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/admin/configurations/bulk-update',
        expect.objectContaining({ updates: expect.any(Array) })
      );
    });
  });

  describe('validateConfiguration', () => {
    it('should validate configuration value', async () => {
      const mockResult = { isValid: true, errors: [] };
      vi.mocked(mockHttpClient.post).mockResolvedValue(mockResult);

      const client = createConfigClient({ httpClient: mockHttpClient });
      const result = await client.validateConfiguration('db.port', '5432', 'Int');

      expect(result).toEqual(mockResult);
      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/admin/configurations/validate',
        expect.objectContaining({ key: 'db.port', value: '5432' }),
        expect.any(Object)
      );
    });

    it('should report validation errors', async () => {
      const mockResult = { isValid: false, errors: ['Invalid port number'] };
      vi.mocked(mockHttpClient.post).mockResolvedValue(mockResult);

      const client = createConfigClient({ httpClient: mockHttpClient });
      const result = await client.validateConfiguration('db.port', 'invalid', 'Int');

      expect(result.isValid).toBe(false);
      expect(result.errors).toContain('Invalid port number');
    });
  });

  describe('exportConfigurations', () => {
    it('should export configurations for environment', async () => {
      const mockExport = {
        environment: 'prod',
        configurations: [{ key: 'app.name', value: 'MeepleAI' }],
      };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockExport);

      const client = createConfigClient({ httpClient: mockHttpClient });
      const result = await client.exportConfigurations('prod');

      expect(result).toEqual(mockExport);
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('environment=prod&activeOnly=true'),
        expect.any(Object)
      );
    });

    it('should throw error when export fails', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const client = createConfigClient({ httpClient: mockHttpClient });

      await expect(client.exportConfigurations('prod')).rejects.toThrow(
        'Failed to export configurations'
      );
    });
  });

  describe('importConfigurations', () => {
    it('should import configurations from request', async () => {
      vi.mocked(mockHttpClient.post).mockResolvedValue(5);

      const client = createConfigClient({ httpClient: mockHttpClient });
      const result = await client.importConfigurations({
        configurations: [
          {
            key: 'app.name',
            value: 'MeepleAI',
            valueType: 'String',
            category: 'app',
            environment: 'dev',
          },
        ],
      });

      expect(result).toBe(5);
      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/admin/configurations/import',
        expect.objectContaining({ configurations: expect.any(Array) })
      );
    });
  });

  describe('getHistory', () => {
    it('should fetch configuration change history', async () => {
      const mockHistory = [
        { version: 1, value: 'old', changedAt: '2024-01-01' },
        { version: 2, value: 'new', changedAt: '2024-01-02' },
      ];
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockHistory);

      const client = createConfigClient({ httpClient: mockHttpClient });
      const result = await client.getHistory('123');

      expect(result).toEqual(mockHistory);
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/admin/configurations/123/history?limit=20'
      );
    });

    it('should return empty array when history null', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const client = createConfigClient({ httpClient: mockHttpClient });
      const result = await client.getHistory('123');

      expect(result).toEqual([]);
    });
  });

  describe('rollback', () => {
    it('should rollback configuration to previous version', async () => {
      const mockConfig = { id: '123', key: 'app.name', value: 'OldValue' };
      vi.mocked(mockHttpClient.post).mockResolvedValue(mockConfig);

      const client = createConfigClient({ httpClient: mockHttpClient });
      const result = await client.rollback('123', 1);

      expect(result).toEqual(mockConfig);
      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/admin/configurations/123/rollback/1',
        expect.any(Object),
        expect.any(Object)
      );
    });
  });

  describe('getCategories', () => {
    it('should fetch all configuration categories', async () => {
      const mockCategories = ['app', 'database', 'auth', 'cache'];
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockCategories);

      const client = createConfigClient({ httpClient: mockHttpClient });
      const result = await client.getCategories();

      expect(result).toEqual(mockCategories);
      expect(mockHttpClient.get).toHaveBeenCalledWith('/api/v1/admin/configurations/categories');
    });

    it('should return empty array when no categories', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const client = createConfigClient({ httpClient: mockHttpClient });
      const result = await client.getCategories();

      expect(result).toEqual([]);
    });
  });

  describe('invalidateCache', () => {
    it('should invalidate all cache when no key provided', async () => {
      vi.mocked(mockHttpClient.post).mockResolvedValue(undefined);

      const client = createConfigClient({ httpClient: mockHttpClient });
      await client.invalidateCache();

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/admin/configurations/cache/invalidate',
        {}
      );
    });

    it('should invalidate specific cache key', async () => {
      vi.mocked(mockHttpClient.post).mockResolvedValue(undefined);

      const client = createConfigClient({ httpClient: mockHttpClient });
      await client.invalidateCache('app.name');

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/admin/configurations/cache/invalidate',
        { key: 'app.name' }
      );
    });
  });
});
