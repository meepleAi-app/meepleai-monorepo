/**
 * RateLimitStatusCard Component
 * Issue #2749: Frontend - Rate Limit Feedback UI
 *
 * Comprehensive card displaying all rate limit information:
 * - Pending requests limit
 * - Monthly requests limit
 * - Cooldown status with countdown
 * - Visual progress bars
 */

'use client';

import { format } from 'date-fns';
import { AlertCircle } from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/data-display/card';
import { RateLimitProgress } from './RateLimitProgress';
import { useRateLimitStatus } from '@/hooks/queries/useShareRequests';

/**
 * Calculate if user can create a new share request
 */
function canCreateRequest(status: {
  currentPendingCount: number;
  maxPendingAllowed: number;
  currentMonthlyCount: number;
  maxMonthlyAllowed: number;
  isInCooldown: boolean;
}): boolean {
  if (status.isInCooldown) return false;
  if (status.currentPendingCount >= status.maxPendingAllowed) return false;
  if (status.currentMonthlyCount >= status.maxMonthlyAllowed) return false;
  return true;
}

/**
 * Generate user-friendly block reason
 */
function getBlockReason(status: {
  currentPendingCount: number;
  maxPendingAllowed: number;
  currentMonthlyCount: number;
  maxMonthlyAllowed: number;
  isInCooldown: boolean;
  cooldownEndsAt: string | null;
}): string | null {
  if (status.isInCooldown && status.cooldownEndsAt) {
    return `Cooldown active until ${format(new Date(status.cooldownEndsAt), 'MMM d \'at\' h:mm a')}`;
  }

  if (status.currentPendingCount >= status.maxPendingAllowed) {
    return 'Maximum pending requests reached';
  }

  if (status.currentMonthlyCount >= status.maxMonthlyAllowed) {
    return 'Monthly request limit reached';
  }

  return null;
}

/**
 * RateLimitStatusCard Component
 *
 * Displays comprehensive rate limit status with:
 * - Pending requests progress
 * - Monthly requests progress
 * - Cooldown status
 * - Reset time information
 *
 * @example
 * ```tsx
 * <RateLimitStatusCard />
 * ```
 */
export function RateLimitStatusCard(): JSX.Element | null {
  const { data: status, isLoading } = useRateLimitStatus();

  if (isLoading) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Share Request Limits</CardTitle>
          <CardDescription>Loading your rate limit status...</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="h-24 animate-pulse rounded-md bg-muted" />
        </CardContent>
      </Card>
    );
  }

  if (!status) {
    return null;
  }

  const blocked = !canCreateRequest(status);
  const blockReason = getBlockReason(status);

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <span>Share Request Limits</span>
        </CardTitle>
        <CardDescription>Your current usage and limits</CardDescription>
      </CardHeader>

      <CardContent className="space-y-6">
        {/* Pending Requests Progress */}
        <RateLimitProgress
          current={status.currentPendingCount}
          max={status.maxPendingAllowed}
          label="Pending Requests"
        />

        {/* Monthly Requests Progress */}
        <RateLimitProgress
          current={status.currentMonthlyCount}
          max={status.maxMonthlyAllowed}
          label="Monthly Requests"
          resetAt={status.monthResetAt}
        />

        {/* Cooldown Alert */}
        {status.isInCooldown && status.cooldownEndsAt && (
          <Alert variant="destructive">
            <AlertCircle className="h-4 w-4" />
            <AlertTitle>Cooldown Active</AlertTitle>
            <AlertDescription>
              You can submit new requests again on{' '}
              {format(new Date(status.cooldownEndsAt), 'MMM d \'at\' h:mm a')}
            </AlertDescription>
          </Alert>
        )}

        {/* Block Reason (if not in cooldown but still blocked) */}
        {blocked && !status.isInCooldown && blockReason && (
          <Alert>
            <AlertCircle className="h-4 w-4" />
            <AlertTitle>Request Limit Reached</AlertTitle>
            <AlertDescription>{blockReason}</AlertDescription>
          </Alert>
        )}
      </CardContent>
    </Card>
  );
}
