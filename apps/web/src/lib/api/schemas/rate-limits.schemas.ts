/**
 * Rate Limit Configuration Schemas (Issue #2750)
 *
 * Zod schemas for admin rate limit configuration API.
 * Epic #2718: Game Sharing from User Library to Shared Catalog
 * Milestone: 6 - Frontend Extensions
 */

import { z } from 'zod';

/**
 * User tier enum schema
 */
export const UserTierSchema = z.enum(['Free', 'Basic', 'Premium', 'Pro', 'Admin']);

/**
 * Cooldown duration schema
 */
export const CooldownDurationSchema = z.object({
  days: z.number(),
  totalSeconds: z.number(),
});

/**
 * Rate limit configuration DTO schema
 */
export const RateLimitConfigDtoSchema = z.object({
  tier: UserTierSchema,
  tierDisplayName: z.string(),
  maxPendingRequests: z.number(),
  maxRequestsPerMonth: z.number(),
  cooldownAfterRejection: CooldownDurationSchema,
  cooldownDisplay: z.string(),
  updatedAt: z.string(),
  updatedByAdminName: z.string().optional(),
});

/**
 * Rate limit override DTO schema
 */
export const RateLimitOverrideDtoSchema = z.object({
  userId: z.string().uuid(),
  userName: z.string(),
  userEmail: z.string().email(),
  userTier: UserTierSchema,
  maxPendingRequests: z.number().optional(),
  maxRequestsPerMonth: z.number().optional(),
  cooldownAfterRejection: CooldownDurationSchema.optional(),
  expiresAt: z.string().optional(),
  isExpired: z.boolean(),
  reason: z.string(),
  createdAt: z.string(),
  createdByAdminName: z.string().optional(),
});

/**
 * Tier configs list response schema
 */
export const RateLimitConfigListSchema = z.object({
  items: z.array(RateLimitConfigDtoSchema),
  totalCount: z.number(),
});

/**
 * User overrides paginated list response schema
 */
export const RateLimitOverrideListSchema = z.object({
  items: z.array(RateLimitOverrideDtoSchema),
  totalCount: z.number(),
  pageNumber: z.number(),
  pageSize: z.number(),
});

/**
 * Update tier config request schema
 */
export const UpdateTierConfigRequestSchema = z.object({
  maxPendingRequests: z.number().positive(),
  maxRequestsPerMonth: z.number().positive(),
  cooldownDays: z.number().nonnegative(),
});

/**
 * Create override request schema
 */
export const CreateOverrideRequestSchema = z.object({
  userId: z.string().uuid(),
  maxPendingRequests: z.number().positive().optional(),
  maxRequestsPerMonth: z.number().positive().optional(),
  cooldownDays: z.number().nonnegative().optional(),
  expiresAt: z.string().optional(),
  reason: z.string().min(10, 'Reason must be at least 10 characters'),
});

// Export inferred TypeScript types
export type UserTier = z.infer<typeof UserTierSchema>;
export type CooldownDuration = z.infer<typeof CooldownDurationSchema>;
export type RateLimitConfigDto = z.infer<typeof RateLimitConfigDtoSchema>;
export type RateLimitOverrideDto = z.infer<typeof RateLimitOverrideDtoSchema>;
export type RateLimitConfigListDto = z.infer<typeof RateLimitConfigListSchema>;
export type RateLimitOverrideListDto = z.infer<typeof RateLimitOverrideListSchema>;
export type UpdateTierConfigRequest = z.infer<typeof UpdateTierConfigRequestSchema>;
export type CreateOverrideRequest = z.infer<typeof CreateOverrideRequestSchema>;
