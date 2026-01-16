/* eslint-disable security/detect-object-injection */
/**
 * QuotaStatusBar Component (Issue #2445)
 *
 * Displays library quota usage with visual progress bar and tier information.
 * Shows upgrade prompt when user approaches or reaches limit.
 */

import { AlertCircle, ChevronRight } from 'lucide-react';
import Link from 'next/link';

import { Progress } from '@/components/ui/progress';
import { cn } from '@/lib/utils';

export interface QuotaStatusBarProps {
  currentCount: number;
  maxAllowed: number;
  userTier: 'free' | 'normal' | 'premium';
  percentageUsed: number;
  remainingSlots: number;
  className?: string;
  showUpgradeLink?: boolean;
}

const TIER_LABELS: Record<'free' | 'normal' | 'premium', string> = {
  free: 'Free',
  normal: 'Normal',
  premium: 'Premium',
};

const TIER_UPGRADE_INFO: Record<'free' | 'normal', { next: string; additionalSlots: number }> = {
  free: { next: 'Normal', additionalSlots: 15 },
  normal: { next: 'Premium', additionalSlots: 30 },
};

export function QuotaStatusBar({
  currentCount,
  maxAllowed,
  userTier,
  percentageUsed,
  remainingSlots,
  className,
  showUpgradeLink = true,
}: QuotaStatusBarProps) {
  const isNearLimit = percentageUsed >= 80;
  const isAtLimit = currentCount >= maxAllowed;
  const canUpgrade = userTier !== 'premium';
  const upgradeInfo = userTier !== 'premium' ? TIER_UPGRADE_INFO[userTier] : null;

  return (
    <div
      className={cn(
        'rounded-lg border bg-card p-4 text-card-foreground shadow-sm',
        isAtLimit && 'border-destructive/50 bg-destructive/5',
        isNearLimit && !isAtLimit && 'border-yellow-500/50 bg-yellow-50/50 dark:bg-yellow-950/20',
        className
      )}
    >
      {/* Header with count */}
      <div className="mb-2 flex items-center justify-between">
        <h3 className="text-sm font-medium">
          La tua libreria: {currentCount}/{maxAllowed} giochi
        </h3>
        {isAtLimit && (
          <div className="flex items-center gap-1 text-xs text-destructive">
            <AlertCircle className="h-3 w-3" />
            <span>Limite raggiunto</span>
          </div>
        )}
      </div>

      {/* Progress bar */}
      <Progress
        value={percentageUsed}
        className={cn(
          'h-2',
          isAtLimit && 'bg-destructive/20',
          isNearLimit && !isAtLimit && 'bg-yellow-200 dark:bg-yellow-900'
        )}
      />

      {/* Footer with tier and upgrade info */}
      <div className="mt-2 flex items-center justify-between text-xs text-muted-foreground">
        <span>
          Piano {TIER_LABELS[userTier]}
          {remainingSlots > 0 && ` \u2022 ${remainingSlots} slot disponibili`}
        </span>

        {canUpgrade && showUpgradeLink && upgradeInfo && (
          <Link
            href="/account/subscription"
            className={cn(
              'flex items-center gap-1 font-medium transition-colors hover:text-foreground',
              isNearLimit && 'text-yellow-700 dark:text-yellow-400',
              isAtLimit && 'text-destructive'
            )}
          >
            Passa a {upgradeInfo.next} per +{upgradeInfo.additionalSlots}
            <ChevronRight className="h-3 w-3" />
          </Link>
        )}
      </div>

      {/* Warning message for users near/at limit */}
      {isNearLimit && !isAtLimit && (
        <div className="mt-3 rounded-md bg-yellow-100 p-2 text-xs text-yellow-800 dark:bg-yellow-950 dark:text-yellow-200">
          <p>Stai per raggiungere il limite della tua libreria.</p>
        </div>
      )}

      {isAtLimit && (
        <div className="mt-3 rounded-md bg-destructive/10 p-2 text-xs text-destructive">
          <p>
            Hai raggiunto il limite di {maxAllowed} giochi.{' '}
            {canUpgrade && (
              <Link href="/account/subscription" className="font-medium underline">
                Passa a {upgradeInfo?.next}
              </Link>
            )}{' '}
            per aggiungerne altri.
          </p>
        </div>
      )}
    </div>
  );
}
