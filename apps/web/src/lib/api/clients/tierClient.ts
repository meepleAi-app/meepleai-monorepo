/**
 * Tier & Usage API Client (Game Night Improvvisata - E2-4/E2-5)
 *
 * API client for user usage snapshot and tier definitions.
 * GET  /api/v1/users/me/usage      -> UsageSnapshot
 * GET  /api/v1/admin/tiers         -> TierDefinition[]
 * POST /api/v1/admin/tiers         -> TierDefinition
 * PUT  /api/v1/admin/tiers/{name}  -> TierDefinition
 */

import { type HttpClient } from '../core/httpClient';
import {
  UsageSnapshotSchema,
  TierDefinitionSchema,
  TierListResponseSchema,
  type UsageSnapshot,
  type TierDefinition,
  type TierLimits,
} from '../schemas/tier.schemas';

export interface CreateTierClientParams {
  httpClient: HttpClient;
}

export interface CreateTierRequest {
  name: string;
  displayName: string;
  limits: TierLimits;
  llmModelTier: string;
  isDefault?: boolean;
}

export interface UpdateTierRequest {
  displayName?: string;
  limits?: TierLimits;
  llmModelTier?: string;
  isDefault?: boolean;
}

/**
 * Tier & Usage API client with Zod validation
 */
export function createTierClient({ httpClient }: CreateTierClientParams) {
  return {
    async getMyUsage(): Promise<UsageSnapshot> {
      const response = await httpClient.get('/api/v1/users/me/usage');
      return UsageSnapshotSchema.parse(response);
    },

    async getTiers(): Promise<TierDefinition[]> {
      const response = await httpClient.get('/api/v1/admin/tiers');
      return TierListResponseSchema.parse(response);
    },

    async createTier(request: CreateTierRequest): Promise<TierDefinition> {
      const response = await httpClient.post('/api/v1/admin/tiers', request);
      return TierDefinitionSchema.parse(response);
    },

    async updateTier(name: string, request: UpdateTierRequest): Promise<TierDefinition> {
      const response = await httpClient.put(`/api/v1/admin/tiers/${name}`, request);
      return TierDefinitionSchema.parse(response);
    },
  };
}

export type TierClient = ReturnType<typeof createTierClient>;
