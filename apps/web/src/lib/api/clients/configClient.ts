/**
 * Configuration Client (FE-IMP-005)
 *
 * Modular client for SystemConfiguration bounded context.
 * Covers: Dynamic configuration CRUD, history, validation, export/import
 */

import {
  SystemConfigurationDtoSchema,
  PagedResultSchema,
  ConfigurationValidationResultSchema,
  ConfigurationExportDtoSchema,
  type SystemConfigurationDto,
  type PagedResult,
  type ConfigurationHistoryDto,
  type ConfigurationValidationResult,
  type ConfigurationExportDto,
} from '../schemas';

import type { HttpClient } from '../core/httpClient';

export interface CreateConfigClientParams {
  httpClient: HttpClient;
}

export interface CreateConfigurationRequest {
  key: string;
  value: string;
  valueType: string;
  description?: string | null;
  category: string;
  requiresRestart?: boolean;
  environment: string;
}

export interface UpdateConfigurationRequest {
  value: string;
  description?: string | null;
  isActive?: boolean;
  requiresRestart?: boolean;
}

export interface BulkConfigurationUpdateRequest {
  updates: Array<{
    id: string;
    value: string;
    description?: string | null;
    isActive?: boolean;
  }>;
}

export interface ConfigurationImportRequest {
  configurations: Array<{
    key: string;
    value: string;
    valueType: string;
    description?: string | null;
    category: string;
    environment: string;
  }>;
  overwriteExisting?: boolean;
}

/**
 * Configuration API client with Zod validation
 */
