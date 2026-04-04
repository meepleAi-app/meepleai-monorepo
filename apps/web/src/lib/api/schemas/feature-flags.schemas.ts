/**
 * Feature Flags API Schemas
 *
 * Zod schemas for user-facing feature flag access (UserFeatureDto).
 * Endpoint: GET /api/v1/users/me/features
 */

import { z } from 'zod';

// ========== User Feature DTO ==========

export const UserFeatureDtoSchema = z.object({
  key: z.string().min(1),
  name: z.string().min(1),
  description: z.string().nullable().optional(),
  hasAccess: z.boolean(),
  accessReason: z.string().nullable().optional(),
});

export type UserFeatureDto = z.infer<typeof UserFeatureDtoSchema>;
