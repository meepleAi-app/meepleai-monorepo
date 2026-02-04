/**
 * Tier-Strategy Admin Client
 * Issue #3440: Admin UI for tier-strategy configuration
 */

import {
  TierStrategyMatrixSchema,
  StrategyModelMappingSchema,
  TierStrategyAccessSchema,
  TierStrategyResetResultSchema,
  type TierStrategyMatrixDto,
  type StrategyModelMappingDto,
  type TierStrategyAccessDto,
  type TierStrategyResetResultDto,
  type UpdateTierStrategyAccessRequest,
  type UpdateStrategyModelMappingRequest,
  type ResetTierStrategyConfigRequest,
} from '../schemas/tier-strategy.schemas';

import type { HttpClient } from '../core/httpClient';

export interface TierStrategyClient {
  /**
   * Get the tier-strategy access matrix
   */
  getMatrix(): Promise<TierStrategyMatrixDto>;

  /**
   * Get all strategy-model mappings
   */
  getModelMappings(): Promise<StrategyModelMappingDto[]>;

  /**
   * Update tier-strategy access
   */
  updateAccess(request: UpdateTierStrategyAccessRequest): Promise<TierStrategyAccessDto>;

  /**
   * Update strategy-model mapping
   */
  updateModelMapping(request: UpdateStrategyModelMappingRequest): Promise<StrategyModelMappingDto>;

  /**
   * Reset configuration to defaults
   */
  reset(request?: ResetTierStrategyConfigRequest): Promise<TierStrategyResetResultDto>;
}

export interface TierStrategyClientConfig {
  httpClient: HttpClient;
}

export function createTierStrategyClient(config: TierStrategyClientConfig): TierStrategyClient {
  const { httpClient } = config;
  const basePath = '/api/v1/admin/tier-strategy';

  return {
    async getMatrix(): Promise<TierStrategyMatrixDto> {
      const response = await httpClient.get<TierStrategyMatrixDto>(`${basePath}/matrix`);
      return TierStrategyMatrixSchema.parse(response);
    },

    async getModelMappings(): Promise<StrategyModelMappingDto[]> {
      const response = await httpClient.get<StrategyModelMappingDto[]>(`${basePath}/model-mappings`);
      if (!response) return [];
      return response.map(item => StrategyModelMappingSchema.parse(item));
    },

    async updateAccess(request: UpdateTierStrategyAccessRequest): Promise<TierStrategyAccessDto> {
      const response = await httpClient.put<TierStrategyAccessDto>(`${basePath}/access`, request);
      return TierStrategyAccessSchema.parse(response);
    },

    async updateModelMapping(
      request: UpdateStrategyModelMappingRequest
    ): Promise<StrategyModelMappingDto> {
      const response = await httpClient.put<StrategyModelMappingDto>(
        `${basePath}/model-mapping`,
        request
      );
      return StrategyModelMappingSchema.parse(response);
    },

    async reset(request?: ResetTierStrategyConfigRequest): Promise<TierStrategyResetResultDto> {
      const body = request ?? { resetAccessMatrix: true, resetModelMappings: true };
      const response = await httpClient.post<TierStrategyResetResultDto>(`${basePath}/reset`, body);
      return TierStrategyResetResultSchema.parse(response);
    },
  };
}