export function createConfigClient({ httpClient }: CreateConfigClientParams) {
  return {
    // ========== Configuration CRUD ==========

    /**
     * Get all configurations with optional filtering and pagination
     * @param category Optional category filter
     * @param environment Optional environment filter
     * @param activeOnly Show only active configurations (default: true)
     * @param page Page number (default: 1)
     * @param pageSize Items per page (default: 50)
     */
    async getConfigurations(
      category?: string,
      environment?: string,
      activeOnly: boolean = true,
      page: number = 1,
      pageSize: number = 50
    ): Promise<PagedResult<SystemConfigurationDto>> {
      const params = new URLSearchParams();
      if (category) params.append('category', category);
      if (environment) params.append('environment', environment);
      params.append('activeOnly', activeOnly.toString());
      params.append('page', page.toString());
      params.append('pageSize', pageSize.toString());

      const response = await httpClient.get(
        `/api/v1/admin/configurations?${params}`,
        PagedResultSchema(SystemConfigurationDtoSchema)
      );

      if (!response) {
        return {
          items: [],
          total: 0,
          page,
          pageSize,
        };
      }

      return response;
    },

    /**
     * Get a single configuration by ID
     * @param id Configuration ID (GUID format)
     */
    async getConfiguration(id: string): Promise<SystemConfigurationDto> {
      const response = await httpClient.get(
        `/api/v1/admin/configurations/${encodeURIComponent(id)}`,
        SystemConfigurationDtoSchema
      );

      if (!response) {
        throw new Error(`Configuration ${id} not found`);
      }

      return response;
    },

    /**
     * Get a configuration by key
     * @param key Configuration key
     * @param environment Optional environment filter
     */
    async getConfigurationByKey(
      key: string,
      environment?: string
    ): Promise<SystemConfigurationDto> {
      const params = new URLSearchParams();
      if (environment) params.append('environment', environment);

      const response = await httpClient.get(
        `/api/v1/admin/configurations/key/${encodeURIComponent(key)}?${params}`,
        SystemConfigurationDtoSchema
      );

      if (!response) {
        throw new Error(`Configuration with key '${key}' not found`);
      }

      return response;
    },

    /**
     * Create a new configuration
     * @param request Configuration creation request
     */
    async createConfiguration(
      request: CreateConfigurationRequest
    ): Promise<SystemConfigurationDto> {
      return httpClient.post('/api/v1/admin/configurations', request, SystemConfigurationDtoSchema);
    },

    /**
     * Update an existing configuration
     * @param id Configuration ID (GUID format)
     * @param request Configuration update request
     */
    async updateConfiguration(
      id: string,
      request: UpdateConfigurationRequest
    ): Promise<SystemConfigurationDto> {
      return httpClient.put(
        `/api/v1/admin/configurations/${encodeURIComponent(id)}`,
        request,
        SystemConfigurationDtoSchema
      );
    },

    /**
     * Delete a configuration
     * @param id Configuration ID (GUID format)
     */
    async deleteConfiguration(id: string): Promise<void> {
      return httpClient.delete(`/api/v1/admin/configurations/${encodeURIComponent(id)}`);
    },

    // ========== Bulk Operations ==========

    /**
     * Bulk update multiple configurations
     * @param request Bulk update request with configuration updates
     */
    async bulkUpdate(request: BulkConfigurationUpdateRequest): Promise<SystemConfigurationDto[]> {
      const response = await httpClient.post<SystemConfigurationDto[]>(
        '/api/v1/admin/configurations/bulk-update',
        request
      );
      return response;
    },

    // ========== Validation ==========

    /**
     * Validate a configuration value
     * @param key Configuration key
     * @param value Configuration value to validate
     * @param valueType Value type (String, Int, Bool, Json, etc.)
     */
    async validateConfiguration(
      key: string,
      value: string,
      valueType: string
    ): Promise<ConfigurationValidationResult> {
      return httpClient.post(
        '/api/v1/admin/configurations/validate',
        { key, value, valueType },
        ConfigurationValidationResultSchema
      );
    },

    // ========== Export/Import ==========

    /**
     * Export configurations for an environment
     * @param environment Target environment
     * @param activeOnly Export only active configurations (default: true)
     */
    async exportConfigurations(
      environment: string,
      activeOnly: boolean = true
    ): Promise<ConfigurationExportDto> {
      const params = new URLSearchParams();
      params.append('environment', environment);
      params.append('activeOnly', activeOnly.toString());

      const response = await httpClient.get(
        `/api/v1/admin/configurations/export?${params}`,
        ConfigurationExportDtoSchema
      );

      if (!response) {
        throw new Error('Failed to export configurations');
      }

      return response;
    },

    /**
     * Import configurations from export file
     * @param request Import request with configurations
     */
    async importConfigurations(request: ConfigurationImportRequest): Promise<number> {
      const response = await httpClient.post<number>(
        '/api/v1/admin/configurations/import',
        request
      );
      return response;
    },

    // ========== History ==========

    /**
     * Get configuration change history
     * @param configurationId Configuration ID (GUID format)
     * @param limit Maximum number of history entries (default: 20)
     */
    async getHistory(
      configurationId: string,
      limit: number = 20
    ): Promise<ConfigurationHistoryDto[]> {
      const response = await httpClient.get<ConfigurationHistoryDto[]>(
        `/api/v1/admin/configurations/${encodeURIComponent(configurationId)}/history?limit=${limit}`
      );

      if (!response) {
        return [];
      }

      return response;
    },

    /**
     * Rollback configuration to a previous version
     * @param configurationId Configuration ID (GUID format)
     * @param toVersion Target version number to rollback to
     */
    async rollback(configurationId: string, toVersion: number): Promise<SystemConfigurationDto> {
      return httpClient.post(
        `/api/v1/admin/configurations/${encodeURIComponent(configurationId)}/rollback/${toVersion}`,
        {},
        SystemConfigurationDtoSchema
      );
    },

    // ========== Utilities ==========

    /**
     * Get all unique categories
     */
    async getCategories(): Promise<string[]> {
      const response = await httpClient.get<string[]>('/api/v1/admin/configurations/categories');

      if (!response) {
        return [];
      }

      return response;
    },

    /**
     * Invalidate configuration cache
     * @param key Optional configuration key to invalidate (invalidates all if not provided)
     */
    async invalidateCache(key?: string): Promise<void> {
      const body = key ? { key } : {};
      return httpClient.post('/api/v1/admin/configurations/cache/invalidate', body);
    },
  };
}

export type ConfigClient = ReturnType<typeof createConfigClient>;
