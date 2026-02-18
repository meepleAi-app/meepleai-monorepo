/**
 * BGG API Rate Limit Tracking Hook
 * Issue #4274: Track and display BGG API quota from response headers
 *
 * Parses X-RateLimit-* headers from BGG API responses:
 * - X-RateLimit-Limit: Maximum requests per window
 * - X-RateLimit-Remaining: Requests remaining
 * - X-RateLimit-Reset: Unix timestamp when limit resets
 *
 * Provides:
 * - Quota state (limit, remaining, resetAt)
 * - Exhaustion detection (remaining === 0)
 * - Countdown timer until reset
 * - Refresh function for manual updates
 */

'use client';

import { useState, useEffect, useCallback } from 'react';

export interface BggRateLimitQuota {
  limit: number | 'unlimited';
  remaining: number | 'unlimited';
  resetAt: Date | null;
  isExhausted: boolean;
  countdown: number; // Seconds until reset
}

export interface UseBggRateLimitResult {
  quota: BggRateLimitQuota;
  updateFromHeaders: (headers: Headers) => void;
  resetQuota: () => void;
}

const DEFAULT_QUOTA: BggRateLimitQuota = {
  limit: 'unlimited',
  remaining: 'unlimited',
  resetAt: null,
  isExhausted: false,
  countdown: 0,
};

/**
 * Parse rate limit headers from BGG API response
 */
function parseRateLimitHeaders(headers: Headers): BggRateLimitQuota {
  const limit = headers.get('X-RateLimit-Limit');
  const remaining = headers.get('X-RateLimit-Remaining');
  const reset = headers.get('X-RateLimit-Reset');

  // Handle unlimited (admin users)
  if (limit === 'unlimited' || remaining === 'unlimited') {
    return {
      limit: 'unlimited',
      remaining: 'unlimited',
      resetAt: null,
      isExhausted: false,
      countdown: 0,
    };
  }

  // Parse numeric values
  const limitNum = limit ? parseInt(limit, 10) : null;
  const remainingNum = remaining ? parseInt(remaining, 10) : null;
  const resetTimestamp = reset ? parseInt(reset, 10) : null;

  if (limitNum === null || remainingNum === null) {
    return DEFAULT_QUOTA; // No rate limit info available
  }

  const resetAt = resetTimestamp ? new Date(resetTimestamp * 1000) : null;
  const now = new Date();
  const countdown = resetAt ? Math.max(0, Math.ceil((resetAt.getTime() - now.getTime()) / 1000)) : 0;

  return {
    limit: limitNum,
    remaining: remainingNum,
    resetAt,
    isExhausted: remainingNum === 0,
    countdown,
  };
}

/**
 * Hook for tracking BGG API rate limit quota
 *
 * @example
 * ```tsx
 * const { quota, updateFromHeaders } = useBggRateLimit();
 *
 * // After BGG API call
 * const response = await fetch('/api/v1/bgg/search?query=wingspan');
 * updateFromHeaders(response.headers);
 *
 * // Display quota
 * if (quota.isExhausted) {
 *   return <div>Rate limit reached. Resets in {quota.countdown}s</div>;
 * }
 * return <div>🔍 {quota.remaining}/{quota.limit} searches</div>;
 * ```
 */
export function useBggRateLimit(): UseBggRateLimitResult {
  const [quota, setQuota] = useState<BggRateLimitQuota>(DEFAULT_QUOTA);

  // Update countdown every second
  useEffect(() => {
    if (quota.resetAt === null || quota.countdown === 0) {
      return;
    }

    const timer = setInterval(() => {
      if (!quota.resetAt) return; // Skip if no reset time

      const now = new Date();
      const newCountdown = Math.max(
        0,
        Math.ceil((quota.resetAt.getTime() - now.getTime()) / 1000)
      );

      if (newCountdown === 0) {
        // Reset expired - quota should be refreshed
        setQuota(prev => ({
          ...prev,
          remaining: prev.limit,
          isExhausted: false,
          countdown: 0,
        }));
      } else {
        setQuota(prev => ({ ...prev, countdown: newCountdown }));
      }
    }, 1000);

    return () => clearInterval(timer);
  }, [quota.resetAt, quota.countdown]);

  const updateFromHeaders = useCallback((headers: Headers) => {
    const newQuota = parseRateLimitHeaders(headers);
    setQuota(newQuota);
  }, []);

  const resetQuota = useCallback(() => {
    setQuota(DEFAULT_QUOTA);
  }, []);

  return {
    quota,
    updateFromHeaders,
    resetQuota,
  };
}

/**
 * Format countdown as human-readable string
 *
 * @example
 * formatCountdown(45) → "45s"
 * formatCountdown(125) → "2min 5s"
 * formatCountdown(3665) → "1h 1min"
 */
export function formatCountdown(seconds: number): string {
  if (seconds < 60) return `${seconds}s`;

  const minutes = Math.floor(seconds / 60);
  const remainingSeconds = seconds % 60;

  if (minutes < 60) {
    return remainingSeconds > 0 ? `${minutes}min ${remainingSeconds}s` : `${minutes}min`;
  }

  const hours = Math.floor(minutes / 60);
  const remainingMinutes = minutes % 60;

  return remainingMinutes > 0 ? `${hours}h ${remainingMinutes}min` : `${hours}h`;
}
