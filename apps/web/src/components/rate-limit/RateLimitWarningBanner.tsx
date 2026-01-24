/**
 * RateLimitWarningBanner Component
 * Issue #2749: Frontend - Rate Limit Feedback UI
 *
 * Non-intrusive warning banner when approaching rate limits:
 * - Shows when >= 80% of pending or monthly limits used
 * - Shows during cooldown
 * - Dismissible with localStorage persistence
 */

'use client';

import { useState, useEffect } from 'react';
import { format } from 'date-fns';
import { X, AlertTriangle } from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';
import { Button } from '@/components/ui/primitives/button';
import { useRateLimitStatus } from '@/hooks/queries/useShareRequests';

const BANNER_DISMISSED_KEY = 'rate-limit-banner-dismissed';
const DISMISSAL_DURATION_MS = 24 * 60 * 60 * 1000; // 24 hours

/**
 * Check if banner was recently dismissed
 */
function isBannerDismissed(): boolean {
  if (typeof window === 'undefined') return false;

  try {
    const dismissedAt = localStorage.getItem(BANNER_DISMISSED_KEY);
    if (!dismissedAt) return false;

    const dismissedTime = new Date(dismissedAt).getTime();
    const now = Date.now();
    return now - dismissedTime < DISMISSAL_DURATION_MS;
  } catch {
    return false;
  }
}

/**
 * Mark banner as dismissed
 */
function dismissBanner(): void {
  if (typeof window === 'undefined') return;

  try {
    localStorage.setItem(BANNER_DISMISSED_KEY, new Date().toISOString());
  } catch {
    // Ignore localStorage errors
  }
}

/**
 * Calculate usage percentage
 */
function getUsagePercentage(current: number, max: number): number {
  return max > 0 ? (current / max) * 100 : 0;
}

/**
 * RateLimitWarningBanner Component
 *
 * Displays warning banner when:
 * - Pending requests >= 80% used
 * - Monthly requests >= 80% used
 * - User is in cooldown
 *
 * Banner can be dismissed and will remain hidden for 24 hours.
 *
 * @example
 * ```tsx
 * <RateLimitWarningBanner />
 * ```
 */
export function RateLimitWarningBanner() {
  const { data: status } = useRateLimitStatus();
  const [dismissed, setDismissed] = useState(false);

  // Check localStorage on mount
  useEffect(() => {
    setDismissed(isBannerDismissed());
  }, []);

  if (!status || dismissed) return null;

  const pendingPercent = getUsagePercentage(
    status.currentPendingCount,
    status.maxPendingAllowed
  );
  const monthlyPercent = getUsagePercentage(
    status.currentMonthlyCount,
    status.maxMonthlyAllowed
  );

  const showWarning = pendingPercent >= 80 || monthlyPercent >= 80 || status.isInCooldown;

  if (!showWarning) return null;

  const handleDismiss = () => {
    dismissBanner();
    setDismissed(true);
  };

  // Determine warning message
  let message = '';
  if (status.isInCooldown && status.cooldownEndsAt) {
    message = `Cooldown active until ${format(new Date(status.cooldownEndsAt), 'MMM d')}`;
  } else if (monthlyPercent >= 80) {
    message = `You've used ${Math.round(monthlyPercent)}% of your monthly limit`;
  } else {
    message = `You have ${status.maxPendingAllowed - status.currentPendingCount} pending slots remaining`;
  }

  return (
    <Alert className="relative">
      <AlertTriangle className="h-4 w-4" />
      <AlertTitle>Rate Limit Warning</AlertTitle>
      <AlertDescription>{message}</AlertDescription>
      <Button
        variant="ghost"
        size="sm"
        className="absolute right-2 top-2 h-6 w-6 p-0"
        onClick={handleDismiss}
        aria-label="Dismiss warning"
      >
        <X className="h-4 w-4" />
      </Button>
    </Alert>
  );
}
