/**
 * useRateLimitNotifications Hook
 * Issue #2749: Frontend - Rate Limit Feedback UI
 *
 * Monitors rate limit status changes and shows toast notifications:
 * - When cooldown ends (can submit again)
 * - When approaching limits (80% usage)
 */

import { useEffect, useRef } from 'react';

import { toast } from 'sonner';

import { useRateLimitStatus } from './queries/useShareRequests';

/**
 * Calculate usage percentage
 */
function getUsagePercentage(current: number, max: number): number {
  return max > 0 ? (current / max) * 100 : 0;
}

/**
 * useRateLimitNotifications Hook
 *
 * Automatically displays toast notifications when:
 * - Cooldown period ends
 * - Monthly usage reaches 80%
 * - Pending requests reach 80%
 *
 * @example
 * ```tsx
 * function MyComponent() {
 *   useRateLimitNotifications();
 *   // ... rest of component
 * }
 * ```
 */
export function useRateLimitNotifications(): void {
  const { data: status } = useRateLimitStatus();
  const previousStatus = useRef(status);

  useEffect(() => {
    if (!status || !previousStatus.current) {
      previousStatus.current = status;
      return;
    }

    const prev = previousStatus.current;

    // Check if cooldown just ended
    if (prev.isInCooldown && !status.isInCooldown) {
      toast.success('You can now submit share requests again!');
    }

    // Check if just hit monthly 80% threshold
    const prevMonthlyPercent = getUsagePercentage(
      prev.currentMonthlyCount,
      prev.maxMonthlyAllowed
    );
    const currentMonthlyPercent = getUsagePercentage(
      status.currentMonthlyCount,
      status.maxMonthlyAllowed
    );

    if (prevMonthlyPercent < 80 && currentMonthlyPercent >= 80) {
      const remaining = status.maxMonthlyAllowed - status.currentMonthlyCount;
      toast.warning(
        `You've used 80% of your monthly share requests. ${remaining} remaining.`
      );
    }

    // Check if just hit pending 80% threshold
    const prevPendingPercent = getUsagePercentage(
      prev.currentPendingCount,
      prev.maxPendingAllowed
    );
    const currentPendingPercent = getUsagePercentage(
      status.currentPendingCount,
      status.maxPendingAllowed
    );

    if (prevPendingPercent < 80 && currentPendingPercent >= 80) {
      const remaining = status.maxPendingAllowed - status.currentPendingCount;
      toast.warning(
        `You're approaching your pending request limit. ${remaining} remaining.`
      );
    }

    // Update previous status
    previousStatus.current = status;
  }, [status]);
}
