/**
 * Session Quota API Schemas (Issue #3075)
 *
 * Zod schemas for validating session quota responses.
 * Session quota tracks user's active game sessions against tier-based limits.
 */

import { z } from 'zod';

/**
 * Session tier enum for session limits
 * Note: Different from rate-limits UserTier which uses 'Free', 'Basic', 'Premium', 'Pro', 'Admin'
 * Session tiers use lowercase: 'free', 'normal', 'premium'
 */
export const SessionTierSchema = z.enum(['free', 'normal', 'premium']);
export type SessionTier = z.infer<typeof SessionTierSchema>;

/**
 * Session quota response DTO matching backend contract
 *
 * @see SessionQuotaDto in SessionLimitsConfigEndpoints.cs
 */
export const SessionQuotaResponseSchema = z.object({
  /** Number of currently active sessions */
  currentSessions: z.number().int().nonnegative(),
  /** Maximum allowed sessions for user's tier */
  maxSessions: z.number().int().positive(),
  /** Remaining slots available */
  remainingSlots: z.number().int().nonnegative(),
  /** Whether user can create a new session */
  canCreateNew: z.boolean(),
  /** Whether user has unlimited sessions (admin) */
  isUnlimited: z.boolean(),
  /** User's current tier */
  userTier: z.string(),
});

export type SessionQuotaResponse = z.infer<typeof SessionQuotaResponseSchema>;

/**
 * Helper function to calculate percentage used
 * @param quota Session quota response
 * @returns Percentage of quota used (0-100)
 */
export function calculateQuotaPercentage(quota: SessionQuotaResponse): number {
  if (quota.isUnlimited || quota.maxSessions === 0) {
    return 0;
  }
  return Math.round((quota.currentSessions / quota.maxSessions) * 100);
}

/**
 * Helper function to determine quota warning level
 * @param quota Session quota response
 * @returns Warning level: 'none' | 'warning' | 'critical' | 'full'
 */
export function getQuotaWarningLevel(
  quota: SessionQuotaResponse
): 'none' | 'warning' | 'critical' | 'full' {
  if (quota.isUnlimited) {
    return 'none';
  }

  const percentage = calculateQuotaPercentage(quota);

  if (percentage >= 100 || !quota.canCreateNew) {
    return 'full';
  }
  if (percentage >= 90) {
    return 'critical';
  }
  if (percentage >= 75) {
    return 'warning';
  }
  return 'none';
}
