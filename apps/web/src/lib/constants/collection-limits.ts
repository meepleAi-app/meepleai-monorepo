/**
 * Collection Limits - Tier-Based Constraints
 * Issue #4183 - Collection Limit UI & Progress Indicators
 *
 * Defines storage and game count limits for each user tier.
 */

export type UserTier = 'Free' | 'Normal' | 'Pro' | 'Enterprise';

export interface CollectionLimits {
  maxGames: number;
  storageMB: number;
}

/**
 * Tier-based collection limits
 * Enterprise tier has unlimited games and storage (represented as Infinity)
 */
export const COLLECTION_LIMITS: Record<UserTier, CollectionLimits> = {
  Free: {
    maxGames: 50,
    storageMB: 100,
  },
  Normal: {
    maxGames: 100,
    storageMB: 500,
  },
  Pro: {
    maxGames: 500,
    storageMB: 5000,
  },
  Enterprise: {
    maxGames: Infinity,
    storageMB: Infinity,
  },
};

/**
 * Get limit color based on usage percentage
 * - Green: < 75%
 * - Yellow: 75-90%
 * - Red: > 90%
 */
export function getLimitColor(percent: number): 'green' | 'yellow' | 'red' {
  if (percent >= 90) return 'red';
  if (percent >= 75) return 'yellow';
  return 'green';
}

/**
 * Format storage size for display
 */
export function formatStorage(mb: number): string {
  if (mb >= 1000) {
    return `${(mb / 1000).toFixed(1)} GB`;
  }
  return `${mb} MB`;
}

/**
 * Format game count for display
 */
export function formatGameCount(count: number): string {
  if (count >= 1000) {
    return `${(count / 1000).toFixed(1)}K`;
  }
  return count.toString();
}
