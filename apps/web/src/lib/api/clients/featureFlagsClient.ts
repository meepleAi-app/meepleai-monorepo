/**
 * Feature Flags API Client
 *
 * User-facing feature access layer.
 * Endpoint: GET /api/v1/users/me/features
 */

import { z } from 'zod';

import { UserFeatureDtoSchema, type UserFeatureDto } from '../schemas/feature-flags.schemas';

import type { HttpClient } from '../core/httpClient';

export interface FeatureFlagsClient {
  getUserFeatures(): Promise<UserFeatureDto[]>;
}

export function createFeatureFlagsClient({
  httpClient,
}: {
  httpClient: HttpClient;
}): FeatureFlagsClient {
  return {
    async getUserFeatures() {
      const data = await httpClient.get<UserFeatureDto[]>('/api/v1/users/me/features');
      return z.array(UserFeatureDtoSchema).parse(data ?? []);
    },
  };
}
