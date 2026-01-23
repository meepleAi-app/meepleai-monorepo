/**
 * Rate Limit Configuration Types
 * Issue #2750 - Admin Rate Limit Config Panel
 */

/**
 * User tier enum (mirrors backend SharedGameCatalog.Domain.ValueObjects.UserTier)
 */
export type UserTier = 'Free' | 'Basic' | 'Premium' | 'Pro' | 'Admin';

/**
 * Cooldown duration value object
 */
export interface CooldownDuration {
  days: number;
  totalSeconds: number;
}

/**
 * Rate limit configuration for a user tier
 */
export interface RateLimitConfigDto {
  tier: UserTier;
  tierDisplayName: string;
  maxPendingRequests: number;
  maxRequestsPerMonth: number;
  cooldownAfterRejection: CooldownDuration;
  cooldownDisplay: string;
  updatedAt: string;
  updatedByAdminName?: string;
}

/**
 * User-specific rate limit override
 */
export interface RateLimitOverrideDto {
  userId: string;
  userName: string;
  userEmail: string;
  userTier: UserTier;
  maxPendingRequests?: number;
  maxRequestsPerMonth?: number;
  cooldownAfterRejection?: CooldownDuration;
  expiresAt?: string;
  isExpired: boolean;
  reason: string;
  createdAt: string;
  createdByAdminName?: string;
}

/**
 * Request to update tier configuration
 */
export interface UpdateTierConfigRequest {
  maxPendingRequests: number;
  maxRequestsPerMonth: number;
  cooldownDays: number;
}

/**
 * Request to create user override
 */
export interface CreateOverrideRequest {
  userId: string;
  maxPendingRequests?: number;
  maxRequestsPerMonth?: number;
  cooldownDays?: number;
  expiresAt?: string;
  reason: string;
}

/**
 * Paginated list response
 */
export interface RateLimitConfigListDto {
  items: RateLimitConfigDto[];
  totalCount: number;
}

export interface RateLimitOverrideListDto {
  items: RateLimitOverrideDto[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}
